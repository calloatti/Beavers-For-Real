using Timberborn.Navigation;
using UnityEngine;

namespace Calloatti.BeaversForReal
{
  public class BVRShorelineEdge
  {
    public Vector3Int Upper;
    public Vector3Int Lower;
    public bool IsActive;
    public bool IsBlockedByBuilding;
    public NavMeshEdge EdgeDown;
    public NavMeshEdge EdgeUp;

    public BVRShorelineEdge(Vector3Int upper, Vector3Int lower)
    {
      Upper = upper;
      Lower = lower;
    }
  }
}