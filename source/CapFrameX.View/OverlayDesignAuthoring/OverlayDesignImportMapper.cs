using System;
using System.Collections.Generic;
using System.Linq;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Integration;

namespace CapFrameX.View.OverlayDesignAuthoring
{
    /// <summary>Why a reading of an imported design has no source on this machine.</summary>
    public enum OverlayDesignImportIssue
    {
        /// <summary>No device of the same kind and vendor: no GPU of that vendor, no CPU of that vendor.</summary>
        NoMatchingDevice,
        /// <summary>Several devices of that kind and vendor report the design's readings equally well.</summary>
        AmbiguousDevice,
        /// <summary>The CPU of the same vendor lacks a core type the reading or core group uses.</summary>
        MissingCoreType,
        /// <summary>The matching device does not report the reading.</summary>
        MissingReading,
        /// <summary>The design names a device whose kind or vendor cannot be told.</summary>
        UnknownDevice
    }

    /// <summary>A device the imported design reads from and the device of this machine it reads now, itself on its own hardware.</summary>
    public sealed record OverlayDesignDeviceMapping(string SourceDevice, string TargetDevice, OverlayDeviceKind Kind, string Vendor,
        OverlayDesignImportIssue? Issue);

    /// <summary>A tile reading without a source on this machine; it keeps its key. Tile is the tile's label, if it has one.</summary>
    public sealed record OverlayDesignUnresolvedReading(string Tile, string Key, OverlayDesignImportIssue Issue);

    public sealed class OverlayDesignImportResult
    {
        public string Name { get; init; }
        public string DesignJson { get; init; }
        /// <summary>The design names no hardware sensor, so it binds to this machine without the vendor and core type rules.</summary>
        public bool IsGeneric { get; init; }
        /// <summary>False when the design was imported unchanged because this machine's sources were unknown.</summary>
        public bool IsMapped { get; init; } = true;
        public IReadOnlyList<OverlayDesignDeviceMapping> Devices { get; init; } = Array.Empty<OverlayDesignDeviceMapping>();
        public IReadOnlyList<OverlayDesignUnresolvedReading> Unresolved { get; init; } = Array.Empty<OverlayDesignUnresolvedReading>();
    }

    /// <summary>
    /// Adapts an imported tile design to this machine. A design that names no hardware sensor is generic:
    /// its semantic keys and roles bind to whatever this machine reports. A design bound to another
    /// machine maps each of its devices onto one of the same kind and vendor, a GPU onto a GPU and a CPU
    /// onto a CPU, and each reading onto the sensor of the same name or, failing that, of the same meaning
    /// ("GPU TBP" for "GPU Power"). CPU core readings and core groups map onto the same core types only: a
    /// group of P-cores rebinds to this CPU's P-cores, whatever their number. A reading without a match keeps
    /// its key, so the design works again on its own hardware, and is reported.
    /// </summary>
    public static class OverlayDesignImportMapper
    {
        public static OverlayDesignImportResult Map(OsdDesignDocument document, IReadOnlyList<OverlayTelemetrySource> catalog)
        {
            ArgumentNullException.ThrowIfNull(document);
            var sources = (catalog ?? Array.Empty<OverlayTelemetrySource>())
                .Where(source => source != null && source.IsAvailable && !string.IsNullOrWhiteSpace(source.Id))
                .GroupBy(source => source.Id, StringComparer.Ordinal).Select(group => group.First()).ToArray();
            return new Mapping(document, sources).Run();
        }

        /// <summary>Hardware sensor keys tie a design to devices; semantic keys, metrics and roles do not.</summary>
        public static bool IsHardwareKey(string key) => key != null
            && (key.StartsWith(OverlaySensorSources.SensorPrefix, StringComparison.Ordinal)
                || key.StartsWith(OverlaySensorSources.UnnamedSensorPrefix, StringComparison.Ordinal));

        public static bool IsGeneric(OsdDesignDocument document) => !document.EnumerateTiles().Any(tile =>
            ReadingKeys(tile).Any(reading => IsHardwareKey(reading.Key))
            || tile.Kind == "group" && tile.GroupMembers?.Any(member => member != null && IsHardwareKey(member.Key)) == true);

        private static IEnumerable<(string Field, string Key)> ReadingKeys(OsdDesignTile tile)
        {
            if (tile.Kind != "metric" && tile.Kind != "bar" && tile.Kind != "gauge") yield break;
            if (!string.IsNullOrEmpty(tile.Key)) yield return ("key", tile.Key);
            if (!string.IsNullOrEmpty(tile.SecondaryKey)) yield return ("secondaryKey", tile.SecondaryKey);
        }

        private static string ReadingOf(string sensorType, string sensorName) => sensorType?.ToLowerInvariant() + "/" + sensorName;

        private static string ReadingOf(string key) => OverlaySensorSources.TryParseSensorId(key, out _, out string type, out string name)
            ? ReadingOf(type, name) : key;

        private sealed class LocalDevice
        {
            public string Key, Name;
            public OverlayDeviceKind Kind;
            public string Vendor;
            public OverlayTelemetrySource[] Sources;
            public Dictionary<string, OverlayTelemetrySource> Readings;

            /// <summary>Whether the device reports a reading of that core family and core type ("" for untyped).</summary>
            public bool HasCoreType(string family, string coreType) => Sources.Any(source => source.GroupKey == Key + "/" + family
                && (OverlaySensorSources.NormalizeCoreType(source.CoreType) ?? string.Empty) == coreType);
        }

        /// <summary>A device the design reads from, known by the name its sensor IDs carry.</summary>
        private sealed class DesignDevice
        {
            public string Name, HardwareType;
            public readonly HashSet<string> Readings = new HashSet<string>(StringComparer.Ordinal);
            public readonly List<string> SensorNames = new List<string>();
            // The devices of this machine whose sensors the design already reads, and whether it reads others.
            public readonly HashSet<string> Owners = new HashSet<string>(StringComparer.Ordinal);
            public bool ReadsElsewhere;
            public OverlayDesignDeviceMapping Mapping;
            public LocalDevice Target;
        }

        private sealed class Mapping
        {
            private readonly OsdDesignDocument _document;
            private readonly OverlayTelemetrySource[] _sources;
            private readonly Dictionary<string, OverlayTelemetrySource> _local;
            private readonly LocalDevice[] _devices;
            private readonly Dictionary<string, DesignDevice> _designDevices = new Dictionary<string, DesignDevice>(StringComparer.Ordinal);
            private readonly Dictionary<(OsdDesignTile, string), OverlayDesignImportIssue> _issues = new Dictionary<(OsdDesignTile, string), OverlayDesignImportIssue>();

            public Mapping(OsdDesignDocument document, OverlayTelemetrySource[] sources)
            {
                _document = document;
                _sources = sources;
                _local = sources.ToDictionary(source => source.Id, StringComparer.Ordinal);
                _devices = sources.Where(source => source.IsHardwareSensor && !string.IsNullOrEmpty(source.DeviceKey))
                    .GroupBy(source => source.DeviceKey, StringComparer.Ordinal).Select(device =>
                    {
                        var first = device.First();
                        var kind = OverlaySensorSources.DeviceKind(first.HardwareType);
                        return new LocalDevice
                        {
                            Key = device.Key, Name = first.HardwareName, Kind = kind,
                            Vendor = OverlaySensorSources.Vendor(first.HardwareType, first.HardwareName),
                            Sources = device.ToArray(),
                            // A name a device reports twice identifies no single reading.
                            Readings = device.GroupBy(source => ReadingOf(source.SensorType, source.Name), StringComparer.Ordinal)
                                .Where(reading => reading.Count() == 1).ToDictionary(reading => reading.Key, reading => reading.First(), StringComparer.Ordinal)
                        };
                    }).ToArray();
            }

            public OverlayDesignImportResult Run()
            {
                bool generic = IsGeneric(_document);
                if (!generic)
                {
                    var tiles = _document.EnumerateTiles().ToArray();
                    CollectDesignDevices(tiles);
                    foreach (var device in _designDevices.Values) Resolve(device);
                    foreach (var tile in tiles)
                    {
                        foreach (var (field, key) in ReadingKeys(tile).ToArray())
                        {
                            string mapped = MapReading(key, out var issue);
                            if (issue.HasValue) _issues[(tile, field)] = issue.Value;
                            else if (field == "key") tile.Key = mapped;
                            else tile.SecondaryKey = mapped;
                        }
                        if (tile.Kind == "group") MapGroup(tile);
                    }
                }
                // Semantic keys and roles bind as for a template, wherever the design came from.
                _document.BindPresetSources(OverlayDesignSources.ToEditorSources(_sources));
                _document.Validate();
                return new OverlayDesignImportResult
                {
                    Name = _document.Name,
                    DesignJson = _document.ToJson(),
                    IsGeneric = generic,
                    Devices = _designDevices.Values.Select(device => device.Mapping).ToArray(),
                    Unresolved = CollectUnresolved()
                };
            }

            private void Note(string key)
            {
                if (!OverlaySensorSources.TryParseSensorId(key, out string hardware, out string type, out string name)) return;
                if (!_designDevices.TryGetValue(hardware, out var device)) _designDevices[hardware] = device = new DesignDevice { Name = hardware };
                device.Readings.Add(ReadingOf(type, name));
                device.SensorNames.Add(name);
                if (_local.TryGetValue(key, out var source)) device.Owners.Add(source.DeviceKey);
                else device.ReadsElsewhere = true;
            }

            private void CollectDesignDevices(IEnumerable<OsdDesignTile> tiles)
            {
                foreach (var tile in tiles)
                {
                    foreach (var (_, key) in ReadingKeys(tile)) Note(key);
                    if (tile.Kind != "group") continue;
                    foreach (var member in tile.GroupMembers ?? new List<OsdGroupMember>())
                        if (member != null) Note(member.Key);
                    // A bound group names its device with the hardware type ("device:Cpu/..."), which tells its kind and vendor.
                    string deviceKey = tile.GroupKey?.Split('|')[0];
                    if (OverlaySensorSources.TryParseDeviceKey(deviceKey, out string hardwareType, out string hardware)
                        && _designDevices.TryGetValue(hardware, out var device))
                        device.HardwareType ??= hardwareType;
                }
            }

            private void Resolve(DesignDevice design)
            {
                var sameModel = _devices.Where(device => device.Name == design.Name).ToArray();
                // The device whose sensors the design already reads here stays its device, also beside a twin;
                // a design that reads both twins and nothing else stays on them.
                var owners = sameModel.Where(device => design.Owners.Contains(device.Key)).ToArray();
                var kind = sameModel.Length > 0 ? sameModel[0].Kind
                    : design.HardwareType != null ? OverlaySensorSources.DeviceKind(design.HardwareType)
                    : OverlaySensorSources.InferDeviceKind(design.Name, design.SensorNames);
                string vendor = sameModel.Length > 0 ? sameModel[0].Vendor
                    : design.HardwareType != null ? OverlaySensorSources.Vendor(design.HardwareType, design.Name)
                    : OverlaySensorSources.InferVendor(kind, design.Name);
                LocalDevice target = null;
                OverlayDesignImportIssue? issue = null;
                if (owners.Length == 1 || owners.Length > 1 && !design.ReadsElsewhere) target = owners[0];
                else if (sameModel.Length == 1) target = sameModel[0];
                // Memory, drives and other devices only map onto the same model.
                else if (sameModel.Length == 0 && kind != OverlayDeviceKind.Cpu && kind != OverlayDeviceKind.Gpu)
                    issue = OverlayDesignImportIssue.NoMatchingDevice;
                else if (sameModel.Length == 0 && vendor == null) issue = OverlayDesignImportIssue.UnknownDevice;
                else
                {
                    // A GPU only stands in for a GPU of the same vendor, a CPU for a CPU of the same vendor.
                    var candidates = sameModel.Length > 1 ? sameModel
                        : _devices.Where(device => device.Kind == kind && device.Vendor == vendor).ToArray();
                    if (candidates.Length == 1) target = candidates[0];
                    else if (candidates.Length == 0) issue = OverlayDesignImportIssue.NoMatchingDevice;
                    else
                    {
                        // Of several, the device that reports most of the design's readings; a tie is not guessed.
                        var ranked = candidates.Select(device => (Device: device, Score: design.Readings.Count(device.Readings.ContainsKey)))
                            .OrderByDescending(candidate => candidate.Score).ToArray();
                        if (ranked[0].Score > ranked[1].Score) target = ranked[0].Device;
                        else issue = OverlayDesignImportIssue.AmbiguousDevice;
                    }
                }
                design.Target = target;
                design.Mapping = new OverlayDesignDeviceMapping(design.Name, target?.Name, kind, vendor, issue);
            }

            private string MapReading(string key, out OverlayDesignImportIssue? issue)
            {
                issue = null;
                // This machine's own sources, semantic keys, metrics and roles need no mapping.
                if (_local.ContainsKey(key) || !IsHardwareKey(key)) return key;
                if (!OverlaySensorSources.TryParseSensorId(key, out string hardware, out string type, out string name))
                {
                    issue = OverlayDesignImportIssue.UnknownDevice;
                    return key;
                }
                var design = _designDevices[hardware];
                var target = design.Target;
                if (target == null)
                {
                    issue = design.Mapping.Issue;
                    return key;
                }
                if (target.Readings.TryGetValue(ReadingOf(type, name), out var source)) return source.Id;
                // A reading another model of the vendor names differently: the first name of the same meaning.
                var rule = OverlaySemanticKeys.FindRule(target.Kind, type, name);
                foreach (string candidate in rule?.Names ?? Array.Empty<string>())
                    if (target.Readings.TryGetValue(ReadingOf(rule.SensorType, candidate), out source)) return source.Id;
                issue = OverlaySensorSources.TryGetCoreReading(type, name, out var core)
                    && !target.HasCoreType(core.Family, OverlaySensorSources.NormalizeCoreType(core.CoreType) ?? string.Empty)
                    ? OverlayDesignImportIssue.MissingCoreType : OverlayDesignImportIssue.MissingReading;
                return key;
            }

            private void MapGroup(OsdDesignTile tile)
            {
                var members = (tile.GroupMembers ?? new List<OsdGroupMember>()).Where(member => member != null).ToArray();
                // A role binds as a template does; this machine's own members stay.
                if (members.Length == 0 || tile.GroupKey?.StartsWith("role:", StringComparison.Ordinal) == true
                    || members.All(member => _local.ContainsKey(member.Key))) return;
                var parsed = members.Select(member => (Member: member,
                    Parsed: OverlaySensorSources.TryParseSensorId(member.Key, out string hardware, out string type, out string name)
                        ? (Hardware: hardware, Type: type, Name: name) : default)).ToArray();
                var devices = parsed.Select(item => item.Parsed.Hardware).Distinct(StringComparer.Ordinal).ToArray();
                if (devices.Length != 1 || devices[0] == null || !_designDevices.TryGetValue(devices[0], out var design))
                {
                    _issues[(tile, "group")] = OverlayDesignImportIssue.UnknownDevice;
                    return;
                }
                if (design.Target == null)
                {
                    _issues[(tile, "group")] = design.Mapping.Issue ?? OverlayDesignImportIssue.NoMatchingDevice;
                    return;
                }
                var readings = parsed.Select(item => OverlaySensorSources.TryGetCoreReading(item.Parsed.Type, item.Parsed.Name, out var reading)
                    ? reading : (OverlayCoreReading?)null).ToArray();
                var families = readings.Select(reading => reading?.Family).Distinct(StringComparer.Ordinal).ToArray();
                var target = design.Target;
                var familySources = families.Length == 1 && families[0] != null && target.Kind == OverlayDeviceKind.Cpu
                    ? target.Sources.Where(source => source.GroupKey == target.Key + "/" + families[0]).ToArray()
                    : Array.Empty<OverlayTelemetrySource>();
                if (familySources.Length == 0)
                {
                    _issues[(tile, "group")] = OverlayDesignImportIssue.MissingReading;
                    return;
                }
                // The core types the group shows; a type whose members are all hidden is not shown.
                var types = parsed.Zip(readings, (item, reading) => (item.Member, Type: OverlaySensorSources.NormalizeCoreType(
                        item.Member.CoreType ?? reading?.CoreType) ?? string.Empty))
                    .Where(item => item.Member.IsEnabled).Select(item => item.Type).ToHashSet(StringComparer.Ordinal);
                if (types.Count == 0) return;
                if (!types.IsSubsetOf(familySources.Select(source => OverlaySensorSources.NormalizeCoreType(source.CoreType) ?? string.Empty)))
                {
                    _issues[(tile, "group")] = OverlayDesignImportIssue.MissingCoreType;
                    return;
                }
                if (!Rebind(tile, families[0], types, familySources))
                    _issues[(tile, "group")] = OverlayDesignImportIssue.MissingReading;
            }

            // Binds the group like a template role on this machine's family, filtered to the group's core types,
            // and keeps the labels and hidden members of readings of the same name.
            private bool Rebind(OsdDesignTile tile, string family, HashSet<string> types, OverlayTelemetrySource[] familySources)
            {
                string single = types.Count == 1 ? types.Single() : null;
                bool byRole = !string.IsNullOrEmpty(single);
                var bound = byRole ? familySources : familySources
                    .Where(source => types.Contains(OverlaySensorSources.NormalizeCoreType(source.CoreType) ?? string.Empty)).ToArray();
                var frame = _document.TileFrame(tile);
                var scratch = new OsdDesignDocument
                {
                    Name = _document.Name, IsCanvas = _document.IsCanvas, Width = _document.Width, Scale = _document.Scale,
                    GridSize = _document.GridSize, Columns = _document.Columns, Gap = _document.Gap, Padding = _document.Padding,
                    CanvasHeight = 4096
                };
                var copy = tile.Copy();
                copy.Padding = _document.EffectivePadding(tile);
                if (_document.IsCanvas)
                {
                    copy.X = 0; copy.Y = 0;
                    copy.FrameWidth = frame.Width; copy.FrameHeight = frame.Height;
                }
                copy.Key = copy.GroupKey = "role:" + family + (byRole ? "|" + single : string.Empty);
                scratch.Tiles.Add(copy);
                var editorSources = OverlayDesignSources.ToEditorSources(bound);
                foreach (var source in editorSources) source.SemanticKey = null;
                scratch.BindPresetSources(editorSources);
                if (copy.GroupKey.StartsWith("role:", StringComparison.Ordinal)) return false;

                var previous = tile.GroupMembers.Where(member => member != null)
                    .GroupBy(member => ReadingOf(member.Key), StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
                foreach (var member in copy.GroupMembers)
                {
                    if (!previous.TryGetValue(ReadingOf(member.Key), out var old)) continue;
                    member.Label = old.Label;
                    member.IsEnabled = old.IsEnabled;
                }
                var state = (tile.Key, tile.GroupKey, tile.Unit, tile.GroupMembers, tile.GroupColumns, tile.GroupBarWidth, tile.GroupBarHeight, tile.GroupByCoreType);
                tile.Key = copy.Key; tile.GroupKey = copy.GroupKey; tile.Unit = copy.Unit; tile.GroupMembers = copy.GroupMembers;
                tile.GroupColumns = copy.GroupColumns; tile.GroupBarWidth = copy.GroupBarWidth;
                tile.GroupBarHeight = copy.GroupBarHeight; tile.GroupByCoreType = copy.GroupByCoreType;
                try
                {
                    _document.EnsureGroupFits(tile);
                }
                catch (FormatException)
                {
                    // More members than the canvas can hold: keep the group as it came.
                    (tile.Key, tile.GroupKey, tile.Unit, tile.GroupMembers, tile.GroupColumns, tile.GroupBarWidth, tile.GroupBarHeight, tile.GroupByCoreType) = state;
                    return false;
                }
                return true;
            }

            private IReadOnlyList<OverlayDesignUnresolvedReading> CollectUnresolved()
            {
                var unresolved = new List<OverlayDesignUnresolvedReading>();
                foreach (var tile in _document.EnumerateTiles())
                {
                    foreach (var (field, key) in ReadingKeys(tile))
                    {
                        if (_local.ContainsKey(key)) continue;
                        unresolved.Add(new OverlayDesignUnresolvedReading(tile.Label, key,
                            _issues.TryGetValue((tile, field), out var issue) ? issue : OverlayDesignImportIssue.MissingReading));
                    }
                    if (tile.Kind != "group") continue;
                    bool role = tile.GroupKey?.StartsWith("role:", StringComparison.Ordinal) == true;
                    if (!role && tile.GroupMembers?.Any(member => member?.IsEnabled == true && !_local.ContainsKey(member.Key)) != true) continue;
                    unresolved.Add(new OverlayDesignUnresolvedReading(tile.Label, tile.GroupKey,
                        _issues.TryGetValue((tile, "group"), out var groupIssue) ? groupIssue : role ? RoleIssue(tile.GroupKey) : OverlayDesignImportIssue.MissingReading));
                }
                return unresolved;
            }

            // A role of one core type ("role:cpu-loads|P") whose family this machine's CPU reports without that type.
            private OverlayDesignImportIssue RoleIssue(string role)
            {
                int separator = role.LastIndexOf('|');
                string type = separator < 0 ? null : OverlaySensorSources.NormalizeCoreType(role.Substring(separator + 1));
                if (type == null) return OverlayDesignImportIssue.MissingReading;
                string family = role.Substring("role:".Length, separator - "role:".Length);
                return _sources.Any(source => source.GroupKey?.EndsWith("/" + family, StringComparison.Ordinal) == true)
                    && !_sources.Any(source => source.GroupKey?.EndsWith("/" + family, StringComparison.Ordinal) == true
                        && OverlaySensorSources.NormalizeCoreType(source.CoreType) == type)
                    ? OverlayDesignImportIssue.MissingCoreType : OverlayDesignImportIssue.MissingReading;
            }
        }
    }
}
