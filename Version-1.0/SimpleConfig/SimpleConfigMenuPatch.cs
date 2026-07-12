using HarmonyLib;
using System;
using Timberborn.CoreUI;
using UnityEngine.UIElements;

namespace Calloatti.Config
{
  public static class SimpleConfigMenuPatch
  {
    public static void Postfix(object __instance)
    {
      var rootField = AccessTools.Field(__instance.GetType(), "_root");
      if (rootField == null) return;
      var root = rootField.GetValue(__instance) as VisualElement;

      var panelStackField = AccessTools.Field(__instance.GetType(), "_panelStack");
      if (panelStackField == null) return;
      var panelStack = panelStackField.GetValue(__instance) as PanelStack;

      if (root == null || panelStack == null) return;
      if (root.Q("ModSettingsButton") != null) return;

      VisualElement scrollWrapper = root.Q<VisualElement>("ScrollViewWrapper");
      if (scrollWrapper == null || scrollWrapper.parent == null) return;

      Button bindingsButton = root.Q<Button>("BindingsButton");
      if (bindingsButton == null) return;

      Button settingsButton = (Button)Activator.CreateInstance(bindingsButton.GetType());
      settingsButton.name = "ModSettingsButton";
      settingsButton.text = "Mod Settings";

      int sheetCount = bindingsButton.styleSheets.count;
      for (int i = 0; i < sheetCount; i++)
      {
        settingsButton.styleSheets.Add(bindingsButton.styleSheets[i]);
      }

      foreach (var className in bindingsButton.GetClasses())
      {
        settingsButton.AddToClassList(className);
      }

      settingsButton.RegisterCallback<ClickEvent>(evt =>
      {
        var controller = new SimpleConfigPanel(panelStack);
        panelStack.HideAndPushOverlay(controller);
      });

      VisualElement buttonRow = new VisualElement();
      buttonRow.style.flexDirection = FlexDirection.Row;
      buttonRow.style.justifyContent = Justify.Center;
      buttonRow.style.marginTop = 15;
      buttonRow.style.marginBottom = 15;

      bindingsButton.style.marginTop = 0;
      bindingsButton.style.marginBottom = 0;
      bindingsButton.style.marginRight = 10;
      bindingsButton.style.alignSelf = Align.Auto;

      settingsButton.style.marginTop = 0;
      settingsButton.style.marginBottom = 0;
      settingsButton.style.marginLeft = 10;
      settingsButton.style.alignSelf = Align.Auto;

      buttonRow.Add(bindingsButton);
      buttonRow.Add(settingsButton);

      scrollWrapper.parent.Add(buttonRow);
    }
  }
}