using System.Collections.Generic;
using HarmonyLib;
using Timberborn.BlockSystem;
using Timberborn.NaturalResources;
using Timberborn.Navigation;
using Timberborn.Planting;
using UnityEngine;

namespace Calloatti.BeaversForReal
{
  public partial class BARManager
  {
    private static BARManager _instance;
    private static bool _dynamicUpdatesEnabled = false;
    private static bool _processRegularChangesFirstRun = true;
    private static readonly HashSet<Vector3Int> _pendingUpdateCoordinates = new HashSet<Vector3Int>();

    public void EnableDynamicUpdates()
    {
      _instance = this;
      _dynamicUpdatesEnabled = true;
      _processRegularChangesFirstRun = true;
    }

    public void SetInstance(BARManager instance) => _instance = instance;

    [HarmonyPatch(typeof(NavigationSynchronizer), "ProcessRegularChanges")]
    private static class ProcessRegularChangesPatch
    {
      [HarmonyPrefix]
      public static void Prefix(NavigationSynchronizer __instance)
      {
        if (!_dynamicUpdatesEnabled || _instance == null) return;
        if (_processRegularChangesFirstRun) { _processRegularChangesFirstRun = false; return; }

        var updater = __instance._navMeshUpdater;
        if (updater == null) return;

        ExtractNodes(updater._enqueuedRegularTerrainChanges);
        ExtractNodes(updater._enqueuedRegularRoadChanges);
      }

      private static void ExtractNodes(Queue<NavMeshChange> queue)
      {
        if (queue == null) return;
        foreach (var change in queue)
        {
          _pendingUpdateCoordinates.Add(_instance._nodeIdService.IdToGrid(change._startNodeId));
          _pendingUpdateCoordinates.Add(_instance._nodeIdService.IdToGrid(change._endNodeId));
        }
      }

      [HarmonyPostfix]
      public static void Postfix()
      {
        if (!_dynamicUpdatesEnabled || _instance == null || _pendingUpdateCoordinates.Count == 0) return;
        var coords = new List<Vector3Int>(_pendingUpdateCoordinates);
        _pendingUpdateCoordinates.Clear();
        _instance.ProcessLocalizedChange(coords);
      }
    }

    private void ProcessLocalizedChange(IEnumerable<Vector3Int> changedCoordinates)
    {
      int minX = int.MaxValue, minY = int.MaxValue;
      int maxX = int.MinValue, maxY = int.MinValue;
      bool hasCoords = false;

      foreach (var coord in changedCoordinates)
      {
        hasCoords = true;
        if (coord.x < minX) minX = coord.x;
        if (coord.y < minY) minY = coord.y;
        if (coord.x > maxX) maxX = coord.x;
        if (coord.y > maxY) maxY = coord.y;
      }
      if (!hasCoords) return;

      // ±1 block padding for search and deletion bounds
      int searchMinX = minX - 1; int searchMaxX = maxX + 1;
      int searchMinY = minY - 1; int searchMaxY = maxY + 1;

      Dictionary<long, BAREdge> ThisEdgesAreOk = new Dictionary<long, BAREdge>();
      int maxNodes = _nodeIdService.NumberOfNodes;
      bool[] isStandable = new bool[maxNodes];
      List<int> potentialLedges = new List<int>();

      // --- PHASE 1: SCAN (1:1 with AddEdges.cs snippet) ---
      for (int x = searchMinX; x <= searchMaxX; x++)
      {
        for (int y = searchMinY; y <= searchMaxY; y++)
        {
          if (!_terrainService.Contains(new Vector2Int(x, y))) continue;
          for (int z = 0; z < _terrainService.Size.z; z++)
          {
            Vector3Int coords = new Vector3Int(x, y, z);
            int nodeId = _nodeIdService.GridToId(coords);

            if (!IsStandableSurface(coords)) continue;
            isStandable[nodeId] = true;

            if (_terrainNavMeshGraph.IsOnNavMesh(nodeId))
            {
              if (_restrictedNodeMap.IsNodeRestricted(nodeId)) continue;

              // FIX: Ignore our own custom edges when counting neighbors to prevent flip-flopping
              var neighbors = _terrainNavMeshGraph.GetNeighbors(nodeId);
              int vanillaCount = 0;
              for (int i = 0; i < neighbors.Count; i++)
              {
                Vector3Int nCoords = _nodeIdService.IdToGrid(neighbors[i].Id);
                if (!_shorelineDict.ContainsKey(GetHash(coords, nCoords)) && !_shorelineDict.ContainsKey(GetHash(nCoords, coords)))
                {
                  vanillaCount++;
                }
              }
              if (vanillaCount == 8) continue;
            }
            else
            {
              bool hasBuilding = false;
              var objectsAtSpot = _blockService.GetObjectsAt(coords);
              for (int i = 0; i < objectsAtSpot.Count; i++)
              {
                var obj = objectsAtSpot[i];
                if (obj.GetComponentOfNullable<PlantableSpec>() == null && obj.GetComponentOfNullable<NaturalResourceSpec>() == null)
                {
                  hasBuilding = true;
                  break;
                }
              }
              if (hasBuilding) continue;
            }
            potentialLedges.Add(nodeId);
          }
        }
      }

      // --- PHASE 2: NEIGHBOR PROCESSING (1:1 with AddEdges.cs snippet) ---
      foreach (int upperNodeId in potentialLedges)
      {
        Vector3Int upper = _nodeIdService.IdToGrid(upperNodeId);
        var vanillaNeighbors = _terrainNavMeshGraph.GetNeighbors(upperNodeId);

        foreach (var d in _cardinalDeltas)
        {
          Vector3Int neighborXY = upper + d;
          if (!_terrainService.Contains(new Vector2Int(neighborXY.x, neighborXY.y))) continue;

          bool hasVanillaEdge = false;
          for (int i = 0; i < vanillaNeighbors.Count; i++)
          {
            Vector3Int vNeighborCoords = _nodeIdService.IdToGrid(vanillaNeighbors[i].Id);
            if (vNeighborCoords.x == neighborXY.x && vNeighborCoords.y == neighborXY.y)
            {
              // FIX: If the connection is our own custom edge, it's not a vanilla block path. Ignore it.
              if (_shorelineDict.ContainsKey(GetHash(upper, vNeighborCoords)) || _shorelineDict.ContainsKey(GetHash(vNeighborCoords, upper)))
              {
                continue;
              }
              hasVanillaEdge = true;
              break;
            }
          }
          if (hasVanillaEdge) continue;

          for (int z = upper.z - 1; z > 0; z--)
          {
            Vector3Int checkCoords = new Vector3Int(neighborXY.x, neighborXY.y, z);
            int checkId = _nodeIdService.GridToId(checkCoords);
            if (isStandable[checkId])
            {
              long hash = GetHash(upper, checkCoords);
              var candidate = new BAREdge(upperNodeId, upper, checkId, checkCoords) { IsBlockedByWater = false };
              if (IsLedgePhysicallyValid(candidate))
              {
                if (_shorelineDict.TryGetValue(hash, out var existing))
                  ThisEdgesAreOk[hash] = existing;
                else
                  ThisEdgesAreOk[hash] = candidate;
              }
              break;
            }
          }
        }
      }

      // --- PHASE 3: DELETION (upperIn && lowerIn) ---
      for (int i = _shorelines.Count - 1; i >= 0; i--)
      {
        var edge = _shorelines[i];

        bool upperIn = edge.Upper.x >= searchMinX && edge.Upper.x <= searchMaxX && edge.Upper.y >= searchMinY && edge.Upper.y <= searchMaxY;
        bool lowerIn = edge.Lower.x >= searchMinX && edge.Lower.x <= searchMaxX && edge.Lower.y >= searchMinY && edge.Lower.y <= searchMaxY;

        if (upperIn && lowerIn)
        {
          long hash = GetHash(edge.Upper, edge.Lower);
          if (!ThisEdgesAreOk.ContainsKey(hash))
          {
            _shorelineDict.Remove(hash);
            _shorelines.RemoveAt(i);
            _navMeshService.RemoveEdge(edge.EdgeDown);
            _navMeshService.RemoveEdge(edge.EdgeUp);
          }
        }
      }

      // --- PHASE 4: ADDITION ---
      foreach (var kvp in ThisEdgesAreOk)
      {
        if (!_shorelineDict.ContainsKey(kvp.Key))
        {
          kvp.Value.EdgeDown = NavMeshEdge.CreateGrouped(kvp.Value.Upper, kvp.Value.Lower, ShorelineGroupId, false, 1.0f);
          kvp.Value.EdgeUp = NavMeshEdge.CreateGrouped(kvp.Value.Lower, kvp.Value.Upper, ShorelineGroupId, false, 1.0f);

          _shorelineDict.Add(kvp.Key, kvp.Value);
          _shorelines.Add(kvp.Value);
          _navMeshService.AddEdge(kvp.Value.EdgeDown);
          _navMeshService.AddEdge(kvp.Value.EdgeUp);
        }
      }
    }
  }
}