using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Timberborn.PlayerDataSystem;
using UnityEngine;

namespace Calloatti.Config
{
  public enum ValidationCode
  {
    Valid,
    InvalidFormat,
    BelowMinimum,
    AboveMaximum,
    NotInOptions
  }

  public class SimpleConfigSchema
  {
    public string ConfigFileName { get; set; } = "DefaultModConfig.txt";
    public List<SimpleConfigEntry> Settings { get; set; } = new List<SimpleConfigEntry>();
  }

  public class SimpleConfigEntry
  {
    private bool _valueBool;
    private int _valueInt;
    private float _valueFloat;
    private string _valueString;

    public string Key { get; set; }
    public string Type { get; set; }
    public object DefaultValue { get; set; }

    public object Value
    {
      get
      {
        switch (Type?.ToLowerInvariant())
        {
          case "bool": return _valueBool;
          case "int": return _valueInt;
          case "float": return _valueFloat;
          case "string":
          default: return _valueString;
        }
      }
      set
      {
        if (value == null) return;

        switch (Type?.ToLowerInvariant())
        {
          case "bool":
            if (value is bool b) _valueBool = b;
            else if (bool.TryParse(value.ToString(), out bool parsedBool)) _valueBool = parsedBool;
            break;
          case "int":
            if (value is int i) _valueInt = i;
            else if (int.TryParse(value.ToString(), out int parsedInt)) _valueInt = parsedInt;
            break;
          case "float":
            if (value is float f) _valueFloat = f;
            else if (float.TryParse(value.ToString().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedFloat)) _valueFloat = parsedFloat;
            break;
          case "string":
          default:
            _valueString = value.ToString();
            break;
        }
      }
    }

    internal bool ValueBool => _valueBool;
    internal int ValueInt => _valueInt;
    internal float ValueFloat => _valueFloat;

    internal string ValueString
    {
      get
      {
        switch (Type?.ToLowerInvariant())
        {
          case "bool": return _valueBool.ToString().ToLowerInvariant();
          case "int": return _valueInt.ToString();
          case "float": return _valueFloat.ToString(CultureInfo.InvariantCulture);
          case "string":
          default: return _valueString ?? string.Empty;
        }
      }
    }

    public string Label { get; set; }
    public string Tooltip { get; set; }
    public string ControlType { get; set; }
    public float? MinValue { get; set; }
    public float? MaxValue { get; set; }
    public float? Step { get; set; }
    public List<string> Options { get; set; }

    public bool AvailableInMainMenu { get; set; } = true;
    public bool AvailableInGame { get; set; } = true;
    public bool AvailableInMapEditor { get; set; } = true;

    public bool RequiresRestart { get; set; }
    public bool RequiresReload { get; set; }

    public bool IsDefined { get; set; } = false;

    public ValidationCode Validate(string input)
    {
      if (string.IsNullOrWhiteSpace(input)) return ValidationCode.Valid;

      switch (Type?.ToLowerInvariant())
      {
        case "bool":
          if (!bool.TryParse(input, out _)) return ValidationCode.InvalidFormat;
          break;

        case "int":
          if (!int.TryParse(input, out int i)) return ValidationCode.InvalidFormat;
          if (MinValue.HasValue && i < MinValue.Value) return ValidationCode.BelowMinimum;
          if (MaxValue.HasValue && i > MaxValue.Value) return ValidationCode.AboveMaximum;
          break;

        case "float":
          if (!float.TryParse(input.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) return ValidationCode.InvalidFormat;
          if (MinValue.HasValue && f < MinValue.Value) return ValidationCode.BelowMinimum;
          if (MaxValue.HasValue && f > MaxValue.Value) return ValidationCode.AboveMaximum;
          break;

        case "string":
        default:
          if (ControlType?.Equals("Color", StringComparison.OrdinalIgnoreCase) == true)
          {
            if (!input.StartsWith("#") || (input.Length != 7 && input.Length != 9)) return ValidationCode.InvalidFormat;
          }
          else if (Options != null && Options.Count > 0 && !Options.Contains(input))
          {
            return ValidationCode.NotInOptions;
          }
          break;
      }

      return ValidationCode.Valid;
    }
  }

  public class SimpleConfig
  {
    private readonly string _txtFilePath;
    private readonly string _txtSchemaPath;
    private readonly Dictionary<string, string> _settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _comments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private SimpleConfigSchema _cachedSchema;

    private FileSystemWatcher _watcher;
    private DateTime _lastFileEventTime = DateTime.MinValue;
    private readonly object _lockObj = new object();

    public SimpleConfig(string modPath)
    {
      _txtSchemaPath = Path.Combine(modPath, "SimpleConfig.txt");

      if (!File.Exists(_txtSchemaPath))
      {
        Debug.LogError($"[SimpleConfig] CRITICAL ERROR: SimpleConfig.txt default records not found at: {_txtSchemaPath}");
        return;
      }

      _cachedSchema = LoadConfigFile();
      _txtFilePath = Path.Combine(PlayerDataFileService.PlayerDataDirectory, _cachedSchema.ConfigFileName);

      LoadSettingsFile();
      UpdateSettingsFile();
      InitializeWatcher();
    }

    private void InitializeWatcher()
    {
      string dir = Path.GetDirectoryName(_txtFilePath);
      string file = Path.GetFileName(_txtFilePath);

      if (!Directory.Exists(dir)) return;

      _watcher = new FileSystemWatcher(dir, file)
      {
        NotifyFilter = NotifyFilters.LastWrite,
        EnableRaisingEvents = true
      };

      _watcher.Changed += OnConfigFileChanged;
    }

    private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
      if ((DateTime.UtcNow - _lastFileEventTime).TotalMilliseconds < 500) return;
      _lastFileEventTime = DateTime.UtcNow;

      System.Threading.Thread.Sleep(50);
      LoadSettingsFile();
    }

    private string CleanSchemaValue(string val)
    {
      if (string.IsNullOrWhiteSpace(val)) return val;
      val = val.Trim();

      if (val.EndsWith(",")) val = val.Substring(0, val.Length - 1).Trim();
      if (val.StartsWith("\"")) val = val.Substring(1);
      if (val.EndsWith("\"")) val = val.Substring(0, val.Length - 1);

      return val.Trim();
    }

    private string InferType(string val)
    {
      if (bool.TryParse(val, out _)) return "bool";
      if (int.TryParse(val, out _)) return "int";
      if (float.TryParse(val.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return "float";
      return "string";
    }

    public SimpleConfigSchema LoadSchema()
    {
      return _cachedSchema;
    }

    // FIXED: Cleaned block-parsing structure ensures Key values are always mapped properly
    private SimpleConfigSchema LoadConfigFile()
    {
      SimpleConfigSchema schema = new SimpleConfigSchema();
      SimpleConfigEntry currentEntry = null;

      string[] lines = File.ReadAllLines(_txtSchemaPath);
      foreach (string rawLine in lines)
      {
        string line = rawLine.Trim();

        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("//"))
          continue;

        int equalsIndex = line.IndexOf('=');
        if (equalsIndex > 0)
        {
          string prop = CleanSchemaValue(line.Substring(0, equalsIndex));
          string rawValue = line.Substring(equalsIndex + 1).Trim();

          if (prop.Equals("ConfigFileName", StringComparison.OrdinalIgnoreCase))
          {
            ApplySchemaProperty(schema, null, prop, rawValue);
            continue;
          }

          if (prop.Equals("Key", StringComparison.OrdinalIgnoreCase))
          {
            if (currentEntry != null)
            {
              currentEntry.Value = currentEntry.DefaultValue;
              schema.Settings.Add(currentEntry);
            }
            currentEntry = new SimpleConfigEntry { IsDefined = true };
          }

          ApplySchemaProperty(schema, currentEntry, prop, rawValue);
        }
      }

      if (currentEntry != null)
      {
        currentEntry.Value = currentEntry.DefaultValue;
        schema.Settings.Add(currentEntry);
      }

      return schema;
    }

    private void ApplySchemaProperty(SimpleConfigSchema schema, SimpleConfigEntry entry, string prop, string rawValue)
    {
      string val = CleanSchemaValue(rawValue);

      if (prop.Equals("ConfigFileName", StringComparison.OrdinalIgnoreCase))
      {
        schema.ConfigFileName = val;
        return;
      }

      if (entry == null) return;

      switch (prop.ToLowerInvariant())
      {
        case "key": entry.Key = val; break;
        case "type": entry.Type = val; break;
        case "defaultvalue": entry.DefaultValue = val; break;
        case "label": entry.Label = val; break;
        case "tooltip": entry.Tooltip = val; break;
        case "controltype": entry.ControlType = val; break;
        case "minvalue": if (float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out float min)) entry.MinValue = min; break;
        case "maxvalue": if (float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out float max)) entry.MaxValue = max; break;
        case "step": if (float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out float step)) entry.Step = step; break;
        case "requiresrestart": if (bool.TryParse(val, out bool rr)) entry.RequiresRestart = rr; break;
        case "requiresreload": if (bool.TryParse(val, out bool rl)) entry.RequiresReload = rl; break;
        case "options": entry.Options = ParseSchemaArray(val); break;
        case "availableinmainmenu": if (bool.TryParse(val, out bool amm)) entry.AvailableInMainMenu = amm; break;
        case "availableingame": if (bool.TryParse(val, out bool ag)) entry.AvailableInGame = ag; break;
        case "availableinmapeditor": if (bool.TryParse(val, out bool ame)) entry.AvailableInMapEditor = ame; break;
      }
    }

    private List<string> ParseSchemaArray(string val)
    {
      var list = new List<string>();
      val = val.Replace("[", "").Replace("]", "");
      string[] parts = val.Split(',');
      foreach (var p in parts)
      {
        string clean = CleanSchemaValue(p);
        if (!string.IsNullOrEmpty(clean)) list.Add(clean);
      }
      return list;
    }

    public void LoadSettingsFile()
    {
      if (!File.Exists(_txtFilePath)) return;

      string[] lines;
      try
      {
        lines = File.ReadAllLines(_txtFilePath);
      }
      catch (IOException ex)
      {
        Debug.LogWarning($"[SimpleConfig] Failed to read settings due to file lock, skipping reload: {ex.Message}");
        return;
      }

      lock (_lockObj)
      {
        _settings.Clear();
        _comments.Clear();

        foreach (string line in lines)
        {
          string trimmed = line.Trim();

          if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith("//"))
            continue;

          int equalsIndex = trimmed.IndexOf('=');
          if (equalsIndex > 0)
          {
            string key = trimmed.Substring(0, equalsIndex).Trim();
            string rawValue = trimmed.Substring(equalsIndex + 1);
            int hashIndex = rawValue.IndexOf(" #");
            if (hashIndex < 0) hashIndex = rawValue.IndexOf("#");

            string activeStringVal = hashIndex >= 0 ? rawValue.Substring(0, hashIndex).Trim() : rawValue.Trim();
            string comment = hashIndex >= 0 ? rawValue.Substring(hashIndex).Trim() : "";

            _settings[key] = activeStringVal;
            if (hashIndex >= 0)
            {
              _comments[key] = comment;
            }

            var entry = _cachedSchema?.Settings.FirstOrDefault(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (entry != null)
            {
              entry.Value = activeStringVal;
            }
            else
            {
              var newEntry = new SimpleConfigEntry
              {
                Key = key,
                Label = key, // <--- ADDED: Fixes the root issue so it is never null!
                IsDefined = false
              };

              if (comment.IndexOf("Inline:true", StringComparison.OrdinalIgnoreCase) >= 0)
              {
                var parts = comment.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                  var trimmedPart = part.Trim();
                  if (trimmedPart.StartsWith("#")) trimmedPart = trimmedPart.Substring(1).Trim();
                  if (trimmedPart.Equals("Inline:true", StringComparison.OrdinalIgnoreCase)) continue;

                  int colonIdx = trimmedPart.IndexOf(':');
                  if (colonIdx > 0)
                  {
                    string propName = trimmedPart.Substring(0, colonIdx).Trim();
                    string propVal = trimmedPart.Substring(colonIdx + 1).Trim();
                    ApplySchemaProperty(_cachedSchema, newEntry, propName, propVal);
                  }
                }
              }

              if (string.IsNullOrWhiteSpace(newEntry.Type)) newEntry.Type = InferType(activeStringVal);
              if (newEntry.DefaultValue == null) newEntry.DefaultValue = activeStringVal;
              if (string.IsNullOrWhiteSpace(newEntry.Label) || newEntry.Label.Trim() == "#") newEntry.Label = key;
              if (string.IsNullOrWhiteSpace(newEntry.Tooltip)) newEntry.Tooltip = "";

              if (string.IsNullOrWhiteSpace(newEntry.ControlType) || newEntry.ControlType.Trim() == "#")
              {
                newEntry.ControlType = newEntry.Type == "bool" ? "Toggle" : "TextField";
              }

              newEntry.Value = activeStringVal;
              _cachedSchema.Settings.Add(newEntry);
            }
          }
        }
      }
    }

    public void UpdateSettingsFile()
    {
      Directory.CreateDirectory(PlayerDataFileService.PlayerDataDirectory);
      List<string> outputLines = new List<string>();

      if (_watcher != null) _watcher.EnableRaisingEvents = false;

      lock (_lockObj)
      {
        foreach (var entry in _cachedSchema.Settings)
        {
          if (string.IsNullOrWhiteSpace(entry.Key)) continue;

          string strValue = entry.ValueString;

          string comment = "Inline:true";
          if (!string.IsNullOrWhiteSpace(entry.Type)) comment += $" | Type:{entry.Type}";
          if (entry.DefaultValue != null)
          {
            string strDefault;
            if (entry.DefaultValue is double d) strDefault = d.ToString(CultureInfo.InvariantCulture);
            else if (entry.DefaultValue is float f) strDefault = f.ToString(CultureInfo.InvariantCulture);
            else if (entry.DefaultValue is bool b) strDefault = b.ToString().ToLowerInvariant();
            else strDefault = entry.DefaultValue.ToString();

            comment += $" | DefaultValue:{strDefault}";
          }
          if (!string.IsNullOrWhiteSpace(entry.Label)) comment += $" | Label:{entry.Label}";
          if (!string.IsNullOrWhiteSpace(entry.Tooltip)) comment += $" | Tooltip:{entry.Tooltip}";
          if (!string.IsNullOrWhiteSpace(entry.ControlType)) comment += $" | ControlType:{entry.ControlType}";
          if (entry.MinValue.HasValue) comment += $" | MinValue:{entry.MinValue.Value.ToString(CultureInfo.InvariantCulture)}";
          if (entry.MaxValue.HasValue) comment += $" | MaxValue:{entry.MaxValue.Value.ToString(CultureInfo.InvariantCulture)}";
          if (entry.Step.HasValue) comment += $" | Step:{entry.Step.Value.ToString(CultureInfo.InvariantCulture)}";
          if (entry.RequiresRestart) comment += " | RequiresRestart:true";
          if (entry.RequiresReload) comment += " | RequiresReload:true";
          if (entry.Options != null && entry.Options.Any()) comment += $" | Options:[{string.Join(",", entry.Options)}]";

          comment += $" | AvailableInMainMenu:{entry.AvailableInMainMenu.ToString().ToLowerInvariant()}";
          comment += $" | AvailableInGame:{entry.AvailableInGame.ToString().ToLowerInvariant()}";
          comment += $" | AvailableInMapEditor:{entry.AvailableInMapEditor.ToString().ToLowerInvariant()}";

          string fullLine = $"{entry.Key}={strValue} # {comment}";
          outputLines.Add(fullLine);

          _settings[entry.Key] = strValue;
          _comments[entry.Key] = "# " + comment;
        }
      }

      try
      {
        File.WriteAllLines(_txtFilePath, outputLines);
      }
      catch (IOException ex)
      {
        Debug.LogError($"[SimpleConfig] Failed to write settings file: {ex.Message}");
      }

      if (_watcher != null) _watcher.EnableRaisingEvents = true;
    }

    public void Save()
    {
      UpdateSettingsFile();
    }

    [Obsolete("Use LoadSettingsFile directly instead.")]
    public void LoadSettings() => LoadSettingsFile();

    public bool HasKey(string key)
    {
      lock (_lockObj) { return _settings.ContainsKey(key); }
    }

    public void DeleteKey(string key)
    {
      lock (_lockObj)
      {
        _settings.Remove(key);
        _comments.Remove(key);
      }
    }

    public List<string> GetAllKeys()
    {
      lock (_lockObj)
      {
        return new List<string>(_settings.Keys);
      }
    }

    public string GetString(string key)
    {
      var entry = _cachedSchema?.Settings.FirstOrDefault(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
      if (entry != null) return entry.ValueString;

      lock (_lockObj)
      {
        if (_settings.TryGetValue(key, out string val)) return val;
      }
      Debug.LogError($"[SimpleConfig] ERROR: Missing string for key '{key}'.");
      return string.Empty;
    }

    public bool GetBool(string key)
    {
      var entry = _cachedSchema?.Settings.FirstOrDefault(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
      if (entry != null) return entry.ValueBool;

      lock (_lockObj)
      {
        if (_settings.TryGetValue(key, out string val) && bool.TryParse(val, out bool result)) return result;
      }
      Debug.LogError($"[SimpleConfig] ERROR: Missing or invalid bool for key '{key}'.");
      return false;
    }

    public int GetInt(string key)
    {
      var entry = _cachedSchema?.Settings.FirstOrDefault(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
      if (entry != null) return entry.ValueInt;

      lock (_lockObj)
      {
        if (_settings.TryGetValue(key, out string val) && int.TryParse(val, out int result)) return result;
      }
      Debug.LogError($"[SimpleConfig] ERROR: Missing or invalid int for key '{key}'.");
      return 0;
    }

    public float GetFloat(string key)
    {
      var entry = _cachedSchema?.Settings.FirstOrDefault(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
      if (entry != null) return entry.ValueFloat;

      lock (_lockObj)
      {
        if (_settings.TryGetValue(key, out string val) && float.TryParse(val.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float result)) return result;
      }
      Debug.LogError($"[SimpleConfig] ERROR: Missing or invalid float for key '{key}'.");
      return 0f;
    }

    public void InsertOrUpdate(string key, object value)
    {
      if (string.IsNullOrWhiteSpace(key)) return;

      lock (_lockObj)
      {
        var entry = _cachedSchema?.Settings.FirstOrDefault(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (entry != null)
        {
          // Case A: Key exists. Just update its value.
          entry.Value = value;
          _settings[key] = entry.ValueString;
        }
        else
        {
          // Case B: Key is brand new. Instantiate, infer type, and assign value safely.
          var newEntry = new SimpleConfigEntry
          {
            Key = key,
            IsDefined = false
          };

          // Native C# type checking
          if (value is bool) newEntry.Type = "bool";
          else if (value is int) newEntry.Type = "int";
          else if (value is float || value is double) newEntry.Type = "float";
          else
          {
            // Fallback: If a raw string is passed, try to parse/infer its type
            newEntry.Type = InferType(value?.ToString() ?? "");
          }

          newEntry.Value = value;
          newEntry.DefaultValue = value; // Default matches the initial insert value

          _cachedSchema?.Settings.Add(newEntry);
          _settings[key] = newEntry.ValueString;
        }
      }
    }

    // Legacy methods for compatibility with older codebases. Prefer using InsertOrUpdate for new implementations.
    public void Set(string key, object value)
    {
      InsertOrUpdate(key, value?.ToString());
    }

    public T GetEnum<T>(string key) where T : struct, Enum
    {
      string val = GetString(key);
      if (Enum.TryParse<T>(val, true, out T result))
      {
        return result;
      }
      return default(T);
    }
    public void SetComment(string key, string comment)
    {
      if (string.IsNullOrWhiteSpace(comment))
      {
        lock (_lockObj) { _comments.Remove(key); }
        return;
      }

      string trimmed = comment.TrimStart();
      if (!trimmed.StartsWith("#") && !trimmed.StartsWith("//"))
      {
        comment = "# " + comment;
      }

      lock (_lockObj) { _comments[key] = comment; }
    }

    public void SetInlineComment(
      string key,
      string type = null,
      object defaultValue = null,
      string label = null,
      string tooltip = null,
      string controlType = null,
      float? minValue = null,
      float? maxValue = null,
      float? step = null,
      List<string> options = null,
      bool? availableInMainMenu = null,
      bool? availableInGame = null,
      bool? availableInMapEditor = null,
      bool? requiresRestart = null,
      bool? requiresReload = null)
    {
      // 1. Check if the key was passed
      if (string.IsNullOrWhiteSpace(key)) return;

      // 2. Thread-safe guard: Exit immediately if the setting doesn't already exist in memory
      SimpleConfigEntry entry;
      lock (_lockObj)
      {
        entry = _cachedSchema?.Settings.FirstOrDefault(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
      }
      if (entry == null) return;

      string strDefault = null;
      if (defaultValue != null)
      {
        if (defaultValue is double d) strDefault = d.ToString(CultureInfo.InvariantCulture);
        else if (defaultValue is float f) strDefault = f.ToString(CultureInfo.InvariantCulture);
        else if (defaultValue is bool b) strDefault = b.ToString().ToLowerInvariant();
        else strDefault = defaultValue.ToString();
      }

      var tokens = new List<string> { "Inline:true" };

      if (!string.IsNullOrWhiteSpace(type)) tokens.Add($"Type:{type}");
      if (strDefault != null) tokens.Add($"DefaultValue:{strDefault}");
      if (!string.IsNullOrWhiteSpace(label)) tokens.Add($"Label:{label}");
      if (!string.IsNullOrWhiteSpace(tooltip)) tokens.Add($"Tooltip:{tooltip}");
      if (!string.IsNullOrWhiteSpace(controlType)) tokens.Add($"ControlType:{controlType}");

      if (minValue.HasValue) tokens.Add($"MinValue:{minValue.Value.ToString(CultureInfo.InvariantCulture)}");
      if (maxValue.HasValue) tokens.Add($"MaxValue:{maxValue.Value.ToString(CultureInfo.InvariantCulture)}");
      if (step.HasValue) tokens.Add($"Step:{step.Value.ToString(CultureInfo.InvariantCulture)}");

      if (requiresRestart.HasValue && requiresRestart.Value) tokens.Add("RequiresRestart:true");
      if (requiresReload.HasValue && requiresReload.Value) tokens.Add("RequiresReload:true");

      if (options != null && options.Any()) tokens.Add($"Options:[{string.Join(",", options)}]");

      if (availableInMainMenu.HasValue) tokens.Add($"AvailableInMainMenu:{availableInMainMenu.Value.ToString().ToLowerInvariant()}");
      if (availableInGame.HasValue) tokens.Add($"AvailableInGame:{availableInGame.Value.ToString().ToLowerInvariant()}");
      if (availableInMapEditor.HasValue) tokens.Add($"AvailableInMapEditor:{availableInMapEditor.Value.ToString().ToLowerInvariant()}");

      string fullCommentString = string.Join(" | ", tokens);

      lock (_lockObj)
      {
        _comments[key] = "# " + fullCommentString;

        if (type != null) entry.Type = type;
        if (defaultValue != null) entry.DefaultValue = defaultValue;
        if (label != null) entry.Label = label;
        if (tooltip != null) entry.Tooltip = tooltip;
        if (controlType != null) entry.ControlType = controlType;
        if (minValue.HasValue) entry.MinValue = minValue;
        if (maxValue.HasValue) entry.MaxValue = maxValue;
        if (step.HasValue) entry.Step = step;
        if (requiresRestart.HasValue) entry.RequiresRestart = requiresRestart.Value;
        if (requiresReload.HasValue) entry.RequiresReload = requiresReload.Value;
        if (options != null) entry.Options = options;
        if (availableInMainMenu.HasValue) entry.AvailableInMainMenu = availableInMainMenu.Value;
        if (availableInGame.HasValue) entry.AvailableInGame = availableInGame.Value;
        if (availableInMapEditor.HasValue) entry.AvailableInMapEditor = availableInMapEditor.Value;
      }
    }
  }
}