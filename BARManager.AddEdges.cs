using System.Collections.Generic;
using Timberborn.BlockSystem;
using Timberborn.NaturalResources;
using Timberborn.Navigation;
using Timberborn.Planting;
using UnityEngine;

namespace Calloatti.BeaversForReal
{
  public partial class BARManager
  {
    private void ProcessAndAddEdges()
    {
      List<int> potentialLedges = new List<int>();
      int maxNodes = _nodeIdService.NumberOfNodes;

      // Our O(1) cache map. If it's true, we already know it's a valid floor.
      bool[] isStandable = new bool[maxNodes];

      // Phase 1: The Initial Scan and Filter
      for (int nodeId = 0; nodeId < maxNodes; nodeId++)
      {
        Vector3Int coords = _nodeIdService.IdToGrid(nodeId);

        // Do the expensive physical check EXACTLY ONCE per node
        if (!IsStandableSurface(coords)) continue;

        // Cache it so Phase 2 can just look it up instantly
        isStandable[nodeId] = true;

        // Check NavMesh and subconditions for jumping OFF
        if (_terrainNavMeshGraph.IsOnNavMesh(nodeId))
        {
          if (_restrictedNodeMap.IsNodeRestricted(nodeId)) continue;
          if (_terrainNavMeshGraph.GetNeighbors(nodeId).Count == 8) continue;
        }
        else
        {
          // 1: Check if there is a building right in the spot (ONLY if not in NavMesh)
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

      // Phase 2: Neighbor Processing and Edge Creation
      foreach (int upperNodeId in potentialLedges)
      {
        Vector3Int upper = _nodeIdService.IdToGrid(upperNodeId);

        // Grab the vanilla edges once per ledge
        var vanillaNeighbors = _terrainNavMeshGraph.GetNeighbors(upperNodeId);

        foreach (var d in _cardinalDeltas)
        {
          Vector3Int neighborXY = upper + d;
          if (!_terrainService.Contains(neighborXY)) continue;

          // Does vanilla already have a path in this exact X/Y direction? (e.g. Stairs/Ramps)
          bool hasVanillaEdge = false;
          for (int i = 0; i < vanillaNeighbors.Count; i++)
          {
            Vector3Int vNeighborCoords = _nodeIdService.IdToGrid(vanillaNeighbors[i].Id);
            if (vNeighborCoords.x == neighborXY.x && vNeighborCoords.y == neighborXY.y)
            {
              hasVanillaEdge = true;
              break;
            }
          }
          if (hasVanillaEdge) continue;

          int lowerNodeId = -1;
          Vector3Int lower = Vector3Int.zero;

          // Scan Z downwards using our cached array
          for (int z = upper.z - 1; z > 0; z--)
          {
            Vector3Int checkCoords = new Vector3Int(neighborXY.x, neighborXY.y, z);
            int checkId = _nodeIdService.GridToId(checkCoords);

            // O(1) lookup. No recalculation.
            if (isStandable[checkId])
            {
              lowerNodeId = checkId;
              lower = checkCoords;
              break;
            }
          }

          // If we hit the void, or didn't find a valid drop-off
          if (lowerNodeId == -1) continue;

          long hash = GetHash(upper, lower);
          if (_shorelineDict.ContainsKey(hash)) continue;

          // Create the edge unblocked for testing
          var newEdge = new BAREdge(upperNodeId, upper, lowerNodeId, lower) { IsBlockedByWater = false };

          if (IsLedgePhysicallyValid(newEdge))
          {
            _shorelineDict.Add(hash, newEdge);
            _shorelines.Add(newEdge);

            newEdge.EdgeDown = NavMeshEdge.CreateGrouped(newEdge.Upper, newEdge.Lower, ShorelineGroupId, false, 1.0f);
            newEdge.EdgeUp = NavMeshEdge.CreateGrouped(newEdge.Lower, newEdge.Upper, ShorelineGroupId, false, 1.0f);

            _navMeshService.AddEdge(newEdge.EdgeDown);
            _navMeshService.AddEdge(newEdge.EdgeUp);
          }
        }
      }
    }

    private bool IsLedgePhysicallyValid(BAREdge edge)
    {
      // Check the air gap between the ledge and the landing spot for solid obstructions
      for (int z = edge.Upper.z; z > edge.Lower.z; z--)
      {
        Vector3Int checkCoords = new Vector3Int(edge.Lower.x, edge.Lower.y, z);

        // 2: Check if there are terrain blocks in the path
        if (_terrainService.GetTerrainHeightBelow(new Vector3Int(checkCoords.x, checkCoords.y, z + 1)) >= z)
        {
          return false;
        }

        // Check if there are building blocks in the path
        var objectsAtLevel = _blockService.GetObjectsAt(checkCoords);
        for (int i = 0; i < objectsAtLevel.Count; i++)
        {
          var obj = objectsAtLevel[i];
          if (obj.GetComponentOfNullable<PlantableSpec>() == null && obj.GetComponentOfNullable<NaturalResourceSpec>() == null)
          {
            return false;
          }
        }
      }
      return true;
    }

    private long GetHash(Vector3Int u, Vector3Int l) => (((long)u.x & 0x3FF) << 50) | (((long)u.y & 0x3FF) << 40) | (((long)u.z & 0x3FF) << 30) | (((long)l.x & 0x3FF) << 20) | (((long)l.y & 0x3FF) << 10) | ((long)l.z & 0x3FF);
  }
}