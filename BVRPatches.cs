using HarmonyLib;
using Timberborn.Navigation;

namespace Calloatti.BeaversForReal
{
  [HarmonyPatch]
  public static class BVRPatches
  {
    [HarmonyPatch(typeof(NavMeshSource), nameof(NavMeshSource.BlockEdge))]
    [HarmonyPrefix]
    public static bool BlockEdge_Prefix(int startNodeId, int endNodeId, int groupId) => groupId != BVRShorelineManager.ShorelineGroupId;

    [HarmonyPatch(typeof(NavMeshSource), nameof(NavMeshSource.UnblockEdge))]
    [HarmonyPrefix]
    public static bool UnblockEdge_Prefix(int startNodeId, int endNodeId, int groupId) => groupId != BVRShorelineManager.ShorelineGroupId;
  }
}