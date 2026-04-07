using HarmonyLib;
using Timberborn.WalkingSystem;

namespace Calloatti.BeaversForReal
{
  [HarmonyPatch(typeof(SwimmingAnimator))]
  public static class BVRSwimmingPatch
  {
    [HarmonyPatch("SmoothOffset")]
    [HarmonyPostfix] // Changed to Postfix so vanilla math happens FIRST
    public static void Postfix(SwimmingAnimator __instance, float modelDepth, ref float __result)
    {
      // If the water is deeper than 1 block...
      if (modelDepth > 1f)
      {
        // ...we overwrite the vanilla result entirely to keep them at the surface.
        __result = modelDepth - 0.1f;
      }
      else
      {
        // Let vanilla calculate its shallow water depth first
        __result = __result + 0.0f;
      }
    }
  }
}