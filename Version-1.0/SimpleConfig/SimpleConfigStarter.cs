using HarmonyLib;
using System;
using System.Reflection;
using Timberborn.Modding;
using Timberborn.ModManagerScene;
using UnityEngine;

namespace Calloatti.Config
{
  public class SimpleConfigStarter : IModStarter
  {
    public void StartMod(IModEnvironment modEnvironment)
    {
      lock (AppDomain.CurrentDomain)
      {
        if (AppDomain.CurrentDomain.GetData("SimpleConfigUiPatched") is bool completelyPatched && completelyPatched)
        {
          return;
        }

        try
        {
          var harmony = new Harmony("com.calloatti.simpleconfig.sharedui");

          Type targetType = AccessTools.TypeByName("Timberborn.SettingsSystemUI.SettingsBox");
          if (targetType == null)
          {
            Debug.LogWarning("[SimpleConfigUI] Could not find internal type SettingsBox.");
            return;
          }

          var targetMethod = targetType.GetMethod("Load", BindingFlags.Instance | BindingFlags.Public);
          var postfixMethod = typeof(SimpleConfigMenuPatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

          if (targetMethod != null && postfixMethod != null)
          {
            harmony.Patch(targetMethod, postfix: new HarmonyMethod(postfixMethod));
            AppDomain.CurrentDomain.SetData("SimpleConfigUiPatched", true);
            Debug.Log("[SimpleConfigUI] Successfully injected unified Mod Settings button into SettingsBox.");
          }
        }
        catch (Exception ex)
        {
          Debug.LogError($"[SimpleConfigUI] Failed to apply runtime mod menu hook: {ex}");
        }
      }
    }
  }
}