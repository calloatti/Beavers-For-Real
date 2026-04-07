using System;
using System.Collections.Generic;
using Timberborn.BlockSystem;
using Timberborn.EntitySystem;
using Timberborn.Localization;
using Timberborn.Navigation;
using Timberborn.QuickNotificationSystem;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystem;
using Timberborn.WaterSystem;
using UnityEngine;

namespace Calloatti.BeaversForReal
{
  public class BVRShorelineManager : ILoadableSingleton, IUpdatableSingleton, IDisposable
  {
    public bool DebugEnabled = false;
    public static int ShorelineGroupId { get; private set; } = -1;

    private readonly ITerrainService _terrainService;
    private readonly INavMeshService _navMeshService;
    private readonly NavMeshGroupService _navMeshGroupService;
    private readonly IThreadSafeWaterMap _waterMap;
    private readonly IBlockService _blockService;
    private readonly StackableBlockService _stackableBlockService;
    private readonly EntityComponentRegistry _entityComponentRegistry;
    private readonly BVRInputService _debugInputService;
    private readonly QuickNotificationService _quickNotificationService;
    private readonly ILoc _loc;

    private readonly List<BVRShorelineEdge> _shorelines = new List<BVRShorelineEdge>();
    private readonly HashSet<Vector2Int> _invalidatedCells = new HashSet<Vector2Int>();
    private BVRPathRenderer _visualizer;

    private int _currentEdgeIndex = 0;
    private const int EdgesPerFrame = 150;

    private bool _needsRescan = false;
    private float _rescanCooldown = 0f;
    private const float DebounceTime = 1.0f;

    public BVRShorelineManager(
      ITerrainService terrainService,
      INavMeshService navMeshService,
      NavMeshGroupService navMeshGroupService,
      IThreadSafeWaterMap waterMap,
      IBlockService blockService,
      StackableBlockService stackableBlockService,
      EntityComponentRegistry entityComponentRegistry,
      BVRInputService debugInputService,
      QuickNotificationService quickNotificationService,
      ILoc loc)
    {
      _terrainService = terrainService;
      _navMeshService = navMeshService;
      _navMeshGroupService = navMeshGroupService;
      _waterMap = waterMap;
      _blockService = blockService;
      _stackableBlockService = stackableBlockService;
      _entityComponentRegistry = entityComponentRegistry;
      _debugInputService = debugInputService;
      _quickNotificationService = quickNotificationService;
      _loc = loc;
    }

    public void Load()
    {
      ShorelineGroupId = _navMeshGroupService.GetOrAddGroupId("Calloatti.BeaversForReal");

      GameObject visualizerObj = new GameObject("BeaversForReal_Visualizer");
      _visualizer = visualizerObj.AddComponent<BVRPathRenderer>();
      _visualizer.Manager = this;
      _visualizer.Shorelines = _shorelines;

      _debugInputService.OnToggleDebug += ToggleDebug;

      RescanMap();
      _terrainService.TerrainHeightChanged += OnMapChanged;
    }

    public void Dispose()
    {
      ClearActiveEdges();
      if (_terrainService != null) _terrainService.TerrainHeightChanged -= OnMapChanged;
      if (_debugInputService != null) _debugInputService.OnToggleDebug -= ToggleDebug;
      if (_visualizer != null) UnityEngine.Object.Destroy(_visualizer.gameObject);
    }

    private void ToggleDebug()
    {
      DebugEnabled = !DebugEnabled;
      string locKey = DebugEnabled ? "Calloatti.BeaversForReal.DebugOn" : "Calloatti.BeaversForReal.DebugOff";
      _quickNotificationService.SendNotification(_loc.T(locKey));
    }

    private void OnMapChanged(object sender, TerrainHeightChangeEventArgs e)
    {
      Vector2Int center = e.Change.Coordinates;

      for (int dx = -1; dx <= 1; dx++)
      {
        for (int dy = -1; dy <= 1; dy++)
        {
          Vector2Int cell = new Vector2Int(center.x + dx, center.y + dy);
          if (_terrainService.Contains(cell))
          {
            _invalidatedCells.Add(cell);
          }
        }
      }

      _needsRescan = true;
      _rescanCooldown = DebounceTime;
    }

    private void ClearActiveEdges()
    {
      foreach (var edge in _shorelines)
      {
        if (edge.IsActive)
        {
          try { _navMeshService.RemoveEdge(edge.EdgeDown); } catch { }
          try { _navMeshService.RemoveEdge(edge.EdgeUp); } catch { }
          edge.IsActive = false;
        }
      }
    }

    private void RescanMap()
    {
      ClearActiveEdges();
      _shorelines.Clear();
      _currentEdgeIndex = 0;

      Vector3Int size = _terrainService.Size;
      HashSet<long> processedHashes = new HashSet<long>();
      Vector3Int[] orthogonalDeltas = { new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0) };

      for (int x = 0; x < size.x; x++)
      {
        for (int y = 0; y < size.y; y++)
        {
          Vector2Int cell = new Vector2Int(x, y);
          foreach (Vector3Int terrainLedge in _terrainService.GetAllHeightsInCell(cell))
          {
            FindAndAddLedges(terrainLedge, orthogonalDeltas, processedHashes);
          }
        }
      }
    }

    private void UpdateInvalidatedCells()
    {
      for (int i = _shorelines.Count - 1; i >= 0; i--)
      {
        BVRShorelineEdge edge = _shorelines[i];
        Vector2Int upperXY = new Vector2Int(edge.Upper.x, edge.Upper.y);
        Vector2Int lowerXY = new Vector2Int(edge.Lower.x, edge.Lower.y);

        if (_invalidatedCells.Contains(upperXY) || _invalidatedCells.Contains(lowerXY))
        {
          if (edge.IsActive)
          {
            try { _navMeshService.RemoveEdge(edge.EdgeDown); } catch { }
            try { _navMeshService.RemoveEdge(edge.EdgeUp); } catch { }
          }
          _shorelines.RemoveAt(i);
        }
      }

      HashSet<long> processedHashes = new HashSet<long>();
      Vector3Int[] orthogonalDeltas = { new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0) };

      foreach (Vector2Int cell in _invalidatedCells)
      {
        foreach (Vector3Int terrainLedge in _terrainService.GetAllHeightsInCell(cell))
        {
          FindAndAddLedges(terrainLedge, orthogonalDeltas, processedHashes);
        }
      }

      _invalidatedCells.Clear();
      _currentEdgeIndex = 0;
    }

    private void FindAndAddLedges(Vector3Int upper, Vector3Int[] deltas, HashSet<long> hashes)
    {
      foreach (var d in deltas)
      {
        Vector3Int neighbor = new Vector3Int(upper.x + d.x, upper.y + d.y, upper.z);
        if (!_terrainService.Contains(neighbor)) continue;

        int neighborZ = _terrainService.GetTerrainHeightBelow(neighbor);

        for (int z = upper.z - 1; z >= neighborZ; z--)
        {
          Vector3Int checkCoords = new Vector3Int(neighbor.x, neighbor.y, z);
          bool isWalkable = (z == neighborZ) || _stackableBlockService.IsFinishedStackableBlockAt(checkCoords);

          if (isWalkable)
          {
            if (z < upper.z)
            {
              long hash = (((long)upper.x & 0x3FF) << 50) | (((long)upper.y & 0x3FF) << 40) | (((long)upper.z & 0x3FF) << 30) | (((long)checkCoords.x & 0x3FF) << 20) | (((long)checkCoords.y & 0x3FF) << 10) | ((long)checkCoords.z & 0x3FF);
              if (hashes.Add(hash)) _shorelines.Add(new BVRShorelineEdge(upper, checkCoords));
            }
            break;
          }
        }
      }
    }

    public void UpdateSingleton()
    {
      if (_needsRescan)
      {
        _rescanCooldown -= Time.deltaTime;
        if (_rescanCooldown <= 0f)
        {
          _needsRescan = false;
          UpdateInvalidatedCells();
        }
        return;
      }
      if (_shorelines.Count == 0) return;

      int processed = 0;
      while (processed < EdgesPerFrame)
      {
        if (_currentEdgeIndex >= _shorelines.Count) _currentEdgeIndex = 0;
        ProcessSingleEdge(_shorelines[_currentEdgeIndex]);
        _currentEdgeIndex++;
        processed++;
      }
    }

    private void ProcessSingleEdge(BVRShorelineEdge s)
    {
      bool upperValid = _navMeshService.IsOnNavMesh(s.Upper);
      if (!upperValid)
      {
        bool isUpperIsland = !_navMeshService.IsOnNavMesh(new Vector3Int(s.Upper.x + 1, s.Upper.y, s.Upper.z)) &&
                             !_navMeshService.IsOnNavMesh(new Vector3Int(s.Upper.x - 1, s.Upper.y, s.Upper.z)) &&
                             !_navMeshService.IsOnNavMesh(new Vector3Int(s.Upper.x, s.Upper.y + 1, s.Upper.z)) &&
                             !_navMeshService.IsOnNavMesh(new Vector3Int(s.Upper.x, s.Upper.y - 1, s.Upper.z));
        if (isUpperIsland) upperValid = true;
      }

      bool lowerValid = _navMeshService.IsOnNavMesh(s.Lower);
      if (!lowerValid)
      {
        bool isLowerIsland = !_navMeshService.IsOnNavMesh(new Vector3Int(s.Lower.x + 1, s.Lower.y, s.Lower.z)) &&
                             !_navMeshService.IsOnNavMesh(new Vector3Int(s.Lower.x - 1, s.Lower.y, s.Lower.z)) &&
                             !_navMeshService.IsOnNavMesh(new Vector3Int(s.Lower.x, s.Lower.y + 1, s.Lower.z)) &&
                             !_navMeshService.IsOnNavMesh(new Vector3Int(s.Lower.x, s.Lower.y - 1, s.Lower.z));
        if (isLowerIsland) lowerValid = true;
      }

      bool isBlocked = !upperValid || !lowerValid;

      if (!isBlocked)
      {
        for (int z = s.Lower.z + 1; z <= s.Upper.z; z++)
        {
          if (_navMeshService.IsOnNavMesh(new Vector3Int(s.Lower.x, s.Lower.y, z)))
          {
            isBlocked = true;
            break;
          }
        }
      }

      if (!isBlocked)
      {
        for (int z = s.Lower.z + 1; z <= s.Upper.z + 1; z++)
        {
          if (_stackableBlockService.IsFinishedStackableBlockAt(new Vector3Int(s.Lower.x, s.Lower.y, z)))
          {
            isBlocked = true;
            break;
          }
        }
      }

      s.IsBlockedByBuilding = isBlocked;

      float depth = _waterMap.WaterDepth(s.Lower);
      float waterSurfaceZ = s.Lower.z + depth;
      float delta = s.Upper.z - waterSurfaceZ;

      float maxNavigationDelta = ModStarter.Config.GetFloat("MaxWaterNavigationHeight");

      bool shouldBeActive = !s.IsBlockedByBuilding && delta <= maxNavigationDelta && depth > 0.1f;

      if (shouldBeActive && !s.IsActive)
      {
        s.IsActive = true;
        s.EdgeDown = NavMeshEdge.CreateGrouped(s.Upper, s.Lower, ShorelineGroupId, false, 1.5f);
        s.EdgeUp = NavMeshEdge.CreateGrouped(s.Lower, s.Upper, ShorelineGroupId, false, 1.5f);
        _navMeshService.AddEdge(s.EdgeDown);
        _navMeshService.AddEdge(s.EdgeUp);
      }
      else if (!shouldBeActive && s.IsActive)
      {
        s.IsActive = false;
        try { _navMeshService.RemoveEdge(s.EdgeDown); } catch { }
        try { _navMeshService.RemoveEdge(s.EdgeUp); } catch { }
      }
    }
  }
}