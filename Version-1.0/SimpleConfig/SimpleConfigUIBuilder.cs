using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Timberborn.CoreUI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Calloatti.Config
{
  public static class SimpleConfigUIBuilder
  {
    public static VisualElement BuildConfigurationOverlay(Action onClose)
    {
      // ====================================================================
      // === SECTION 1: MODAL & WINDOW SETUP ================================
      // ====================================================================

      // Core layout size properties
      float textColWidth = 384f;
      float controlColWidth = 256f;
      float buttonColWidth = 118f;

      // Mirroring the calculation setup from SyncModsPro table structure
      float calculatedTotalWidth = textColWidth + controlColWidth + buttonColWidth; // 900f
      float panelWidth = calculatedTotalWidth + 100f;                              // 1000f

      var modalBackground = new VisualElement();
      modalBackground.name = "SimpleConfigModalOverlay";
      modalBackground.style.position = Position.Absolute;
      modalBackground.style.top = 0;
      modalBackground.style.bottom = 0;
      modalBackground.style.left = 0;
      modalBackground.style.right = 0;
      modalBackground.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.90f));
      modalBackground.style.alignItems = Align.Center;
      modalBackground.style.justifyContent = Justify.Center;

      var mainWindow = new NineSliceVisualElement();
      mainWindow.AddToClassList("content-centered");
      mainWindow.AddToClassList("sliced-border");
      mainWindow.AddToClassList("sliced-border--nontransparent");

      mainWindow.style.width = panelWidth;
      mainWindow.style.maxWidth = StyleKeyword.None;

      var headerBackground = new NineSliceVisualElement();
      headerBackground.AddToClassList("capsule-header");
      headerBackground.style.justifyContent = Justify.Center;
      headerBackground.style.alignItems = Align.Center;
      headerBackground.style.top = -10;

      var title = new Label("Mod Settings");
      title.AddToClassList("capsule-header__text");
      title.style.unityTextAlign = TextAnchor.MiddleCenter;
      title.style.top = -2;
      headerBackground.Add(title);
      mainWindow.Add(headerBackground);

      var windowBox = new VisualElement();
      windowBox.AddToClassList("box");
      windowBox.style.width = panelWidth;
      windowBox.style.maxWidth = StyleKeyword.None;
      windowBox.style.height = 750;
      windowBox.style.paddingTop = 45f;
      windowBox.style.paddingBottom = 45f;
      windowBox.style.paddingLeft = 65f;
      windowBox.style.paddingRight = 45f;

      var scrollView = CreateScrollView();
      windowBox.Add(scrollView);
      mainWindow.Add(windowBox);

      var closeButton = new Button();
      closeButton.AddToClassList("close-button");
      closeButton.RegisterCallback<ClickEvent>(evt =>
      {
        onClose?.Invoke();
      });
      mainWindow.Add(closeButton);

      // ====================================================================
      // === SECTION 2: MOD ITERATION & STATE SETUP =========================
      // ====================================================================
      if (SimpleConfigUIRegistry.ActiveModRepository != null)
      {
        var sortedMods = SimpleConfigUIRegistry.ActiveModRepository.EnabledMods
                            .OrderBy(m => m.DisplayName)
                            .ToList();

        foreach (var mod in sortedMods)
        {
          string path = mod.ModDirectory.Path;
          string schemaPath = Path.Combine(path, "SimpleConfig.txt");

          if (!File.Exists(schemaPath)) continue;

          var localConfig = new SimpleConfig(path);
          SimpleConfigSchema schema = localConfig.LoadSchema();

          if (schema == null || !schema.Settings.Any()) continue;

          var pendingChanges = new Dictionary<string, object>();

          // Native Unity Foldout control to wrap the settings
          var foldout = new Foldout();
          foldout.text = mod.DisplayName;
          foldout.value = false; // Default to collapsed
          foldout.style.marginBottom = 15;

          var foldoutToggle = foldout.Q<Toggle>();
          if (foldoutToggle != null)
          {
            var foldoutLabel = foldoutToggle.Q<Label>();
            if (foldoutLabel != null) foldoutLabel.AddToClassList("text--big");
          }

          var saveButton = new Button();
          saveButton.text = "Save Changes";
          saveButton.style.paddingTop = 8;
          saveButton.style.paddingBottom = 8;
          saveButton.style.paddingLeft = 40;
          saveButton.style.paddingRight = 40;
          saveButton.style.fontSize = 14;
          saveButton.style.unityFontStyleAndWeight = FontStyle.Bold;
          saveButton.style.borderTopWidth = 1;
          saveButton.style.borderBottomWidth = 1;
          saveButton.style.borderLeftWidth = 1;
          saveButton.style.borderRightWidth = 1;
          saveButton.style.borderTopLeftRadius = 4;
          saveButton.style.borderTopRightRadius = 4;
          saveButton.style.borderBottomLeftRadius = 4;
          saveButton.style.borderBottomRightRadius = 4;
          saveButton.style.color = new StyleColor(Color.white);

          Action updateDirtyState = () =>
          {
            bool isDirty = pendingChanges.Count > 0;
            if (isDirty)
            {
              saveButton.style.backgroundColor = new StyleColor(new Color(0.65f, 0.15f, 0.15f, 1f));
              saveButton.style.borderTopColor = new StyleColor(new Color(0.8f, 0.3f, 0.3f, 1f));
              saveButton.style.borderBottomColor = new StyleColor(new Color(0.8f, 0.3f, 0.3f, 1f));
              saveButton.style.borderLeftColor = new StyleColor(new Color(0.8f, 0.3f, 0.3f, 1f));
              saveButton.style.borderRightColor = new StyleColor(new Color(0.8f, 0.3f, 0.3f, 1f));
            }
            else
            {
              saveButton.style.backgroundColor = new StyleColor(new Color(0.15f, 0.35f, 0.15f, 1f));
              saveButton.style.borderTopColor = new StyleColor(new Color(0.3f, 0.5f, 0.3f, 1f));
              saveButton.style.borderBottomColor = new StyleColor(new Color(0.3f, 0.5f, 0.3f, 1f));
              saveButton.style.borderLeftColor = new StyleColor(new Color(0.3f, 0.5f, 0.3f, 1f));
              saveButton.style.borderRightColor = new StyleColor(new Color(0.3f, 0.5f, 0.3f, 1f));
            }
          };

          // Initialize baseline colors
          updateDirtyState();

          saveButton.RegisterCallback<MouseEnterEvent>(evt => {
            bool isDirty = pendingChanges.Count > 0;
            saveButton.style.backgroundColor = new StyleColor(isDirty ? new Color(0.75f, 0.2f, 0.2f, 1f) : new Color(0.2f, 0.45f, 0.2f, 1f));
          });
          saveButton.RegisterCallback<MouseLeaveEvent>(evt => {
            bool isDirty = pendingChanges.Count > 0;
            saveButton.style.backgroundColor = new StyleColor(isDirty ? new Color(0.65f, 0.15f, 0.15f, 1f) : new Color(0.15f, 0.35f, 0.15f, 1f));
          });

          saveButton.RegisterCallback<ClickEvent>(evt =>
          {
            foreach (var kvp in pendingChanges)
            {
              if (kvp.Value is bool b) localConfig.Set(kvp.Key, b);
              else if (kvp.Value is int i) localConfig.Set(kvp.Key, i);
              else if (kvp.Value is float f) localConfig.Set(kvp.Key, f);
              else localConfig.Set(kvp.Key, kvp.Value?.ToString());
            }
            localConfig.Save();
            pendingChanges.Clear();
            updateDirtyState();

            saveButton.text = "Saved!";
            saveButton.schedule.Execute(() => saveButton.text = "Save Changes").StartingIn(1500);
          });

          // ====================================================================
          // === SECTION 3: ROW GENERATION & ZEBRA STRIPING =====================
          // ====================================================================
          int rowIndex = 0;

          foreach (var entry in schema.Settings)
          {
            if (string.IsNullOrWhiteSpace(entry.Key)) continue;

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.FlexStart;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 8;
            row.style.paddingBottom = 8;
            row.style.paddingLeft = 10;
            row.style.paddingRight = 10;

            // Two-color zebra striping: Slightly brighter (white overlay) and slightly darker (black overlay)
            if (rowIndex % 2 == 0)
            {
              row.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.05f));
            }
            else
            {
              row.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.15f));
            }

            // ====================================================================
            // === SECTION 4: COLUMN 1 - TEXT STACK (LEFT) ========================
            // ====================================================================
            var textContainer = new VisualElement();
            textContainer.style.flexDirection = FlexDirection.Column;
            textContainer.style.justifyContent = Justify.Center;
            textContainer.style.width = textColWidth;
            textContainer.style.minWidth = textColWidth;
            textContainer.style.maxWidth = textColWidth;
            textContainer.style.flexShrink = 0;
            textContainer.style.paddingRight = 15;

            string cleanLabel = string.IsNullOrWhiteSpace(entry.Label) ? entry.Key : entry.Label;
            var keyLabel = new Label(cleanLabel);
            keyLabel.AddToClassList("text--default");
            keyLabel.style.unityFontStyleAndWeight = FontStyle.Normal;

            var tooltipLabel = new Label(entry.Tooltip ?? "");
            tooltipLabel.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f));
            tooltipLabel.style.fontSize = 12;
            tooltipLabel.style.whiteSpace = WhiteSpace.Normal;

            var rawKeyDisplayLabel = new Label($"Key: {entry.Key}");
            rawKeyDisplayLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            rawKeyDisplayLabel.style.fontSize = 12;
            rawKeyDisplayLabel.style.marginTop = 2;

            var defaultLabel = new Label($"Default: {entry.DefaultValue ?? "None"}");
            defaultLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            defaultLabel.style.fontSize = 12;
            defaultLabel.style.marginTop = 2;

            textContainer.Add(keyLabel);
            if (!string.IsNullOrWhiteSpace(entry.Tooltip)) textContainer.Add(tooltipLabel);
            textContainer.Add(rawKeyDisplayLabel);
            textContainer.Add(defaultLabel);

            if (entry.RequiresRestart || entry.RequiresReload)
            {
              var indicatorLabel = new Label(entry.RequiresRestart ? "[Requires Restart]" : "[Requires Reload]");
              indicatorLabel.style.fontSize = 12;
              indicatorLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
              indicatorLabel.style.marginTop = 2;
              indicatorLabel.style.color = new StyleColor(entry.RequiresRestart ? new Color(0.85f, 0.35f, 0.35f) : new Color(0.85f, 0.55f, 0.25f));
              textContainer.Add(indicatorLabel);
            }

            // ====================================================================
            // === SECTION 5: COLUMN 2 - CONTROLS (MIDDLE) ========================
            // ====================================================================
            var controlContainer = new VisualElement { style = { width = controlColWidth, minWidth = controlColWidth, maxWidth = controlColWidth, flexShrink = 0, justifyContent = Justify.Center, paddingRight = 15 } };
            VisualElement control = null;
            Action resetToDefaultAction = null;

            string controlType = entry.ControlType ?? "TextField";
            switch (controlType.ToLowerInvariant())
            {
              case "toggle":
                var toggle = new Toggle { value = localConfig.GetBool(entry.Key) };
                toggle.AddToClassList("game-toggle");
                toggle.RegisterValueChangedCallback(evt => {
                  if (evt.newValue == localConfig.GetBool(entry.Key)) pendingChanges.Remove(entry.Key);
                  else pendingChanges[entry.Key] = evt.newValue;
                  updateDirtyState();
                });
                resetToDefaultAction = () => toggle.value = bool.TryParse(entry.DefaultValue?.ToString(), out bool b) && b;
                control = toggle;
                break;

              case "slider":
                bool isInt = entry.Type != null && entry.Type.Equals("int", StringComparison.OrdinalIgnoreCase);
                float initialValue = isInt ? localConfig.GetInt(entry.Key) : localConfig.GetFloat(entry.Key);

                float baseValueForBounds = initialValue;
                if (entry.DefaultValue != null && float.TryParse(entry.DefaultValue.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float parsedDef))
                {
                  baseValueForBounds = parsedDef;
                }

                float min = entry.MinValue ?? (baseValueForBounds - 10f);
                float max = entry.MaxValue ?? (baseValueForBounds + 10f);
                float step = entry.Step ?? (isInt ? 1f : 0.1f);

                var stepperWrapper = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, width = Length.Percent(100) } };
                var inputField = new NineSliceTextField();
                inputField.AddToClassList("text-field");
                inputField.style.flexGrow = 1;
                inputField.style.marginRight = 5;
                inputField.value = isInt ? Mathf.RoundToInt(initialValue).ToString() : initialValue.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

                var btnMinus = new Button();
                btnMinus.AddToClassList("button-square");
                btnMinus.AddToClassList("button-minus");
                btnMinus.AddToClassList("button-square--small");
                btnMinus.style.marginRight = 2;

                var btnPlus = new Button();
                btnPlus.AddToClassList("button-square");
                btnPlus.AddToClassList("button-plus");
                btnPlus.AddToClassList("button-square--small");

                Action<float> applyValue = (newValue) =>
                {
                  newValue = Mathf.Clamp(newValue, min, max);
                  if (isInt)
                  {
                    int intStep = Mathf.Max(1, Mathf.RoundToInt(step));
                    int intVal = Mathf.RoundToInt(newValue);
                    if (intStep > 1) intVal = Mathf.RoundToInt((float)intVal / intStep) * intStep;
                    inputField.SetValueWithoutNotify(intVal.ToString());

                    if (intVal == localConfig.GetInt(entry.Key)) pendingChanges.Remove(entry.Key);
                    else pendingChanges[entry.Key] = intVal;
                  }
                  else
                  {
                    if (step > 0f) newValue = Mathf.Round(newValue / step) * step;
                    inputField.SetValueWithoutNotify(newValue.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));

                    if (Mathf.Approximately(newValue, localConfig.GetFloat(entry.Key))) pendingChanges.Remove(entry.Key);
                    else pendingChanges[entry.Key] = newValue;
                  }
                  updateDirtyState();
                };

                Func<float> getCurrentValue = () =>
                {
                  if (pendingChanges.TryGetValue(entry.Key, out object pending)) return Convert.ToSingle(pending);
                  return isInt ? localConfig.GetInt(entry.Key) : localConfig.GetFloat(entry.Key);
                };

                btnMinus.RegisterCallback<ClickEvent>(evt => applyValue(getCurrentValue() - step));
                btnPlus.RegisterCallback<ClickEvent>(evt => applyValue(getCurrentValue() + step));

                inputField.RegisterValueChangedCallback(evt =>
                {
                  if (float.TryParse(evt.newValue.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float parsed))
                  {
                    if (isInt)
                    {
                      int parsedInt = Mathf.RoundToInt(parsed);
                      if (parsedInt == localConfig.GetInt(entry.Key)) pendingChanges.Remove(entry.Key);
                      else pendingChanges[entry.Key] = parsedInt;
                    }
                    else
                    {
                      if (Mathf.Approximately(parsed, localConfig.GetFloat(entry.Key))) pendingChanges.Remove(entry.Key);
                      else pendingChanges[entry.Key] = parsed;
                    }
                    updateDirtyState();
                  }
                });

                inputField.RegisterCallback<FocusOutEvent>(evt => applyValue(getCurrentValue()));

                resetToDefaultAction = () =>
                {
                  float def = float.TryParse(entry.DefaultValue?.ToString(), out float f) ? f : min;
                  applyValue(def);
                };

                stepperWrapper.Add(inputField);
                stepperWrapper.Add(btnMinus);
                stepperWrapper.Add(btnPlus);

                control = stepperWrapper;
                break;

              case "dropdown":
                var options = entry.Options ?? new List<string>();
                var dependencies = SimpleConfigUIDependencies.Instance;
                if (dependencies == null) break;

                var dropdown = new Timberborn.DropdownSystem.Dropdown();
                dropdown.Initialize(dependencies.DropdownListDrawer);

                var provider = new SimpleConfigDropdownProvider(
                  options,
                  getter: () => localConfig.GetString(entry.Key),
                  setter: (val) =>
                  {
                    if (val == localConfig.GetString(entry.Key)) pendingChanges.Remove(entry.Key);
                    else pendingChanges[entry.Key] = val;
                    updateDirtyState();
                  }
                );

                dependencies.DropdownItemsSetter.SetItems(dropdown, provider);

                resetToDefaultAction = () =>
                {
                  string def = entry.DefaultValue?.ToString();
                  if (def != null && options.Contains(def))
                  {
                    provider.SetValue(def);
                    dropdown.UpdateSelectedValue();
                  }
                };

                dropdown.style.width = Length.Percent(100);
                var labelElement = dropdown.Q<Label>("Label");
                if (labelElement != null) labelElement.style.display = DisplayStyle.None;

                var selectionButton = dropdown.Q<Button>("Selection");
                if (selectionButton != null)
                {
                  selectionButton.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                  selectionButton.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f, 1f));
                  selectionButton.style.borderTopWidth = 1;
                  selectionButton.style.borderBottomWidth = 1;
                  selectionButton.style.borderLeftWidth = 1;
                  selectionButton.style.borderRightWidth = 1;
                  selectionButton.style.borderTopColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f, 1f));
                  selectionButton.style.borderBottomColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f, 1f));
                  selectionButton.style.borderLeftColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f, 1f));
                  selectionButton.style.borderRightColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f, 1f));
                  selectionButton.style.borderTopLeftRadius = 3;
                  selectionButton.style.borderTopRightRadius = 3;
                  selectionButton.style.borderBottomLeftRadius = 3;
                  selectionButton.style.borderBottomRightRadius = 3;
                  selectionButton.style.minHeight = 28;
                }

                control = dropdown;
                break;

              case "textfield":
              default:
                var field = new NineSliceTextField { value = localConfig.GetString(entry.Key) };
                field.AddToClassList("text-field");
                field.RegisterValueChangedCallback(evt => {
                  if (evt.newValue == localConfig.GetString(entry.Key)) pendingChanges.Remove(entry.Key);
                  else pendingChanges[entry.Key] = evt.newValue;
                  updateDirtyState();
                });
                resetToDefaultAction = () => field.value = entry.DefaultValue?.ToString();
                control = field;
                break;
            }

            if (control != null) controlContainer.Add(control);

            // ====================================================================
            // === SECTION 6: COLUMN 3 - DEFAULT BUTTON (RIGHT) ===================
            // ====================================================================
            var buttonContainer = new VisualElement { style = { width = buttonColWidth, minWidth = buttonColWidth, maxWidth = buttonColWidth, flexShrink = 0, alignItems = Align.FlexStart, justifyContent = Justify.Center } };

            var defaultButton = new Button();
            defaultButton.text = "Default";
            defaultButton.style.paddingTop = 4;
            defaultButton.style.paddingBottom = 4;
            defaultButton.style.paddingLeft = 8;
            defaultButton.style.paddingRight = 8;
            defaultButton.style.height = 26;
            defaultButton.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f, 1f));
            defaultButton.style.borderTopWidth = 1;
            defaultButton.style.borderBottomWidth = 1;
            defaultButton.style.borderLeftWidth = 1;
            defaultButton.style.borderRightWidth = 1;
            defaultButton.style.borderTopColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f, 1f));
            defaultButton.style.borderBottomColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f, 1f));
            defaultButton.style.borderLeftColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f, 1f));
            defaultButton.style.borderRightColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f, 1f));
            defaultButton.style.borderTopLeftRadius = 3;
            defaultButton.style.borderTopRightRadius = 3;
            defaultButton.style.borderBottomLeftRadius = 3;
            defaultButton.style.borderBottomRightRadius = 3;
            defaultButton.style.color = new StyleColor(Color.white);

            defaultButton.RegisterCallback<ClickEvent>(evt => resetToDefaultAction?.Invoke());
            defaultButton.RegisterCallback<MouseEnterEvent>(evt => defaultButton.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f, 1f)));
            defaultButton.RegisterCallback<MouseLeaveEvent>(evt => defaultButton.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f, 1f)));

            buttonContainer.Add(defaultButton);

            // ====================================================================
            // === SECTION 7: ROW & SAVE BUTTON ASSEMBLY ==========================
            // ====================================================================
            row.Add(textContainer);
            row.Add(controlContainer);
            row.Add(buttonContainer);

            foldout.Add(row);
            rowIndex++;
          }

          var saveWrapper = new VisualElement();
          saveWrapper.style.flexDirection = FlexDirection.Row;
          saveWrapper.style.justifyContent = Justify.Center;
          saveWrapper.style.alignItems = Align.Center;

          // Match the exact padding of the property rows to maintain height
          saveWrapper.style.paddingTop = 24;
          saveWrapper.style.paddingBottom = 24;
          saveWrapper.style.paddingLeft = 10;
          saveWrapper.style.paddingRight = 10;

          // Continue the zebra striping pattern for the save row
          if (rowIndex % 2 == 0)
          {
            saveWrapper.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.05f));
          }
          else
          {
            saveWrapper.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.15f));
          }

          saveWrapper.Add(saveButton);

          foldout.Add(saveWrapper);
          scrollView.Add(foldout);
        }
      }

      modalBackground.Add(mainWindow);
      return modalBackground;
    }

    // ====================================================================
    // === SECTION 8: SCROLL VIEW CREATION ================================
    // ====================================================================
    private static ScrollView CreateScrollView()
    {
      ScrollView scrollView = new ScrollView();
      scrollView.style.flexGrow = 1;
      scrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
      scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

      var dragger = scrollView.Q<VisualElement>(className: "unity-base-slider__dragger");
      if (dragger != null)
      {
        dragger.style.width = 20;
        dragger.style.minHeight = 58;
        dragger.style.backgroundColor = Color.clear;
        dragger.style.borderTopWidth = dragger.style.borderBottomWidth = dragger.style.borderLeftWidth = dragger.style.borderRightWidth = 0;
        var tex = Resources.Load<Texture2D>("UI/Images/Core/vertical-scroll-button-nine-slice");
        if (tex != null)
        {
          dragger.style.backgroundImage = new StyleBackground(tex);
          dragger.style.unitySliceTop = dragger.style.unitySliceBottom = dragger.style.unitySliceLeft = dragger.style.unitySliceRight = 14;
        }
      }

      var tracker = scrollView.Q<VisualElement>(className: "unity-base-slider__tracker");
      if (tracker != null)
      {
        tracker.style.width = 20;
        tracker.style.backgroundColor = Color.clear;
        tracker.style.borderTopWidth = tracker.style.borderBottomWidth = tracker.style.borderLeftWidth = tracker.style.borderRightWidth = 0;
        var tex = Resources.Load<Texture2D>("UI/Images/Core/vertical-scroll-bar-nine-slice");
        if (tex != null)
        {
          tracker.style.backgroundImage = new StyleBackground(tex);
          tracker.style.unitySliceTop = tracker.style.unitySliceBottom = 16;
        }
      }

      return scrollView;
    }
  }
}