using Timberborn.Navigation;
using UnityEngine;

namespace Calloatti.BeaversForReal
{
  public class BAREdge
  {
    public int UpperNodeId;
    public Vector3Int Upper;
    public int LowerNodeId;
    public Vector3Int Lower;

    public bool IsBlockedByWater;
    public NavMeshEdge EdgeDown;
    public NavMeshEdge EdgeUp;

    public BAREdge(int upperNodeId, Vector3Int upper, int lowerNodeId, Vector3Int lower)
    {
      UpperNodeId = upperNodeId;
      Upper = upper;
      LowerNodeId = lowerNodeId;
      Lower = lower;
    }
  }
}