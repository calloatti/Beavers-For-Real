using System;
using System.Collections.Generic;
using System.Globalization;
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
      // --- DEBUG FLAG ---
      bool showDebugBorders = false; // Set to false to hide the red layout borders

      // --- FIXED OUTER WINDOW DIMENSIONS (Preserved from out to in) ---
      float panelWidth = 868f; // Outer canvas frame width
      float calculatedTotalWidth = 768f; // Available content width (panelWidth - 100f)

      // --- CONFIGURABLE CONTROL DIMENSIONS ---
      float controlColWidth = 16*18f; // Configurable width for Column 2 controls
      float columnGap = 10f; // Gap between Column 1 and Column 2

      // --- CONFIGURABLE SPACING DIMENSIONS ---
      float metaItemGap = 8f; // Configurable space between Current, Default, Min, and Max items

      // --- INFERRED COLUMN 1 WIDTH (Calculated for reference) ---
      float estimatedTextColWidth = calculatedTotalWidth - controlColWidth - columnGap; // Evaluates to 502f

      // Grab ILoc from our Singleton safely
      var loc = SimpleConfigUIDependencies.Instance?.Loc;

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

      var title = new Label(loc != null ? loc.T("Calloatti.SimpleConfig.ModSettings") : "Mod Settings");
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

      // --- DEBUG HELPER ACTION ---
      Action<VisualElement> addDebugBorder = (element) =>
      {
        if (showDebugBorders)
        {
          element.style.borderTopColor = new StyleColor(Color.red);
          element.style.borderBottomColor = new StyleColor(Color.red);
          element.style.borderLeftColor = new StyleColor(Color.red);
          element.style.borderRightColor = new StyleColor(Color.red);
          element.style.borderTopWidth = 1f;
          element.style.borderBottomWidth = 1f;
          element.style.borderLeftWidth = 1f;
          element.style.borderRightWidth = 1f;
        }
      };

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

          var pendingChanges = new Dictionary<string, string>();
          var validationUIUpdaters = new Dictionary<string, Action<bool>>();

          var foldout = new Foldout();
          foldout.text = mod.DisplayName;
          foldout.value = false;
          foldout.style.marginBottom = 15;

          var foldoutToggle = foldout.Q<Toggle>();
          if (foldoutToggle != null)
          {
            var foldoutLabel = foldoutToggle.Q<Label>();
            if (foldoutLabel != null) foldoutLabel.AddToClassList("text--big");

            foldoutToggle.style.marginBottom = 10;
          }

          var saveButton = new Button();
          saveButton.text = loc != null ? loc.T("Calloatti.SimpleConfig.SaveChanges") : "Save Changes";
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
            bool hasErrors = false;

            foreach (var kvp in pendingChanges)
            {
              var entry = schema.Settings.FirstOrDefault(s => s.Key.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase));
              if (entry != null)
              {
                var valCode = entry.Validate(kvp.Value);
                if (valCode != ValidationCode.Valid)
                {
                  hasErrors = true;
                  if (validationUIUpdaters.TryGetValue(kvp.Key, out var markError)) markError(true);
                }
                else
                {
                  if (validationUIUpdaters.TryGetValue(kvp.Key, out var markError)) markError(false);
                }
              }
            }

            if (hasErrors)
            {
              saveButton.text = loc != null ? loc.T("Calloatti.SimpleConfig.ValidationFailed") : "Validation Failed!";
              saveButton.schedule.Execute(() => saveButton.text = loc != null ? loc.T("Calloatti.SimpleConfig.SaveChanges") : "Save Changes").StartingIn(1500);
              return;
            }

            foreach (var kvp in pendingChanges)
            {
              localConfig.InsertOrUpdate(kvp.Key, kvp.Value);
            }

            localConfig.Save();
            pendingChanges.Clear();
            updateDirtyState();

            saveButton.text = loc != null ? loc.T("Calloatti.SimpleConfig.Saved") : "Saved!";
            saveButton.schedule.Execute(() => saveButton.text = loc != null ? loc.T("Calloatti.SimpleConfig.SaveChanges") : "Save Changes").StartingIn(1500);
          });

          var sortedEntries = schema.Settings
              .Where(e => e.IsDefined)
              .Concat(schema.Settings.Where(e => !e.IsDefined).OrderBy(e => {
                string labelForSort = !string.IsNullOrEmpty(e.Label) ? e.Label : e.Key;
                return loc != null ? loc.T(labelForSort) : labelForSort;
              }))
              .ToList();

          int rowIndex = 0;

          foreach (var entry in sortedEntries)
          {
            if (string.IsNullOrWhiteSpace(entry.Key)) continue;

            var row = new VisualElement();
            row.style.width = Length.Percent(100);
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.FlexStart;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 8;
            row.style.paddingBottom = 8;
            row.style.paddingLeft = 10;
            row.style.paddingRight = 10; // Ensures 10f padding on the outer right edge of the row

            Color baseRowColor = (rowIndex % 2 == 0) ? new Color(1f, 1f, 1f, 0.05f) : new Color(0f, 0f, 0f, 0.15f);
            row.style.backgroundColor = new StyleColor(baseRowColor);

            validationUIUpdaters[entry.Key] = (isError) => {
              row.style.backgroundColor = new StyleColor(isError ? new Color(0.6f, 0.1f, 0.1f, 0.35f) : baseRowColor);
            };

            var textContainer = new VisualElement();
            textContainer.style.flexDirection = FlexDirection.Column;
            textContainer.style.justifyContent = Justify.Center;
            textContainer.style.flexGrow = 1;          // Grows to fill all available space
            textContainer.style.flexShrink = 1;        // Soft shrink constraint to fit container
            textContainer.style.marginRight = columnGap; // Exact gap pushing Column 2 to the right
            addDebugBorder(textContainer);

            string finalLabel = loc != null ? loc.T(entry.Label) : entry.Label;
            var keyLabel = new Label(finalLabel);
            keyLabel.AddToClassList("text--default");
            keyLabel.style.unityFontStyleAndWeight = FontStyle.Normal;

            string rawTooltip = entry.Tooltip ?? "";
            string finalTooltip = (loc != null && !string.IsNullOrWhiteSpace(rawTooltip)) ? loc.T(rawTooltip) : rawTooltip;
            var tooltipLabel = new Label(finalTooltip);
            tooltipLabel.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f));
            tooltipLabel.style.fontSize = 12;
            tooltipLabel.style.whiteSpace = WhiteSpace.Normal;

            string keyStr = loc != null ? loc.T("Calloatti.SimpleConfig.Key") : "Key:";
            var rawKeyDisplayLabel = new Label($"{keyStr} {entry.Key}");
            rawKeyDisplayLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            rawKeyDisplayLabel.style.fontSize = 12;
            rawKeyDisplayLabel.style.marginTop = 2;

            textContainer.Add(keyLabel);
            if (!string.IsNullOrWhiteSpace(entry.Tooltip)) textContainer.Add(tooltipLabel);
            textContainer.Add(rawKeyDisplayLabel);

            // --- MUTABLE VALUE RESTORATION DELEGATES ---
            Action resetToDefaultAction = null;
            Action resetToSavedAction = null;

            // --- LEFT-ALIGNED INLINE METADATA ROW (CURRENT / DEFAULT / MIN / MAX) ---
            var metaRow = new VisualElement();
            metaRow.style.flexDirection = FlexDirection.Row;
            metaRow.style.marginTop = 2;

            string currentTextStr = loc != null ? loc.T("Calloatti.SimpleConfig.CurrentValue") : "Current:";
            var currentLabel = new Label($"[{currentTextStr} {entry.ValueString}]");
            currentLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            currentLabel.style.fontSize = 12;
            currentLabel.style.marginRight = metaItemGap;
            currentLabel.RegisterCallback<MouseEnterEvent>(evt => currentLabel.style.color = new StyleColor(new Color(0.85f, 0.65f, 0.15f)));
            currentLabel.RegisterCallback<MouseLeaveEvent>(evt => currentLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f)));
            currentLabel.RegisterCallback<ClickEvent>(evt => resetToSavedAction?.Invoke());

            string defStr = loc != null ? loc.T("Calloatti.SimpleConfig.Default") : "Default:";
            string noneStr = loc != null ? loc.T("Calloatti.SimpleConfig.None") : "None";
            var defaultLabel = new Label($"[{defStr} {entry.DefaultValue ?? noneStr}]");
            defaultLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            defaultLabel.style.fontSize = 12;
            defaultLabel.style.marginRight = metaItemGap;
            defaultLabel.RegisterCallback<MouseEnterEvent>(evt => defaultLabel.style.color = new StyleColor(new Color(0.85f, 0.65f, 0.15f)));
            defaultLabel.RegisterCallback<MouseLeaveEvent>(evt => defaultLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f)));
            defaultLabel.RegisterCallback<ClickEvent>(evt => resetToDefaultAction?.Invoke());

            metaRow.Add(currentLabel);
            metaRow.Add(defaultLabel);

            bool isNumeric = string.Equals(entry.Type, "int", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(entry.Type, "float", StringComparison.OrdinalIgnoreCase);

            if (isNumeric)
            {
              string minStr = loc != null ? loc.T("Calloatti.SimpleConfig.Min") : "Min:";
              string maxStr = loc != null ? loc.T("Calloatti.SimpleConfig.Max") : "Max:";

              if (entry.MinValue.HasValue)
              {
                var minLabel = new Label($"{minStr} {entry.MinValue.Value.ToString(CultureInfo.InvariantCulture)}");
                minLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
                minLabel.style.fontSize = 12;
                minLabel.style.marginRight = metaItemGap;
                metaRow.Add(minLabel);
              }
              if (entry.MaxValue.HasValue)
              {
                var maxLabel = new Label($"{maxStr} {entry.MaxValue.Value.ToString(CultureInfo.InvariantCulture)}");
                maxLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
                maxLabel.style.fontSize = 12;
                metaRow.Add(maxLabel);
              }
            }
            textContainer.Add(metaRow);

            // --- ENVIRONMENT REQUIREMENTS NOTIFICATION ROW ---
            if (entry.RequiresRestart || entry.RequiresReload)
            {
              var requirementRow = new VisualElement();
              requirementRow.style.flexDirection = FlexDirection.Row;
              requirementRow.style.marginTop = 2;

              if (entry.RequiresRestart)
              {
                string restartStr = loc != null ? loc.T("Calloatti.SimpleConfig.RequiresRestart") : "[Requires Restart]";
                var restartLabel = new Label(restartStr);
                restartLabel.style.fontSize = 12;
                restartLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
                restartLabel.style.color = new StyleColor(new Color(0.85f, 0.35f, 0.35f));
                restartLabel.style.marginRight = 6;
                requirementRow.Add(restartLabel);
              }

              if (entry.RequiresReload)
              {
                string reloadStr = loc != null ? loc.T("Calloatti.SimpleConfig.RequiresReload") : "[Requires Reload]";
                var reloadLabel = new Label(reloadStr);
                reloadLabel.style.fontSize = 12;
                reloadLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
                reloadLabel.style.color = new StyleColor(new Color(0.85f, 0.55f, 0.25f));
                requirementRow.Add(reloadLabel);
              }
              textContainer.Add(requirementRow);
            }

            var controlContainer = new VisualElement();
            controlContainer.style.width = controlColWidth;
            controlContainer.style.minWidth = controlColWidth;
            controlContainer.style.maxWidth = controlColWidth;
            controlContainer.style.flexGrow = 0;
            controlContainer.style.flexShrink = 0;
            controlContainer.style.justifyContent = Justify.Center;
            addDebugBorder(controlContainer);
            VisualElement control = null;

            string controlType = entry.ControlType ?? "TextField";
            switch (controlType.ToLowerInvariant())
            {
              case "toggle":
                var toggle = new Toggle { value = entry.ValueBool };
                toggle.AddToClassList("game-toggle");
                toggle.RegisterValueChangedCallback(evt => {
                  string val = evt.newValue.ToString().ToLowerInvariant();
                  if (val == entry.ValueString) pendingChanges.Remove(entry.Key);
                  else pendingChanges[entry.Key] = val;
                  updateDirtyState();
                });
                resetToDefaultAction = () => toggle.value = bool.TryParse(entry.DefaultValue?.ToString(), out bool b) && b;
                resetToSavedAction = () => toggle.value = entry.ValueBool;
                control = toggle;
                break;

              case "slider":
                bool isInt = entry.Type != null && entry.Type.Equals("int", StringComparison.OrdinalIgnoreCase);
                float step = entry.Step ?? (isInt ? 1f : 0.1f);

                var stepperWrapper = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, width = Length.Percent(100) } };
                var inputField = new NineSliceTextField();
                inputField.AddToClassList("text-field");
                inputField.style.flexGrow = 1;
                inputField.style.marginRight = 5;
                inputField.value = entry.ValueString;

                var btnMinus = new Button();
                btnMinus.AddToClassList("button-square");
                btnMinus.AddToClassList("button-minus");
                btnMinus.AddToClassList("button-square--small");
                btnMinus.style.marginRight = 2;

                var btnPlus = new Button();
                btnPlus.AddToClassList("button-square");
                btnPlus.AddToClassList("button-plus");
                btnPlus.AddToClassList("button-square--small");

                float sliderMin = entry.MinValue ?? 0f;
                float sliderMax = entry.MaxValue ?? 100f;
                float currentSliderVal = 0f;
                if (float.TryParse(entry.ValueString.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedInitial))
                {
                  currentSliderVal = Mathf.Clamp(parsedInitial, sliderMin, sliderMax);
                }

                var uiSlider = new Slider(sliderMin, sliderMax)
                {
                  value = currentSliderVal
                };

                uiSlider.AddToClassList("slider");
                uiSlider.AddToClassList("precise-slider__slider");
                uiSlider.style.marginTop = 8;
                uiSlider.style.width = Length.Percent(100);

                Action<float> applyButtonDelta = (delta) =>
                {
                  float currentVal = 0f;
                  if (float.TryParse(inputField.value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                  {
                    currentVal = parsed;
                  }

                  float newVal = currentVal + delta;

                  if (entry.MinValue.HasValue) newVal = Mathf.Max(newVal, entry.MinValue.Value);
                  if (entry.MaxValue.HasValue) newVal = Mathf.Min(newVal, entry.MaxValue.Value);

                  string newValStr = isInt ? Mathf.RoundToInt(newVal).ToString() : newVal.ToString("0.##", CultureInfo.InvariantCulture);
                  inputField.value = newValStr;
                };

                btnMinus.RegisterCallback<ClickEvent>(evt => applyButtonDelta(-step));
                btnPlus.RegisterCallback<ClickEvent>(evt => applyButtonDelta(step));

                inputField.RegisterValueChangedCallback(evt =>
                {
                  if (validationUIUpdaters.TryGetValue(entry.Key, out var clearer)) clearer(false);

                  string val = evt.newValue.Trim();
                  if (val == entry.ValueString) pendingChanges.Remove(entry.Key);
                  else pendingChanges[entry.Key] = val;

                  if (float.TryParse(val.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedVal))
                  {
                    float clamped = Mathf.Clamp(parsedVal, sliderMin, sliderMax);
                    if (Mathf.Abs(uiSlider.value - clamped) > 0.001f)
                    {
                      uiSlider.value = clamped;
                    }
                  }

                  updateDirtyState();
                });

                uiSlider.RegisterValueChangedCallback(evt =>
                {
                  float newVal = evt.newValue;
                  string newValStr = isInt ? Mathf.RoundToInt(newVal).ToString() : newVal.ToString("0.##", CultureInfo.InvariantCulture);
                  if (inputField.value != newValStr)
                  {
                    inputField.value = newValStr;
                  }
                });

                resetToDefaultAction = () =>
                {
                  string defValStr = entry.DefaultValue?.ToString();
                  inputField.value = defValStr;
                  if (float.TryParse(defValStr?.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedDef))
                  {
                    uiSlider.value = Mathf.Clamp(parsedDef, sliderMin, sliderMax);
                  }
                };

                resetToSavedAction = () =>
                {
                  inputField.value = entry.ValueString;
                  if (float.TryParse(entry.ValueString.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedSaved))
                  {
                    uiSlider.value = Mathf.Clamp(parsedSaved, sliderMin, sliderMax);
                  }
                };

                stepperWrapper.Add(inputField);
                stepperWrapper.Add(btnMinus);
                stepperWrapper.Add(btnPlus);

                var sliderGroupWrapper = new VisualElement();
                sliderGroupWrapper.style.flexDirection = FlexDirection.Column;
                sliderGroupWrapper.style.width = Length.Percent(100);
                sliderGroupWrapper.Add(stepperWrapper);
                sliderGroupWrapper.Add(uiSlider);

                control = sliderGroupWrapper;
                break;

              case "dropdown":
                var options = entry.Options ?? new List<string>();
                var dependencies = SimpleConfigUIDependencies.Instance;
                if (dependencies == null) break;

                var dropdown = new Timberborn.DropdownSystem.Dropdown();
                dropdown.Initialize(dependencies.DropdownListDrawer);

                var provider = new SimpleConfigDropdownProvider(
                  options,
                  getter: () => pendingChanges.TryGetValue(entry.Key, out string pend) ? pend : entry.ValueString,
                  setter: (val) =>
                  {
                    if (val == entry.ValueString) pendingChanges.Remove(entry.Key);
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

                resetToSavedAction = () =>
                {
                  provider.SetValue(entry.ValueString);
                  dropdown.UpdateSelectedValue();
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

              case "color":
                var colorField = new NineSliceTextField { value = entry.ValueString };
                colorField.AddToClassList("text-field");

                colorField.RegisterValueChangedCallback(evt => {
                  if (validationUIUpdaters.TryGetValue(entry.Key, out var clearer)) clearer(false);

                  string val = evt.newValue.Trim();
                  if (val == entry.ValueString) pendingChanges.Remove(entry.Key);
                  else pendingChanges[entry.Key] = val;

                  updateDirtyState();
                });
                resetToDefaultAction = () => colorField.value = entry.DefaultValue?.ToString();
                resetToSavedAction = () => colorField.value = entry.ValueString;
                control = colorField;
                break;

              case "textfield":
              default:
                var field = new NineSliceTextField { value = entry.ValueString };
                field.AddToClassList("text-field");

                field.RegisterValueChangedCallback(evt => {
                  if (validationUIUpdaters.TryGetValue(entry.Key, out var clearer)) clearer(false);

                  string newVal = evt.newValue.Trim();
                  if (newVal == entry.ValueString) pendingChanges.Remove(entry.Key);
                  else pendingChanges[entry.Key] = newVal;

                  updateDirtyState();
                });

                resetToDefaultAction = () => field.value = entry.DefaultValue?.ToString();
                resetToSavedAction = () => field.value = entry.ValueString;
                control = field;
                break;
            }

            if (control != null) controlContainer.Add(control);

            row.Add(textContainer);
            row.Add(controlContainer);

            foldout.Add(row);
            rowIndex++;
          }

          var saveWrapper = new VisualElement();
          saveWrapper.style.flexDirection = FlexDirection.Row;
          saveWrapper.style.justifyContent = Justify.Center;
          saveWrapper.style.alignItems = Align.Center;
          saveWrapper.style.paddingTop = 24;
          saveWrapper.style.paddingBottom = 24;
          saveWrapper.style.paddingLeft = 10;
          saveWrapper.style.paddingRight = 10;

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