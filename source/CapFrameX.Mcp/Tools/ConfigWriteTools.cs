using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Mcp.Attributes;
using CapFrameX.Monitoring.Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace CapFrameX.Mcp.Tools
{
    [McpServerToolType]
    public class ConfigWriteTools
    {
        // Properties on IAppConfiguration that we never expose via cfx_set_config — too risky or non-trivial to round-trip.
        private static readonly HashSet<string> ConfigSetDenylist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "OnValueChanged",
            "HardwareSimulationConfiguration",
            "ReportDataGridColumnSettings",
            "ComparisonLineGraphColors",
            "RecordListHeaderOrder",
            "LastAppNotificationTimestamp",
        };

        private readonly IAppConfiguration _config;
        private readonly IOverlayService _overlayService;
        private readonly ISensorConfig _sensorConfig;
        private readonly IRecordManager _recordManager;
        private readonly IPathService _paths;

        public ConfigWriteTools(
            IAppConfiguration config,
            IOverlayService overlayService,
            ISensorConfig sensorConfig,
            IRecordManager recordManager,
            IPathService paths)
        {
            _config = config;
            _overlayService = overlayService;
            _sensorConfig = sensorConfig;
            _recordManager = recordManager;
            _paths = paths;
        }

        // ─── cfx_get_config ──────────────────────────────────────────────────

        [McpServerTool(Name = "cfx_get_config",
            Description = "Returns the current AppSettings values. Optional `nameFilter` does a case-insensitive substring match against property names " +
                "to scope the response to a single area (e.g. 'overlay', 'capture', 'sensor'). Use cfx_get_osd_options for the complete typed OSD snapshot; " +
                "one name filter cannot cover every OSD property.")]
        public ConfigGetResult GetConfig(
            [Description("Optional case-insensitive substring filter on the property name.")] string nameFilter = null)
        {
            var result = new ConfigGetResult();
            foreach (var prop in EnumerateConfigProperties())
            {
                if (!string.IsNullOrEmpty(nameFilter) &&
                    prop.Name.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                object value;
                try { value = prop.GetValue(_config); }
                catch { continue; }

                result.Properties.Add(new ConfigPropertyInfo
                {
                    Name = prop.Name,
                    Type = prop.PropertyType.Name,
                    Value = SerializeForOutput(value),
                    Settable = prop.CanWrite && !ConfigSetDenylist.Contains(prop.Name),
                });
            }
            result.Count = result.Properties.Count;
            return result;
        }

        // ─── cfx_set_config ──────────────────────────────────────────────────

        [McpServerTool(Name = "cfx_set_config",
            Description = "Mutates a single AppSettings property. Pass the property name (e.g. 'IsOverlayActive', 'OSDRefreshPeriod') and the new value as JSON " +
                "(true / 250 / \"value\"). Returns the old and new values; the change is hot-applied where the property's setter triggers it. " +
                "Use cfx_set_osd_options for OSD properties so validation and all required runtime notifications are applied.")]
        public ConfigSetResult SetConfig(
            [Description("Property name on IAppConfiguration (case-sensitive)")] string name,
            [Description("New value as JSON: true/false, number, or quoted string. Use null to skip.")] string valueJson)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("name is required", nameof(name));
            if (ConfigSetDenylist.Contains(name))
                throw new InvalidOperationException($"Property '{name}' is not exposed for write via cfx_set_config.");

            var prop = typeof(IAppConfiguration).GetProperty(name,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null)
                throw new InvalidOperationException($"Property '{name}' not found on IAppConfiguration.");
            if (!prop.CanWrite)
                throw new InvalidOperationException($"Property '{name}' is read-only.");

            object oldValue;
            try { oldValue = prop.GetValue(_config); }
            catch { oldValue = null; }

            object newValue;
            try { newValue = JsonConvert.DeserializeObject(valueJson ?? "null", prop.PropertyType); }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"Could not parse value as {prop.PropertyType.Name}: {ex.Message}", nameof(valueJson));
            }

            Log.Logger.Information("MCP cfx_set_config: {name} = {old} -> {new}",
                name, SerializeForOutput(oldValue), SerializeForOutput(newValue));
            prop.SetValue(_config, newValue);

            return new ConfigSetResult
            {
                Name = name,
                OldValue = SerializeForOutput(oldValue),
                NewValue = SerializeForOutput(newValue),
                Applied = true,
            };
        }

        // ─── cfx_get_overlay_config ──────────────────────────────────────────

        [McpServerTool(Name = "cfx_get_overlay_config",
            Description = "Returns the raw JSON of an overlay slot configuration file (OverlayEntryConfiguration_<slot>.json). " +
                "Slots are 0/1/2 by default. Use this to back up a slot before cfx_set_overlay_config or to inspect persisted entries.")]
        public OverlayConfigResult GetOverlayConfig(
            [Description("Slot index. Active slot is currently selected via OverlayEntryConfigurationFile.")] int slot = 0)
        {
            var path = ResolveOverlayConfigPath(slot);
            var result = new OverlayConfigResult { Slot = slot, Path = path };
            if (!File.Exists(path))
            {
                result.Exists = false;
                return result;
            }
            try
            {
                result.Exists = true;
                result.Json = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                throw new IOException($"Could not read overlay config slot {slot} at '{path}': {ex.Message}", ex);
            }
            return result;
        }

        // ─── cfx_set_overlay_config ──────────────────────────────────────────

        [McpServerTool(Name = "cfx_set_overlay_config",
            Description = "Replaces the overlay slot configuration file with the provided JSON. The JSON must already match the OverlayEntryPersistence shape — " +
                "fetch via cfx_get_overlay_config first, modify, and write back. " +
                "Note: changes take effect when CapFrameX reloads the slot (e.g. user toggles slots) or restarts.")]
        public OverlayConfigResult SetOverlayConfig(
            [Description("Slot index (0/1/2)")] int slot,
            [Description("Full overlay configuration JSON to write to disk.")] string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("json must be provided", nameof(json));

            // Validate it parses before clobbering the file.
            try { JToken.Parse(json); }
            catch (Exception ex) { throw new ArgumentException("json is not valid JSON: " + ex.Message, nameof(json), ex); }

            var path = ResolveOverlayConfigPath(slot);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // Backup any existing file to .bak so the user can recover.
            if (File.Exists(path))
            {
                try { File.Copy(path, path + ".bak", overwrite: true); }
                catch (Exception ex) { Log.Logger.Warning(ex, "MCP cfx_set_overlay_config: backup failed for {path}", path); }
            }

            File.WriteAllText(path, json);
            Log.Logger.Information("MCP cfx_set_overlay_config: wrote {bytes} bytes to slot {slot} ({path})",
                json.Length, slot, path);

            return new OverlayConfigResult { Slot = slot, Path = path, Exists = true, Json = json };
        }

        // ─── cfx_toggle_overlay_entry ────────────────────────────────────────

        [McpServerTool(Name = "cfx_toggle_overlay_entry",
            Description = "Toggles a single overlay entry's enabled / show-on-overlay flag in the live overlay state — finer grained than cfx_set_overlay_config. " +
                "Pass at least one of showOnOverlay or isEntryEnabled. " +
                "Note: applies in-memory only; the change is reverted when CapFrameX restarts. Use cfx_set_overlay_entry for validated, optionally persisted changes.")]
        public ToggleOverlayEntryResult ToggleOverlayEntry(
            [Description("Entry identifier as returned by cfx_get_overlay_entries.identifier")] string identifier,
            [Description("New ShowOnOverlay value. Omit to leave unchanged.")] bool? showOnOverlay = null,
            [Description("New IsEntryEnabled value. Omit to leave unchanged.")] bool? isEntryEnabled = null)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                throw new ArgumentException("identifier must be provided", nameof(identifier));
            if (!showOnOverlay.HasValue && !isEntryEnabled.HasValue)
                throw new ArgumentException("Provide at least one of showOnOverlay or isEntryEnabled");

            var entries = _overlayService.CurrentOverlayEntries ?? Array.Empty<IOverlayEntry>();
            var entry = entries.FirstOrDefault(e =>
                string.Equals(e?.Identifier, identifier, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
                throw new InvalidOperationException($"Overlay entry '{identifier}' not found.");

            var result = new ToggleOverlayEntryResult { Identifier = identifier };
            result.OldShowOnOverlay = entry.ShowOnOverlay;
            result.OldIsEntryEnabled = entry.IsEntryEnabled;

            if (showOnOverlay.HasValue && entry.ShowOnOverlay != showOnOverlay.Value)
            {
                entry.ShowOnOverlay = showOnOverlay.Value;
            }
            if (isEntryEnabled.HasValue && entry.IsEntryEnabled != isEntryEnabled.Value)
            {
                entry.IsEntryEnabled = isEntryEnabled.Value;
            }

            result.NewShowOnOverlay = entry.ShowOnOverlay;
            result.NewIsEntryEnabled = entry.IsEntryEnabled;
            result.Applied = true;
            Log.Logger.Information("MCP cfx_toggle_overlay_entry: {id} show {oldShow}->{newShow} enabled {oldEn}->{newEn}",
                identifier, result.OldShowOnOverlay, result.NewShowOnOverlay, result.OldIsEntryEnabled, result.NewIsEntryEnabled);
            return result;
        }

        // ─── cfx_set_logged_sensors ──────────────────────────────────────────

        [McpServerTool(Name = "cfx_set_logged_sensors",
            Description = "Selects/deselects the given sensor identifiers for logging into capture records. " +
                "Use cfx_get_logged_sensors first to get valid identifiers. The change is persisted; future captures will use the new selection.")]
        public async Task<SetLoggedSensorsResult> SetLoggedSensors(
            [Description("Sensor identifiers to apply selection to. Use cfx_get_logged_sensors.sensors[].identifier.")] string[] identifiers,
            [Description("New selection state. true = include in capture, false = exclude.")] bool selected = true)
        {
            if (identifiers == null || identifiers.Length == 0)
                throw new ArgumentException("identifiers must contain at least one entry", nameof(identifiers));

            var existing = _sensorConfig.GetSensorConfigCopy() ?? new Dictionary<string, bool>();
            var result = new SetLoggedSensorsResult { Selected = selected };

            foreach (var id in identifiers)
            {
                if (string.IsNullOrEmpty(id)) continue;
                if (!existing.ContainsKey(id))
                {
                    result.Missing.Add(id);
                    continue;
                }
                _sensorConfig.SelectForLogging(id, selected);
                result.Applied.Add(id);
            }

            try { await _sensorConfig.Save().ConfigureAwait(false); }
            catch (Exception ex) { Log.Logger.Warning(ex, "MCP cfx_set_logged_sensors: save failed"); }

            Log.Logger.Information("MCP cfx_set_logged_sensors: applied={applied} missing={missing}",
                result.Applied.Count, result.Missing.Count);
            return result;
        }

        // ─── cfx_set_record_comment ──────────────────────────────────────────

        [McpServerTool(Name = "cfx_set_record_comment",
            Description = "Updates the comment field on an existing record (json or csv). Other custom fields (CPU/GPU/RAM/GameName) are preserved unchanged. " +
                "Use to annotate records — e.g. 'before driver upgrade', 'baseline', 'with VRR off'.")]
        public async Task<SetRecordCommentResult> SetRecordComment(
            [Description("Record id (absolute file path) from cfx_list_records")] string id,
            [Description("New comment string. Empty/null clears the comment.")] string comment)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("id must be provided", nameof(id));
            if (!File.Exists(id)) throw new FileNotFoundException("Record not found", id);

            var info = await _recordManager.GetFileRecordInfo(new FileInfo(id)).ConfigureAwait(false);
            if (info == null)
                throw new InvalidOperationException("Could not load record metadata: " + id);

            string oldComment = info.Comment;
            _recordManager.UpdateCustomData(info,
                customCpuInfo: info.ProcessorName,
                customGpuInfo: info.GraphicCardName,
                customRamInfo: info.SystemRamInfo,
                customMainboardInfo: info.MotherboardName,
                customGameName: info.GameName,
                customComment: comment ?? string.Empty);

            Log.Logger.Information("MCP cfx_set_record_comment: {id} '{old}' -> '{new}'", id, oldComment, comment);
            return new SetRecordCommentResult
            {
                Id = id,
                OldComment = oldComment,
                NewComment = comment ?? string.Empty,
            };
        }

        // ─── helpers ─────────────────────────────────────────────────────────

        private string ResolveOverlayConfigPath(int slot)
        {
            var folder = _paths?.ConfigFolder ?? string.Empty;
            return Path.Combine(folder, $"OverlayEntryConfiguration_{slot}.json");
        }

        private static IEnumerable<PropertyInfo> EnumerateConfigProperties()
        {
            return typeof(IAppConfiguration)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase);
        }

        private static string SerializeForOutput(object value)
        {
            if (value == null) return null;
            try { return JsonConvert.SerializeObject(value); }
            catch { return value.ToString(); }
        }
    }
}
