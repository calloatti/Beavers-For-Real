using HarmonyLib;
using Timberborn.WalkingSystem;
using Timberborn.Navigation;
using System.Collections.Generic;
using System;
using Timberborn.GameDistricts;
using Timberborn.CharacterModelSystem;
using UnityEngine;

namespace Calloatti.BeaversForReal
{
  [HarmonyPatch(typeof(SwimmingAnimator))]
  public static class BFRSwimmingPatch
  {
    [HarmonyPatch("SmoothOffset")]
    [HarmonyPostfix]
    public static void Postfix(SwimmingAnimator __instance, float modelDepth, ref float __result)
    {
      if (modelDepth > 1f)
      {
        __result = modelDepth - 0.1f;
      }
      else
      {
        __result = __result + 0.0f;
      }
    }
  }

  [HarmonyPatch(typeof(NavMeshSourceNode))]
  public static class BFRUnblockEdgePatch
  {
    private static int GetBlockageKey(int nodeId, int groupId) => (nodeId * 397) ^ groupId;

    [HarmonyPatch(nameof(NavMeshSourceNode.UnblockEdge))]
    [HarmonyPrefix]
    public static bool UnblockPrefix(NavMeshSourceNode __instance, int nodeId, int groupId, List<int> ____blockages)
    {
      int blockageKey = GetBlockageKey(nodeId, groupId);

      if (____blockages == null || !____blockages.Contains(blockageKey))
      {
        return false;
      }
      return true;
    }
  }

  [HarmonyPatch(typeof(CitizenUnstucker), nameof(CitizenUnstucker.TryUnstuckAndKeepDistrict))]
  public static class BFRStuckPatch
  {
    public static void Postfix(Citizen citizen, DistrictCenter preferredDistrict, ref bool __result)
    {
      // RESTORED: The null check to prevent Traverse from crashing when a beaver has no district
      if (__result || preferredDistrict == null) return;

      Vector3 currentPos = citizen.Transform.position;
      Vector3Int gridPos = NavigationCoordinateSystem.WorldToGridInt(currentPos);

      var isReachableMethod = Traverse.Create(preferredDistrict).Method("IsGloballyReachableFromPosition", new System.Type[] { typeof(Vector3) });

      for (int z = 0; gridPos.z + z < 32; z++)
      {
        for (int x = -1; x <= 1; x++)
        {
          for (int y = -1; y <= 1; y++)
          {
            Vector3Int checkGrid = new Vector3Int(gridPos.x + x, gridPos.y + y, gridPos.z + z);
            Vector3 checkWorld = NavigationCoordinateSystem.GridToWorld(checkGrid);

            if (isReachableMethod.GetValue<bool>(checkWorld))
            {
              citizen.Transform.position = checkWorld;
              citizen.GetComponent<CharacterModel>().Position = checkWorld;

              // RESTORED: Condensed the Walker null check back to the inline null-conditional operator
              citizen.GetComponent<Walker>()?.StopNextTick();

              if (ModStarter.Config.GetBool("LogUnstuckBeavers"))
              {
                Debug.Log($"[BeaversForReal] Successfully unstuck citizen! Teleported from {gridPos} to {checkGrid}.");
              }

              __result = true;
              return;
            }
          }
        }
      }
    }
  }
}