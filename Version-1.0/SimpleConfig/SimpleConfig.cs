using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Timberborn.PlayerDataSystem;
using UnityEngine;

// 2026/02/25 08:42

namespace Calloatti.Config
{
  public class SimpleConfigSchema
  {
    public string ConfigFileName { get; set; } = "DefaultModConfig.txt";
    public List<SimpleConfigEntry> Settings { get; set; } = new List<SimpleConfigEntry>();
  }

  public class SimpleConfigEntry
  {
    public string Key { get; set; }
    public string Type { get; set; }
    public object DefaultValue { get; set; }
    public string Label { get; set; }
    public string Tooltip { get; set; }
    public string ControlType { get; set; }
    public float? MinValue { get; set; }
    public float? MaxValue { get; set; }
    public float? Step { get; set; }
    public List<string> Options { get; set; }

    public List<string> AvailableIn { get; set; } = new List<string> { "MainMenu" };

    public bool RequiresRestart { get; set; }
    public bool RequiresReload { get; set; }

    // State tracking properties to isolate legacy/modern orphans from schema configurations
    public bool IsOrphan { get; set; }
    public bool IsInline { get; set; }
    public string RawComment { get; set; }
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
        Debug.LogError($"[SimpleConfig] CRITICAL ERROR: SimpleConfig.txt not found at: {_txtSchemaPath}");
        return;
      }

      _cachedSchema = ParseSchemaFile();
      _txtFilePath = Path.Combine(PlayerDataFileService.PlayerDataDirectory, _cachedSchema.ConfigFileName);

      LoadTxt();
      EnrichSchemaWithOrphans();
      SyncWithSchema(_cachedSchema);
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
      LoadTxt();
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

    public SimpleConfigSchema LoadSchema()
    {
      return _cachedSchema;
    }

    private SimpleConfigSchema ParseSchemaFile()
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

          int hashIndex = rawValue.IndexOf(" #");
          if (hashIndex >= 0)
          {
            rawValue = rawValue.Substring(0, hashIndex).Trim();
          }

          if (prop.Equals("Key", StringComparison.OrdinalIgnoreCase))
          {
            if (currentEntry != null)
            {
              schema.Settings.Add(currentEntry);
            }
            currentEntry = new SimpleConfigEntry { IsOrphan = false }; // Explicitly schema-bound
          }

          ApplySchemaProperty(schema, currentEntry, prop, rawValue);
        }
      }

      if (currentEntry != null)
      {
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
        case "availablein": entry.AvailableIn = ParseSchemaArray(val); break;
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

    private void EnrichSchemaWithOrphans()
    {
      if (_cachedSchema == null) return;

      HashSet<string> knownKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (var entry in _cachedSchema.Settings)
      {
        if (!string.IsNullOrEmpty(entry.Key)) knownKeys.Add(entry.Key);
      }

      foreach (var kvp in _settings)
      {
        string key = kvp.Key;
        if (knownKeys.Contains(key)) continue;

        string val = kvp.Value;
        _comments.TryGetValue(key, out string comment);
        comment = comment ?? "";

        var newEntry = new SimpleConfigEntry
        {
          Key = key,
          Label = key,
          Tooltip = "",
          DefaultValue = val,
          RawComment = comment,
          IsOrphan = true // Segregates writing behavior from SyncWithSchema configuration overwrites
        };

        if (comment.IndexOf("Inline:true", StringComparison.OrdinalIgnoreCase) >= 0)
        {
          newEntry.IsInline = true;
          var parts = comment.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
          foreach (var part in parts)
          {
            var trimmedPart = part.Trim();
            if (trimmedPart.Equals("Inline:true", StringComparison.OrdinalIgnoreCase)) continue;

            int colonIdx = trimmedPart.IndexOf(':');
            if (colonIdx > 0)
            {
              string propName = trimmedPart.Substring(0, colonIdx).Trim();
              string propVal = trimmedPart.Substring(colonIdx + 1).Trim();
              ApplySchemaProperty(_cachedSchema, newEntry, propName, propVal);
            }
          }

          if (string.IsNullOrWhiteSpace(newEntry.Label) || newEntry.Label.Trim() == "#")
          {
            newEntry.Label = key;
          }

          if (string.IsNullOrWhiteSpace(newEntry.ControlType) || newEntry.ControlType.Trim() == "#")
          {
            newEntry.ControlType = (bool.TryParse(val, out _) || bool.TryParse(newEntry.DefaultValue?.ToString(), out _)) ? "toggle" : "textfield";
          }
        }
        else
        {
          // LEGACY PARSER (Protects layout and captures comment substrings without destructively overwriting)
          newEntry.IsInline = false;
          string defaultValue = val;
          string label = key;
          string tooltip = "";

          int defaultIdx = comment.LastIndexOf("default", StringComparison.OrdinalIgnoreCase);
          if (defaultIdx >= 0)
          {
            string defaultPart = comment.Substring(defaultIdx);
            int colonIdx = defaultPart.IndexOf(':');
            if (colonIdx >= 0)
            {
              defaultValue = defaultPart.Substring(colonIdx + 1).Trim();
            }
            comment = comment.Substring(0, defaultIdx).Trim(' ', '-');
          }

          if (!string.IsNullOrWhiteSpace(comment))
          {
            if (comment.IndexOf("true/false", StringComparison.OrdinalIgnoreCase) >= 0)
            {
              tooltip = comment;
            }
            else
            {
              var parts = comment.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
              if (parts.Length == 1)
              {
                label = parts[0].Trim();
              }
              else if (parts.Length >= 2)
              {
                label = parts[0].Trim();
                tooltip = string.Join(" - ", parts.Skip(1)).Trim();
              }
            }
          }

          string controlType = "textfield";
          if (bool.TryParse(val, out _) || bool.TryParse(defaultValue, out _))
          {
            controlType = "toggle";
          }

          newEntry.DefaultValue = defaultValue;
          newEntry.Label = string.IsNullOrWhiteSpace(label) || label.Trim() == "#" ? key : label;
          newEntry.Tooltip = tooltip;
          newEntry.ControlType = controlType;
        }

        _cachedSchema.Settings.Add(newEntry);
      }
    }

    private void SyncWithSchema(SimpleConfigSchema schema)
    {
      bool changesMade = false;

      if (schema?.Settings != null)
      {
        foreach (var entry in schema.Settings)
        {
          if (string.IsNullOrWhiteSpace(entry.Key)) continue;

          string strValue = "";
          if (entry.DefaultValue != null)
          {
            if (entry.DefaultValue is double d)
              strValue = d.ToString(CultureInfo.InvariantCulture);
            else if (entry.DefaultValue is float f)
              strValue = f.ToString(CultureInfo.InvariantCulture);
            else if (entry.DefaultValue is bool b)
              strValue = b.ToString();
            else
              strValue = entry.DefaultValue.ToString();
          }

          string comment = "";

          // Enforce modern formatting if it came from SimpleConfig.txt schema (non-orphan)
          // OR if it's an orphan that already explicitly requested inline processing.
          if (!entry.IsOrphan || entry.IsInline)
          {
            comment = "Inline:true";
            if (!string.IsNullOrWhiteSpace(entry.Type)) comment += $" | Type:{entry.Type}";
            if (entry.DefaultValue != null) comment += $" | DefaultValue:{strValue}"; // Moved after Type
            if (!string.IsNullOrWhiteSpace(entry.Label)) comment += $" | Label:{entry.Label}";
            if (!string.IsNullOrWhiteSpace(entry.Tooltip)) comment += $" | Tooltip:{entry.Tooltip}";
            if (!string.IsNullOrWhiteSpace(entry.ControlType)) comment += $" | ControlType:{entry.ControlType}";
            if (entry.MinValue.HasValue) comment += $" | MinValue:{entry.MinValue.Value.ToString(CultureInfo.InvariantCulture)}";
            if (entry.MaxValue.HasValue) comment += $" | MaxValue:{entry.MaxValue.Value.ToString(CultureInfo.InvariantCulture)}";
            if (entry.Step.HasValue) comment += $" | Step:{entry.Step.Value.ToString(CultureInfo.InvariantCulture)}";
            if (entry.RequiresRestart) comment += $" | RequiresRestart:true";
            if (entry.RequiresReload) comment += $" | RequiresReload:true";
            if (entry.Options != null && entry.Options.Any()) comment += $" | Options:[{string.Join(",", entry.Options)}]";
            if (entry.AvailableIn != null && entry.AvailableIn.Any()) comment += $" | AvailableIn:[{string.Join(",", entry.AvailableIn)}]";
          }
          else
          {
            // Strict Legacy Protection: Writes the captured raw comment line verbatim.
            comment = entry.RawComment;
          }

          if (!string.IsNullOrWhiteSpace(comment))
          {
            string targetComment = comment.StartsWith("#") ? comment : "# " + comment;

            string existingComment;
            lock (_lockObj)
            {
              _comments.TryGetValue(entry.Key, out existingComment);
              if (existingComment != null && !existingComment.StartsWith("#"))
              {
                existingComment = "# " + existingComment;
              }
            }

            if (existingComment != targetComment)
            {
              SetComment(entry.Key, comment);
              changesMade = true;
            }
          }

          bool hasKey;
          lock (_lockObj)
          {
            hasKey = _settings.ContainsKey(entry.Key);
          }

          if (!hasKey)
          {
            lock (_lockObj)
            {
              _settings[entry.Key] = strValue;
            }
            changesMade = true;
          }
        }
      }

      if (changesMade)
      {
        Save();
      }
    }

    public void LoadTxt()
    {
      if (!File.Exists(_txtFilePath)) return;

      string[] lines;
      try
      {
        lines = File.ReadAllLines(_txtFilePath);
      }
      catch (IOException ex)
      {
        Debug.LogWarning($"[SimpleConfig] Failed to read config due to file lock, skipping reload: {ex.Message}");
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
            int slashIndex = rawValue.IndexOf(" //");

            int commentIndex = -1;
            if (hashIndex >= 0 && slashIndex >= 0) commentIndex = Math.Min(hashIndex, slashIndex);
            else if (hashIndex >= 0) commentIndex = hashIndex;
            else if (slashIndex >= 0) commentIndex = slashIndex;

            if (commentIndex >= 0)
            {
              _settings[key] = rawValue.Substring(0, commentIndex).Trim();
              _comments[key] = rawValue.Substring(commentIndex).Trim();
            }
            else
            {
              _settings[key] = rawValue.Trim();
            }
          }
        }
      }
    }

    public void Save()
    {
      Directory.CreateDirectory(PlayerDataFileService.PlayerDataDirectory);
      List<string> outputLines = new List<string>();
      HashSet<string> writtenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      if (_watcher != null) _watcher.EnableRaisingEvents = false;

      lock (_lockObj)
      {
        if (File.Exists(_txtFilePath))
        {
          foreach (string line in File.ReadAllLines(_txtFilePath))
          {
            string trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith("//"))
            {
              outputLines.Add(line);
              continue;
            }

            int equalsIndex = trimmed.IndexOf('=');
            if (equalsIndex > 0)
            {
              string key = trimmed.Substring(0, equalsIndex).Trim();

              if (_settings.TryGetValue(key, out string val))
              {
                string comment = _comments.TryGetValue(key, out string c) ? $" {c}" : "";
                outputLines.Add($"{key}={val}{comment}");
                writtenKeys.Add(key);
              }
            }
            else
            {
              outputLines.Add(line);
            }
          }
        }

        foreach (var kvp in _settings)
        {
          if (!writtenKeys.Contains(kvp.Key))
          {
            string comment = _comments.TryGetValue(kvp.Key, out string c) ? $" {c}" : "";
            outputLines.Add($"{kvp.Key}={kvp.Value}{comment}");
          }
        }
      }

      File.WriteAllLines(_txtFilePath, outputLines);

      if (_watcher != null) _watcher.EnableRaisingEvents = true;
    }

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
      lock (_lockObj)
      {
        if (_settings.TryGetValue(key, out string val)) return val;
      }
      Debug.LogError($"[SimpleConfig] ERROR: Missing string for key '{key}'.");
      return string.Empty;
    }

    public bool GetBool(string key)
    {
      lock (_lockObj)
      {
        if (_settings.TryGetValue(key, out string val) && bool.TryParse(val, out bool result)) return result;
      }
      Debug.LogError($"[SimpleConfig] ERROR: Missing or invalid bool for key '{key}'.");
      return false;
    }

    public int GetInt(string key)
    {
      lock (_lockObj)
      {
        if (_settings.TryGetValue(key, out string val) && int.TryParse(val, out int result)) return result;
      }
      Debug.LogError($"[SimpleConfig] ERROR: Missing or invalid int for key '{key}'.");
      return 0;
    }

    public float GetFloat(string key)
    {
      lock (_lockObj)
      {
        if (_settings.TryGetValue(key, out string val) && float.TryParse(val.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float result)) return result;
      }
      Debug.LogError($"[SimpleConfig] ERROR: Missing or invalid float for key '{key}'.");
      return 0f;
    }

    public T GetEnum<T>(string key) where T : struct, Enum
    {
      lock (_lockObj)
      {
        if (_settings.TryGetValue(key, out string val) && Enum.TryParse<T>(val, true, out T result)) return result;
      }
      Debug.LogError($"[SimpleConfig] ERROR: Missing or invalid enum '{typeof(T).Name}' for key '{key}'.");
      return default;
    }

    public void Set(string key, object value)
    {
      lock (_lockObj)
      {
        if (value is float f)
          _settings[key] = f.ToString(CultureInfo.InvariantCulture);
        else if (value is double d)
          _settings[key] = d.ToString(CultureInfo.InvariantCulture);
        else
          _settings[key] = value.ToString();
      }
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

    /// <summary>
    /// Dynamically constructs and saves a modern explicit inline comment for a given key.
    /// Parameters left as null will be cleanly omitted from the resulting text file string.
    /// </summary>
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
      List<string> availableIn = null,
      bool? requiresRestart = null,
      bool? requiresReload = null)
    {
      if (string.IsNullOrWhiteSpace(key)) return;

      // 1. Format the default value to an invariant string if provided
      string strDefault = null;
      if (defaultValue != null)
      {
        if (defaultValue is double d) strDefault = d.ToString(CultureInfo.InvariantCulture);
        else if (defaultValue is float f) strDefault = f.ToString(CultureInfo.InvariantCulture);
        else if (defaultValue is bool b) strDefault = b.ToString().ToLowerInvariant();
        else strDefault = defaultValue.ToString();
      }

      // 2. Build the pipe-separated tokens list dynamically
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
      if (availableIn != null && availableIn.Any()) tokens.Add($"AvailableIn:[{string.Join(",", availableIn)}]");

      string fullCommentString = string.Join(" | ", tokens);

      lock (_lockObj)
      {
        // 3. Update the internal text comment dictionary
        _comments[key] = "# " + fullCommentString;

        // 4. Reactive UI Step: Update the cached schema memory so the UI updates instantly
        var entry = _cachedSchema?.Settings.FirstOrDefault(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
        {
          entry = new SimpleConfigEntry { Key = key, IsOrphan = true };
          _cachedSchema?.Settings.Add(entry);
        }

        entry.IsInline = true;
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
        if (availableIn != null) entry.AvailableIn = availableIn;
      }
    }
  }
}