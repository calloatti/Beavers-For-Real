using Bindito.Core;
using Calloatti.Config;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.BlockSystem;
using Timberborn.EntitySystem;
using Timberborn.InputSystem;
using Timberborn.Localization;
using Timberborn.Modding;
using Timberborn.ModManagerScene;
using Timberborn.Navigation;
using Timberborn.QuickNotificationSystem;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystem;
using Timberborn.WaterSystem;
using UnityEngine;

namespace Calloatti.BeaversForReal
{
  public class BeaversForRealStarter : IModStarter
  {
    public static SimpleConfig Config { get; private set; }

    public void StartMod(IModEnvironment modEnvironment)
    {
      Config = new SimpleConfig(modEnvironment.ModPath);
      new Harmony("calloatti.beaversforreal").PatchAll();
    }
  }

  [Context("Game")]
  public class BeaversForRealConfigurator : Configurator
  {
    protected override void Configure()
    {
      Bind<ShorelineManager>().AsSingleton();
      Bind<DebugInputService>().AsSingleton();
    }
  }

  [HarmonyPatch]
  public static class BeaversForRealPatches
  {
    [HarmonyPatch(typeof(NavMeshSource), nameof(NavMeshSource.BlockEdge))]
    [HarmonyPrefix]
    public static bool BlockEdge_Prefix(int startNodeId, int endNodeId, int groupId) => groupId != ShorelineManager.ShorelineGroupId;

    [HarmonyPatch(typeof(NavMeshSource), nameof(NavMeshSource.UnblockEdge))]
    [HarmonyPrefix]
    public static bool UnblockEdge_Prefix(int startNodeId, int endNodeId, int groupId) => groupId != ShorelineManager.ShorelineGroupId;
  }

  public class DebugInputService : ILoadableSingleton, IInputProcessor, IDisposable
  {
    private readonly InputService _inputService;

    public event Action OnToggleDebug;

    [Inject]
    public DebugInputService(InputService inputService)
    {
      _inputService = inputService;
    }

    public void Load()
    {
      _inputService.AddInputProcessor(this);
    }

    public bool ProcessInput()
    {
      if (_inputService.IsKeyDown("Calloatti.BeaversForReal.KeyBind.Toggle"))
      {
        OnToggleDebug?.Invoke();
        return false;
      }
      return false;
    }

    public void Dispose()
    {
      _inputService.RemoveInputProcessor(this);
    }
  }

  public class ShorelineEdge
  {
    public Vector3Int Upper;
    public Vector3Int Lower;
    public bool IsActive;
    public bool IsBlockedByBuilding; // New flag for the deferred check
    public NavMeshEdge EdgeDown;
    public NavMeshEdge EdgeUp;

    public ShorelineEdge(Vector3Int upper, Vector3Int lower)
    {
      Upper = upper;
      Lower = lower;
    }
  }

  public class ShorelineManager : ILoadableSingleton, IUpdatableSingleton, IDisposable
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
    private readonly DebugInputService _debugInputService;
    private readonly QuickNotificationService _quickNotificationService;
    private readonly ILoc _loc;

    private readonly List<ShorelineEdge> _shorelines = new List<ShorelineEdge>();
    private PathRenderer _visualizer;

    private int _currentEdgeIndex = 0;
    private const int EdgesPerFrame = 150;

    private bool _needsRescan = false;
    private float _rescanCooldown = 0f;
    private const float DebounceTime = 1.0f;

    public ShorelineManager(
      ITerrainService terrainService,
      INavMeshService navMeshService,
      NavMeshGroupService navMeshGroupService,
      IThreadSafeWaterMap waterMap,
      IBlockService blockService,
      StackableBlockService stackableBlockService,
      EntityComponentRegistry entityComponentRegistry,
      DebugInputService debugInputService,
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
      _visualizer = visualizerObj.AddComponent<PathRenderer>();
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
      if (!_needsRescan) ClearActiveEdges();
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

      // PASS 1: Fast Terrain Scan
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

    private void FindAndAddLedges(Vector3Int upper, Vector3Int[] deltas, HashSet<long> hashes)
    {
      foreach (var d in deltas)
      {
        Vector3Int neighbor = new Vector3Int(upper.x + d.x, upper.y + d.y, upper.z);
        if (!_terrainService.Contains(neighbor)) continue;

        int neighborZ = _terrainService.GetTerrainHeightBelow(neighbor);

        // Find highest surface in neighbor column (Terrain or Platform)
        for (int z = upper.z - 1; z >= neighborZ; z--)
        {
          Vector3Int checkCoords = new Vector3Int(neighbor.x, neighbor.y, z);
          bool isWalkable = (z == neighborZ) || _stackableBlockService.IsFinishedStackableBlockAt(checkCoords);

          if (isWalkable)
          {
            if (z < upper.z)
            {
              // Perfect hash using 10 bits per coordinate component (supports sizes up to 1024)
              long hash = (((long)upper.x & 0x3FF) << 50) | (((long)upper.y & 0x3FF) << 40) | (((long)upper.z & 0x3FF) << 30) | (((long)checkCoords.x & 0x3FF) << 20) | (((long)checkCoords.y & 0x3FF) << 10) | ((long)checkCoords.z & 0x3FF);
              if (hashes.Add(hash)) _shorelines.Add(new ShorelineEdge(upper, checkCoords));
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
        if (_rescanCooldown <= 0f) { _needsRescan = false; RescanMap(); }
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

    private void ProcessSingleEdge(ShorelineEdge s)
    {
      // STEP 1 - Island Pardon Logic
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
        // STEP 2 - FINAL NON EDITABLE
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
        // STEP 3
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

      // 2. WATER LOGIC
      float depth = _waterMap.WaterDepth(s.Lower);
      float waterSurfaceZ = s.Lower.z + depth;
      float delta = s.Upper.z - waterSurfaceZ;

      // Fetch the max allowable height from the config
      float maxNavigationDelta = BeaversForRealStarter.Config.GetFloat("MaxWaterNavigationHeight");

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

    private class PathRenderer : MonoBehaviour
    {
      public ShorelineManager Manager;
      private Material _mat;
      private Mesh _cubeGreen;
      private Mesh _cubeRed;
      public List<ShorelineEdge> Shorelines;

      void Awake()
      {
        _mat = new Material(Shader.Find("Hidden/Internal-Colored"));
        _mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        _cubeGreen = CreateCube(Color.green);
        _cubeRed = CreateCube(Color.red);
      }

      void OnDestroy()
      {
        if (_mat != null) Destroy(_mat);
        if (_cubeGreen != null) Destroy(_cubeGreen);
        if (_cubeRed != null) Destroy(_cubeRed);
      }

      private Mesh CreateCube(Color color)
      {
        Mesh m = new Mesh();
        m.vertices = new Vector3[] { new Vector3(-0.05f, -0.05f, -0.05f), new Vector3(0.05f, -0.05f, -0.05f), new Vector3(0.05f, 0.05f, -0.05f), new Vector3(-0.05f, 0.05f, -0.05f), new Vector3(-0.05f, -0.05f, 0.05f), new Vector3(0.05f, -0.05f, 0.05f), new Vector3(0.05f, 0.05f, 0.05f), new Vector3(-0.05f, 0.05f, 0.05f) };
        Color[] cols = new Color[8]; for (int i = 0; i < 8; i++) cols[i] = color;
        m.colors = cols;
        m.SetIndices(new int[] { 0, 1, 1, 2, 2, 3, 3, 0, 4, 5, 5, 6, 6, 7, 7, 4, 0, 4, 1, 5, 2, 6, 3, 7 }, MeshTopology.Lines, 0);
        return m;
      }

      void OnRenderObject()
      {
        if (Manager == null || !Manager.DebugEnabled || Shorelines == null || _mat == null) return;

        _mat.SetPass(0);
        foreach (var s in Shorelines)
        {
          // Visually: Green if active, Red if blocked by building OR dry
          bool visualActive = s.IsActive;
          Vector3 u = NavigationCoordinateSystem.GridToWorld(s.Upper);
          Vector3 l = NavigationCoordinateSystem.GridToWorld(s.Lower);
          Graphics.DrawMeshNow(visualActive ? _cubeGreen : _cubeRed, Matrix4x4.Translate(u));
          Graphics.DrawMeshNow(visualActive ? _cubeGreen : _cubeRed, Matrix4x4.Translate(l));
          GL.Begin(GL.LINES);
          GL.Color(visualActive ? Color.green : Color.red);
          GL.Vertex(u); GL.Vertex(l);
          GL.End();
        }
      }
    }
  }
}