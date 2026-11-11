using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Integration;
using CapFrameX.View.OverlayDesignAuthoring;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    /// <summary>
    /// Exporting tile designs on one simulated PC and importing them on another. A design bound to its
    /// hardware maps a GPU onto a GPU of the same vendor and a CPU onto a CPU of the same vendor, core
    /// readings and core groups onto the same core types only; a generic design binds anywhere.
    /// </summary>
    [TestClass]
    public class OverlayDesignTransferTest
    {
        private string _folder;

        [TestInitialize]
        public void Initialize()
        {
            _folder = Path.Combine(Path.GetTempPath(), "CapFrameX-design-transfer-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (!Directory.Exists(_folder)) return;
            foreach (var file in Directory.GetFiles(_folder)) File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(_folder, true);
        }

        public static IEnumerable<object[]> MachinePairs => SimHardware.Machines
            .SelectMany(source => SimHardware.Machines.Select(target => new object[] { source.Id, target.Id }));

        public static IEnumerable<object[]> TargetMachines => SimHardware.Machines.Select(machine => new object[] { machine.Id });

        // ---------------------------------------------------------------------------------------------
        // A hardware design as a user builds it: concrete sensors, core groups, generic readings.
        // ---------------------------------------------------------------------------------------------

        private enum Part { Cpu, Gpu, Memory, Drive }

        /// <summary>A tile reading: the first of the names the source device reports is used.</summary>
        private sealed record ReadingSpec(string Label, Part Part, string Type, string[] Names, bool CoreReading = false,
            ReadingSpec Secondary = null);

        /// <summary>A planned tile of the design and what it reads on the source machine.</summary>
        private sealed class PlannedTile
        {
            public string Label;
            public SimDevice Device;
            public string Type, Name, Key;
            public bool CoreReading;
            public PlannedTile Secondary;
            public string Family;
            public bool IsGroup => Family != null;
        }

        private static IEnumerable<ReadingSpec> ReadingSpecs(SimMachine machine)
        {
            var cpu = machine.Cpu;
            var first = cpu.Cores[0];
            yield return new ReadingSpec("GPU LOAD", Part.Gpu, "Load", new[] { "GPU Core" });
            yield return new ReadingSpec("GPU TEMP", Part.Gpu, "Temperature", new[] { "GPU Core" });
            yield return new ReadingSpec("GPU HOT SPOT", Part.Gpu, "Temperature", new[] { "GPU Hot Spot" });
            yield return new ReadingSpec("GPU MEMORY TEMP", Part.Gpu, "Temperature", new[] { "GPU Memory Junction", "GPU Memory" });
            yield return new ReadingSpec("GPU POWER", Part.Gpu, "Power", new[] { "GPU TBP", "GPU Power" });
            yield return new ReadingSpec("GPU CLOCK", Part.Gpu, "Clock", new[] { "GPU Core" });
            yield return new ReadingSpec("VRAM", Part.Gpu, "Data", new[] { "GPU Memory Dedicated", "GPU Memory Used", "D3D Shared Memory Used" },
                Secondary: new ReadingSpec("VRAM TOTAL", Part.Gpu, "Data", new[] { "GPU Memory Total" }));
            yield return new ReadingSpec("GPU FAN", Part.Gpu, "Fan", new[] { "GPU Fan", "GPU" });
            yield return new ReadingSpec("CPU LOAD", Part.Cpu, "Load", new[] { "CPU Total" });
            yield return new ReadingSpec("CPU TEMP", Part.Cpu, "Temperature", new[] { "CPU Package", "CPU Package (Tctl/Tdie)" });
            yield return new ReadingSpec("CPU POWER", Part.Cpu, "Power", new[] { "CPU Package" });
            yield return new ReadingSpec("CPU CLOCK", Part.Cpu, "Clock", new[] { "CPU Max" });
            yield return new ReadingSpec("P MAX", Part.Cpu, "Clock", new[] { "CPU P Max" });
            yield return new ReadingSpec("E MAX", Part.Cpu, "Clock", new[] { "CPU E Max" });
            yield return new ReadingSpec("CORE 1 LOAD", Part.Cpu, "Load", SimCpu.LoadNames(first).Take(1).ToArray(), CoreReading: true);
            yield return new ReadingSpec("CORE 1 CLOCK", Part.Cpu, "Clock", new[] { SimCpu.CoreName(first) }, CoreReading: true);
            yield return new ReadingSpec("RAM", Part.Memory, "Data", new[] { "Memory Used" });
            yield return new ReadingSpec("DRIVE TEMP", Part.Drive, "Temperature", new[] { "Temperature" });
        }

        private static SimDevice DeviceOf(SimMachine machine, Part part) => part switch
        {
            Part.Cpu => machine.Cpu, Part.Gpu => machine.Gpus[0], Part.Memory => machine.Memory, _ => machine.Drive
        };

        private static PlannedTile Plan(SimMachine machine, ReadingSpec spec)
        {
            var device = DeviceOf(machine, spec.Part);
            string name = spec.Names.FirstOrDefault(candidate => device.Has(spec.Type, candidate));
            if (name == null) return null;
            return new PlannedTile
            {
                Label = spec.Label, Device = device, Type = spec.Type, Name = name, CoreReading = spec.CoreReading,
                Key = machine.Source(device, spec.Type, name).Id,
                Secondary = spec.Secondary == null ? null : Plan(machine, spec.Secondary)
            };
        }

        private static List<OsdGroupMember> Placeholders(string role) => Enumerable.Range(1, 4).Select(index => new OsdGroupMember
        {
            Key = role + "/" + index, Label = "C" + index, SourceName = "Preview core " + index, CoreIndex = index
        }).ToList();

        /// <summary>
        /// Builds the design on the machine as the designer leaves it: concrete sensor IDs, roles and
        /// semantic keys bound to this machine.
        /// </summary>
        private static OsdDesignDocument HardwareDesign(SimMachine machine, out List<PlannedTile> plan)
        {
            var document = new OsdDesignDocument
            {
                Name = "Hardware design " + machine.Id, IsCanvas = true, Width = 520, Scale = 1, Columns = 2,
                GridSize = 4, Padding = 6, Gap = 6, CanvasHeight = 4000
            };
            plan = ReadingSpecs(machine).Select(spec => Plan(machine, spec)).Where(tile => tile != null).ToList();
            double y = 0;
            int column = 0;
            foreach (var tile in plan)
            {
                document.Tiles.Add(new OsdDesignTile
                {
                    Kind = "metric", Label = tile.Label, Key = tile.Key, SecondaryKey = tile.Secondary?.Key, Unit = "",
                    LabelPosition = "left", ValueSize = 16, Padding = 6,
                    X = column * 264, Y = y, FrameWidth = 256, FrameHeight = 44
                });
                if (++column == 2) { column = 0; y += 48; }
            }
            if (column != 0) y += 48;
            // Generic readings: they bind on this machine as in the designer.
            foreach (var (label, key) in new[] { ("FPS", "fps"), ("GPU NAME", "gpuNameShort") })
            {
                document.Tiles.Add(new OsdDesignTile
                {
                    Kind = "metric", Label = label, Key = key, Unit = "", LabelPosition = "left", ValueSize = 16, Padding = 6,
                    X = column * 264, Y = y, FrameWidth = 256, FrameHeight = 44
                });
                if (++column == 2) { column = 0; y += 48; }
            }
            if (column != 0) y += 48;

            OsdDesignTile Group(string label, string role, string display, double height, string unit = "%", double maximum = 100)
            {
                var tile = new OsdDesignTile
                {
                    Kind = "group", Label = label, Key = role, GroupKey = role, GroupDisplay = display, Unit = unit,
                    Minimum = 0, Maximum = maximum, Padding = 6, GroupMembers = Placeholders(role),
                    X = 0, Y = y, FrameWidth = 520, FrameHeight = height
                };
                if (display == "bar")
                {
                    tile.GroupBarOrientation = "vertical"; tile.GroupShowLabels = false; tile.GroupShowValues = false;
                    tile.GroupBarHeight = 40; tile.GroupColumns = 32;
                }
                else tile.GroupColumns = 16;
                document.Tiles.Add(tile);
                y += height + 8;
                return tile;
            }
            foreach (string type in machine.Cpu.CoreTypes)
                Group(type == null ? "THREADS" : type + "-CORES", type == null ? "role:cpu-loads" : "role:cpu-loads|" + type, "bar", 80);
            Group("HEATMAP", "role:cpu-loads", "heatmap", 120);
            Group("EFFECTIVE", "role:cpu-effective-clocks", "bar", 80, "MHz", 6000);
            var editorSources = OverlayDesignSources.ToEditorSources(machine.Catalog);
            document.CanvasHeight = Math.Max(y + 400, 1200);
            document.BindPresetSources(editorSources);

            // A user's group of every thread load, with the last core type hidden on hybrid CPUs.
            var family = OsdSourceGroup.Build(editorSources).Single(group => group.Sources.All(source =>
                source.GroupKey?.EndsWith("/cpu-loads", StringComparison.Ordinal) == true));
            var mixed = family.CreateTile(1);
            mixed.Label = "MIXED";
            mixed.GroupShowLabels = false;
            mixed.X = 0;
            mixed.Y = document.Tiles.Max(tile => tile.Y.Value + tile.FrameHeight.Value) + 8;
            mixed.FrameWidth = 520;
            mixed.FrameHeight = 64;
            if (machine.Cpu.Hybrid)
                foreach (var member in mixed.GroupMembers.Where(member => member.CoreType == machine.Cpu.CoreTypes.Last()))
                    member.IsEnabled = false;
            document.Tiles.Add(mixed);
            document.EnsureGroupFits(mixed);
            document.CanvasHeight = document.Tiles.Max(tile => tile.Y.Value + tile.FrameHeight.Value) + 8;
            document.Validate();

            foreach (var group in document.Tiles.Where(tile => tile.Kind == "group"))
                plan.Add(new PlannedTile
                {
                    Label = group.Label, Device = machine.Cpu,
                    Family = group.Label == "EFFECTIVE" ? "cpu-effective-clocks" : "cpu-loads"
                });
            return document;
        }

        // ---------------------------------------------------------------------------------------------
        // The rules, stated independently of the mapper.
        // ---------------------------------------------------------------------------------------------

        private static string Type(string coreType) => OverlaySensorSources.NormalizeCoreType(coreType) ?? "";

        /// <summary>
        /// The device a source device stands for on the target: the same model, else for a CPU the CPU of the
        /// same vendor, for a GPU the GPU of the same vendor that reports most of the design's readings.
        /// </summary>
        private static SimDevice TargetDevice(SimDevice device, SimMachine target, IEnumerable<(string Type, string Name)> readings,
            out OverlayDesignImportIssue? issue)
        {
            issue = null;
            var sameModel = target.Devices.Where(candidate => candidate.Name == device.Name).ToArray();
            if (sameModel.Length == 1) return sameModel[0];
            SimDevice[] candidates = device switch
            {
                SimCpu => new SimDevice[] { target.Cpu }.Where(cpu => cpu.Vendor == device.Vendor).ToArray(),
                SimGpu => target.Gpus.Where(gpu => gpu.Vendor == device.Vendor).ToArray(),
                _ => Array.Empty<SimDevice>()
            };
            if (candidates.Length == 1) return candidates[0];
            if (candidates.Length == 0)
            {
                issue = OverlayDesignImportIssue.NoMatchingDevice;
                return null;
            }
            var ranked = candidates.Select(candidate => (Device: candidate, Score: readings.Count(reading => candidate.Has(reading.Type, reading.Name))))
                .OrderByDescending(candidate => candidate.Score).ToArray();
            if (ranked[0].Score > ranked[1].Score) return ranked[0].Device;
            issue = OverlayDesignImportIssue.AmbiguousDevice;
            return null;
        }

        private sealed record Expected(string Key, OverlayDesignImportIssue? Issue);

        private static Expected ExpectReading(PlannedTile tile, SimMachine target, List<PlannedTile> plan)
        {
            // A sensor the target reports stays.
            if (target.Catalog.Any(source => source.Id == tile.Key)) return new Expected(tile.Key, null);
            var readings = plan.Where(other => other.Device == tile.Device && !other.IsGroup)
                .SelectMany(other => other.Secondary == null ? new[] { other } : new[] { other, other.Secondary })
                .Select(other => (other.Type, other.Name)).ToArray();
            var device = TargetDevice(tile.Device, target, readings, out var issue);
            if (device == null) return new Expected(tile.Key, issue);
            if (device.Has(tile.Type, tile.Name)) return new Expected(target.Source(device, tile.Type, tile.Name).Id, null);
            var kind = OverlaySensorSources.DeviceKind(device.HardwareType);
            var rule = OverlaySemanticKeys.FindRule(kind, tile.Type, tile.Name);
            string equivalent = rule?.Names.FirstOrDefault(name => device.Has(rule.SensorType, name));
            if (equivalent != null) return new Expected(target.Source(device, rule.SensorType, equivalent).Id, null);
            bool coreTypeMissing = tile.CoreReading && OverlaySensorSources.TryGetCoreReading(tile.Type, tile.Name, out var core)
                && !target.Cpu.CoreTypes.Select(Type).Contains(Type(core.CoreType));
            return new Expected(tile.Key, coreTypeMissing ? OverlayDesignImportIssue.MissingCoreType : OverlayDesignImportIssue.MissingReading);
        }

        private sealed record ExpectedGroup(string[] Members, OverlayDesignImportIssue? Issue);

        private static ExpectedGroup ExpectGroup(OsdDesignTile source, string family, SimMachine sourceMachine, SimMachine target)
        {
            var members = source.GroupMembers.Select(member => member.Key).ToArray();
            var device = TargetDevice(sourceMachine.Cpu, target, Array.Empty<(string, string)>(), out var issue);
            if (device == null) return new ExpectedGroup(members, issue);
            if (device.Name == sourceMachine.Cpu.Name) return new ExpectedGroup(members, null);
            var types = source.GroupMembers.Where(member => member.IsEnabled).Select(member => Type(member.CoreType)).ToHashSet();
            var targetFamily = target.Catalog.Where(candidate => candidate.IsHardwareSensor && candidate.HardwareName == device.Name
                && candidate.GroupKey?.EndsWith("/" + family, StringComparison.Ordinal) == true).ToArray();
            if (!types.IsSubsetOf(targetFamily.Select(candidate => Type(candidate.CoreType))))
                return new ExpectedGroup(members, OverlayDesignImportIssue.MissingCoreType);
            return new ExpectedGroup(targetFamily.Where(candidate => types.Contains(Type(candidate.CoreType))).Select(candidate => candidate.Id).ToArray(), null);
        }

        private OverlayDesignImportResult ExportAndImport(string designJson, SimMachine target, string name = "design.json")
        {
            string path = Path.Combine(_folder, name);
            OverlayDesignProfileTransfer.Export(designJson, path);
            return OverlayDesignProfileTransfer.Import(path, target.Catalog);
        }

        [TestMethod]
        [DynamicData(nameof(MachinePairs))]
        public void HardwareDesignMapsOntoTheSameVendorAndCoreTypesOnly(string sourceId, string targetId)
        {
            var source = SimHardware.Machine(sourceId);
            var target = SimHardware.Machine(targetId);
            var design = HardwareDesign(source, out var plan);
            string exported = design.ToJson();

            var result = ExportAndImport(exported, target);

            Assert.IsFalse(result.IsGeneric, "Concrete sensors bind a design to its hardware.");
            Assert.IsTrue(result.IsMapped);
            var imported = OsdDesignDocument.FromJson(result.DesignJson);
            imported.Validate();
            var tiles = imported.EnumerateTiles().ToDictionary(tile => tile.Label);
            var sourceTiles = design.EnumerateTiles().ToDictionary(tile => tile.Label);
            var expectedUnresolved = new List<(string Tile, string Key, OverlayDesignImportIssue Issue)>();
            string context = sourceId + " -> " + targetId + ": ";

            foreach (var tile in plan)
            {
                if (tile.IsGroup)
                {
                    var expectedGroup = ExpectGroup(sourceTiles[tile.Label], tile.Family, source, target);
                    CollectionAssert.AreEquivalent(expectedGroup.Members, tiles[tile.Label].GroupMembers.Select(member => member.Key).ToArray(),
                        context + tile.Label);
                    if (expectedGroup.Issue.HasValue)
                        expectedUnresolved.Add((tile.Label, sourceTiles[tile.Label].GroupKey, expectedGroup.Issue.Value));
                    continue;
                }
                var expected = ExpectReading(tile, target, plan);
                Assert.AreEqual(expected.Key, tiles[tile.Label].Key, context + tile.Label);
                if (expected.Issue.HasValue) expectedUnresolved.Add((tile.Label, tile.Key, expected.Issue.Value));
                if (tile.Secondary == null) continue;
                var secondary = ExpectReading(tile.Secondary, target, plan);
                Assert.AreEqual(secondary.Key, tiles[tile.Label].SecondaryKey, context + tile.Label + " (secondary)");
                if (secondary.Issue.HasValue) expectedUnresolved.Add((tile.Label, tile.Secondary.Key, secondary.Issue.Value));
            }
            // Generic readings stay with this machine's metrics.
            Assert.AreEqual(sourceTiles["FPS"].Key, tiles["FPS"].Key, context + "FPS");
            Assert.AreEqual(sourceTiles["GPU NAME"].Key, tiles["GPU NAME"].Key, context + "GPU NAME");

            CollectionAssert.AreEquivalent(expectedUnresolved.Select(item => item.ToString()).ToArray(),
                result.Unresolved.Select(item => (item.Tile, item.Key, item.Issue).ToString()).ToArray(), context + "unresolved readings");

            // Every reading the design shows now exists here, or is reported.
            var local = target.Catalog.Select(source => source.Id).ToHashSet();
            foreach (var tile in imported.EnumerateTiles().Where(tile => tile.Kind == "group"))
                if (!result.Unresolved.Any(item => item.Tile == tile.Label))
                    Assert.IsTrue(tile.GroupMembers.All(member => local.Contains(member.Key)), context + tile.Label);
            OverlayRuntimeSourceBinder.Bind(OverlayRuntimeDesign.Parse("imported", imported.Name, result.DesignJson), target.Catalog);
        }

        [TestMethod]
        [DynamicData(nameof(MachinePairs))]
        public void ImportedHardwareDesignReturnsToItsOwnMachine(string sourceId, string targetId)
        {
            var source = SimHardware.Machine(sourceId);
            var target = SimHardware.Machine(targetId);
            var design = HardwareDesign(source, out var plan);

            var there = ExportAndImport(design.ToJson(), target, "there.json");
            var back = ExportAndImport(there.DesignJson, source, "back.json");

            // Readings without a source there kept their keys, so on the way back every tile finds its sensor:
            // the source again, or the sensor of the same meaning the target stood in with. Only a machine with
            // several GPUs of the vendor may fail to tell which one a mapped reading came from.
            var local = source.Catalog.Select(candidate => candidate.Id).ToHashSet();
            var returned = OsdDesignDocument.FromJson(back.DesignJson);
            var context = sourceId + " -> " + targetId + " -> " + sourceId + ": ";
            Assert.IsTrue(back.Unresolved.All(item => item.Issue == OverlayDesignImportIssue.AmbiguousDevice),
                context + string.Join(", ", back.Unresolved));
            bool Reported(OsdDesignTile tile, string key) => back.Unresolved.Any(item => item.Tile == tile.Label && item.Key == key);
            foreach (var tile in returned.EnumerateTiles())
            {
                if (tile.Kind == "metric")
                {
                    Assert.IsTrue(local.Contains(tile.Key) || Reported(tile, tile.Key), context + tile.Label + " " + tile.Key);
                    if (tile.SecondaryKey != null)
                        Assert.IsTrue(local.Contains(tile.SecondaryKey) || Reported(tile, tile.SecondaryKey), context + tile.Label + " (secondary)");
                }
                else if (tile.Kind == "group")
                {
                    Assert.IsTrue(tile.GroupMembers.All(member => local.Contains(member.Key)), context + tile.Label);
                }
            }
            if (source.Gpus.Select(gpu => gpu.Vendor).Distinct().Count() == source.Gpus.Length)
                Assert.AreEqual(0, back.Unresolved.Count, context + string.Join(", ", back.Unresolved));
            if (sourceId == targetId)
            {
                Assert.AreEqual(design.ToJson(), there.DesignJson, "A design imported on its own machine stays as it is.");
                Assert.AreEqual(0, there.Unresolved.Count);
                Assert.IsTrue(there.Devices.All(device => device.TargetDevice == device.SourceDevice));
            }
        }

        // ---------------------------------------------------------------------------------------------
        // Starter profiles: generic ones bind anywhere, bound ones follow the rules.
        // ---------------------------------------------------------------------------------------------

        [TestMethod]
        [DynamicData(nameof(TargetMachines))]
        public void GenericStarterBindsOnEveryMachineAsTheDesignerBindsIt(string targetId)
        {
            var target = SimHardware.Machine(targetId);
            var editorSources = OverlayDesignSources.ToEditorSources(target.Catalog);
            var local = target.Catalog.Select(candidate => candidate.Id).ToHashSet();
            int generic = 0;
            foreach (var starter in OverlayDesignStarterCatalog.Profiles)
            {
                if (!OverlayDesignImportMapper.IsGeneric(OsdDesignDocument.FromJson(starter.DesignJson))) continue;
                generic++;
                var result = ExportAndImport(starter.DesignJson, target, "starter.json");
                Assert.IsTrue(result.IsGeneric, starter.PresetId);
                Assert.AreEqual(0, result.Devices.Count, starter.PresetId + ": a generic design maps no device.");

                // Exactly what the designer does when it opens the unmodified starter on this machine.
                var expected = OsdDesignDocument.FromJson(starter.DesignJson);
                expected.BindPresetSources(editorSources);
                Assert.AreEqual(expected.ToJson(), result.DesignJson, starter.PresetId + " on " + targetId);

                Assert.AreEqual(Unbound(OsdDesignDocument.FromJson(result.DesignJson), local), result.Unresolved.Count, starter.PresetId + " on " + targetId);
                Assert.IsTrue(result.Unresolved.All(item => item.Issue is OverlayDesignImportIssue.MissingReading or OverlayDesignImportIssue.MissingCoreType),
                    starter.PresetId + ": a generic design has no device to miss.");
            }
            Assert.IsTrue(generic >= 25, "Most starters are generic.");
        }

        // What a design cannot show on a PC: keys it does not report, roles it cannot bind, groups with absent members.
        private static int Unbound(OsdDesignDocument document, ICollection<string> local) => document.EnumerateTiles().Sum(tile =>
            tile.Kind is "metric" or "bar" or "gauge"
                ? new[] { tile.Key, tile.SecondaryKey }.Count(key => !string.IsNullOrEmpty(key) && !local.Contains(key))
                : tile.Kind == "group" && (tile.GroupKey?.StartsWith("role:", StringComparison.Ordinal) == true
                    || tile.GroupMembers.Any(member => member.IsEnabled && !local.Contains(member.Key))) ? 1 : 0);

        private static IEnumerable<string> HardwareKeys(OsdDesignTile tile) => (tile.Kind is "metric" or "bar" or "gauge"
                ? new[] { tile.Key, tile.SecondaryKey }
                : tile.Kind == "group" ? tile.GroupMembers.Select(member => member.Key) : Enumerable.Empty<string>())
            .Where(OverlayDesignImportMapper.IsHardwareKey);

        private static readonly Dictionary<(string, string), string> BoundStarters = new Dictionary<(string, string), string>();

        // A starter as the designer saves it after opening it on the machine.
        private static string BoundStarter(OverlayDesignStarterProfile starter, SimMachine machine)
        {
            lock (BoundStarters)
            {
                if (BoundStarters.TryGetValue((starter.PresetId, machine.Id), out string json)) return json;
                var document = OsdDesignDocument.FromJson(starter.DesignJson);
                document.BindPresetSources(OverlayDesignSources.ToEditorSources(machine.Catalog));
                return BoundStarters[(starter.PresetId, machine.Id)] = document.ToJson();
            }
        }

        [TestMethod]
        [DynamicData(nameof(MachinePairs))]
        public void StartersBoundOnOneMachineMapOntoTheSameVendorAndCoreTypes(string sourceId, string targetId)
        {
            var source = SimHardware.Machine(sourceId);
            var target = SimHardware.Machine(targetId);
            var sources = source.Catalog.ToDictionary(candidate => candidate.Id);
            var targets = target.Catalog.ToDictionary(candidate => candidate.Id);
            foreach (var starter in OverlayDesignStarterCatalog.Profiles)
            {
                string exported = BoundStarter(starter, source);
                var original = OsdDesignDocument.FromJson(exported);
                var result = OverlayDesignImportMapper.Map(OsdDesignDocument.FromJson(exported), target.Catalog);
                string context = starter.PresetId + " " + sourceId + " -> " + targetId + ": ";
                var imported = OsdDesignDocument.FromJson(result.DesignJson);
                imported.Validate();
                // Every gap is reported, and nothing else.
                Assert.AreEqual(Unbound(imported, targets.Keys), result.Unresolved.Count, context + string.Join(", ", result.Unresolved));
                // A few starters name the hardware they were made on, which no simulated PC has.
                if (sourceId == targetId && original.EnumerateTiles().SelectMany(HardwareKeys).All(sources.ContainsKey))
                    Assert.AreEqual(exported, result.DesignJson, context + "the same machine changes nothing.");

                foreach (var (before, after) in original.EnumerateTiles().Zip(imported.EnumerateTiles()))
                {
                    Assert.AreEqual(before.Label, after.Label, context);
                    if (before.Kind is "metric" or "bar" or "gauge")
                    {
                        foreach (var (oldKey, newKey) in new[] { (before.Key, after.Key), (before.SecondaryKey, after.SecondaryKey) })
                        {
                            if (string.IsNullOrEmpty(oldKey)) continue;
                            if (targets.ContainsKey(oldKey))
                                Assert.AreEqual(oldKey, newKey, context + before.Label + ": a sensor this PC reports stays.");
                            if (oldKey != newKey)
                            {
                                Assert.IsTrue(targets.ContainsKey(newKey), context + newKey);
                                // A semantic key the source machine could not bind binds here like any generic reading.
                                if (OverlayDesignImportMapper.IsHardwareKey(oldKey))
                                    AssertSameKindAndVendor(Traits(oldKey, sources), Traits(newKey, targets), context + before.Label, checkCoreType: true);
                            }
                            else if (!targets.ContainsKey(newKey))
                                Assert.IsTrue(result.Unresolved.Any(item => item.Key == newKey && item.Tile == after.Label), context + "unreported " + newKey);
                        }
                    }
                    else if (before.Kind == "group")
                    {
                        var oldMembers = before.GroupMembers.Select(member => member.Key).ToArray();
                        var newMembers = after.GroupMembers.Select(member => member.Key).ToArray();
                        bool role = before.GroupKey?.StartsWith("role:", StringComparison.Ordinal) == true;
                        if (!role && oldMembers.All(targets.ContainsKey))
                            CollectionAssert.AreEqual(oldMembers, newMembers, context + before.Label + ": cores this PC reports stay.");
                        if (!oldMembers.SequenceEqual(newMembers))
                        {
                            Assert.IsTrue(newMembers.All(targets.ContainsKey), context + before.Label);
                            // A role the source machine could not bind binds here as a template does.
                            if (role) continue;
                            var types = before.GroupMembers.Where(member => member.IsEnabled).Select(member => Type(member.CoreType)).ToHashSet();
                            Assert.IsTrue(after.GroupMembers.All(member => types.Contains(Type(member.CoreType))),
                                context + before.Label + ": only the core types the group showed.");
                            var from = Traits(oldMembers[0], sources);
                            foreach (var member in newMembers)
                                AssertSameKindAndVendor(from, Traits(member, targets), context + before.Label, checkCoreType: false);
                        }
                        else if (after.GroupMembers.Any(member => member.IsEnabled && !targets.ContainsKey(member.Key)))
                            Assert.IsTrue(result.Unresolved.Any(item => item.Tile == after.Label), context + "unreported group " + before.Label);
                    }
                }
            }
        }

        private sealed record ReadingTraits(OverlayDeviceKind Kind, string Vendor, string CoreType);

        // Kind, vendor and core type of a reading: from the PC that reports it, else from its ID as an importing PC reads it.
        private static ReadingTraits Traits(string key, IReadOnlyDictionary<string, OverlayTelemetrySource> catalog)
        {
            if (catalog.TryGetValue(key, out var source))
                return new ReadingTraits(OverlaySensorSources.DeviceKind(source.HardwareType),
                    OverlaySensorSources.Vendor(source.HardwareType, source.HardwareName), source.GroupKey == null ? null : Type(source.CoreType));
            Assert.IsTrue(OverlaySensorSources.TryParseSensorId(key, out string hardware, out string type, out string name), key);
            var kind = OverlaySensorSources.InferDeviceKind(hardware, new[] { name });
            return new ReadingTraits(kind, OverlaySensorSources.InferVendor(kind, hardware),
                OverlaySensorSources.TryGetCoreReading(type, name, out var core) ? Type(core.CoreType) : null);
        }

        private static void AssertSameKindAndVendor(ReadingTraits from, ReadingTraits to, string context, bool checkCoreType)
        {
            Assert.AreEqual(from.Kind, to.Kind, context);
            Assert.AreEqual(from.Vendor, to.Vendor, context);
            if (checkCoreType && from.CoreType != null && to.CoreType != null)
                Assert.AreEqual(from.CoreType, to.CoreType, context + ": a core reading keeps its core type.");
        }

        // ---------------------------------------------------------------------------------------------
        // Focused cases.
        // ---------------------------------------------------------------------------------------------

        [TestMethod]
        public void ExportWritesTheDesignAsUtf8WithoutBomAndReplacesAnExistingFile()
        {
            var machine = SimHardware.Machines[0];
            string json = HardwareDesign(machine, out _).ToJson();
            string path = Path.Combine(_folder, "export.json");
            File.WriteAllText(path, "previous");

            OverlayDesignProfileTransfer.Export(json, path);

            var bytes = File.ReadAllBytes(path);
            Assert.IsFalse(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "No byte order mark.");
            Assert.AreEqual(json, Encoding.UTF8.GetString(bytes));
            Assert.AreEqual(1, Directory.GetFiles(_folder).Length, "No temporary file stays behind.");
        }

        [TestMethod]
        public void FailedExportKeepsThePreviousFileAndLeavesNoTemporaryFile()
        {
            string json = HardwareDesign(SimHardware.Machines[0], out _).ToJson();
            string path = Path.Combine(_folder, "locked.json");
            File.WriteAllText(path, "previous");
            File.SetAttributes(path, FileAttributes.ReadOnly);

            Assert.ThrowsExactly<UnauthorizedAccessException>(() => OverlayDesignProfileTransfer.Export(json, path));

            Assert.AreEqual("previous", File.ReadAllText(path));
            Assert.AreEqual(1, Directory.GetFiles(_folder).Length);
        }

        [TestMethod]
        public void InvalidDesignIsNeitherExportedNorImported()
        {
            string path = Path.Combine(_folder, "invalid.json");
            AssertRejected(() => OverlayDesignProfileTransfer.Export("{\"version\":1}", path));
            Assert.IsFalse(File.Exists(path));

            File.WriteAllText(path, "{ not a design");
            AssertRejected(() => OverlayDesignProfileTransfer.Import(path, SimHardware.Machines[0].Catalog));
        }

        // The editor rejects a broken design as the designer's import does: a format or JSON error.
        private static void AssertRejected(Action action)
        {
            try
            {
                action();
            }
            catch (Exception error) when (error is FormatException || error is System.Text.Json.JsonException)
            {
                return;
            }
            Assert.Fail("The design was accepted.");
        }

        [TestMethod]
        public void OversizedFileIsRejectedBeforeItIsRead()
        {
            // A valid design, padded beyond the 1 MB the import accepts.
            string json = HardwareDesign(SimHardware.Machines[0], out _).ToJson();
            string path = Path.Combine(_folder, "large.json");
            File.WriteAllText(path, json + new string(' ', OsdDesignDocument.MaximumJsonLength), new UTF8Encoding(false));
            Assert.ThrowsExactly<FormatException>(() => OverlayDesignProfileTransfer.Import(path, SimHardware.Machines[0].Catalog));
            File.WriteAllText(path, json, new UTF8Encoding(false));
            Assert.IsNotNull(OverlayDesignProfileTransfer.Import(path, SimHardware.Machines[0].Catalog).DesignJson);
        }

        [TestMethod]
        public void WithoutSourcesTheDesignIsImportedUnchanged()
        {
            string json = HardwareDesign(SimHardware.Machines[0], out _).ToJson();
            string path = Path.Combine(_folder, "unchanged.json");
            OverlayDesignProfileTransfer.Export(json, path);

            var result = OverlayDesignProfileTransfer.Import(path, null);

            Assert.IsFalse(result.IsMapped);
            Assert.IsFalse(result.IsGeneric);
            Assert.AreEqual(json, result.DesignJson);
            Assert.AreEqual(0, result.Unresolved.Count);
        }

        [TestMethod]
        // Both GeForce cards report every reading of the RTX 5070 Ti; the hot spot only the RTX 4060 reports decides for the laptop GPU.
        [DataRow("Ultra5-245K+RTX5070Ti", "Ultra9-285K+RTX5090+RTX4060", OverlayDesignImportIssue.AmbiguousDevice, null)]
        [DataRow("HX370+RTX4060Laptop", "Ultra9-285K+RTX5090+RTX4060", null, "NVIDIA GeForce RTX 4060")]
        // The same model wins over every other GPU of the vendor.
        [DataRow("i9-14900K+RTX4060+UHD770", "Ultra9-285K+RTX5090+RTX4060", null, "NVIDIA GeForce RTX 4060")]
        [DataRow("Ultra9-285K+RTX5090", "R7-9800X3D+RTX5090", null, "NVIDIA GeForce RTX 5090")]
        [DataRow("i5-12400+ArcB580", "R7-9800X3D+ArcB580", null, "Intel(R) Arc(TM) B580 Graphics")]
        // The Radeon card reports more of the design's readings than the Ryzen's integrated GPU.
        [DataRow("R9-9950X3D+RX7900XTX", "R5-7600X+RX9070XT", null, "AMD Radeon RX 9070 XT")]
        // The only GPU of the vendor is an integrated one.
        [DataRow("R9-9950X3D+RX7900XTX", "R7-9800X3D+RTX5090", null, "AMD Radeon(TM) Graphics")]
        [DataRow("i5-12400+ArcB580", "i9-14900K+RTX4060+UHD770", null, "Intel(R) UHD Graphics 770")]
        [DataRow("Ultra7-155H+ArcGraphics", "i5-12400+ArcB580", null, "Intel(R) Arc(TM) B580 Graphics")]
        // Two cards of the model: which one is not guessed.
        [DataRow("R9-9950X3D+RX7900XTX", "R9-9950X3D+2xRX7900XTX", OverlayDesignImportIssue.AmbiguousDevice, null)]
        [DataRow("R5-7600X+RX9070XT", "R9-9950X3D+2xRX7900XTX", OverlayDesignImportIssue.AmbiguousDevice, null)]
        [DataRow("R9-9950X3D+2xRX7900XTX", "R9-9950X3D+RX7900XTX", null, "AMD Radeon RX 7900 XTX")]
        // No GPU of the vendor.
        [DataRow("R9-9950X3D+RX7900XTX", "Ultra9-285K+RTX5090", OverlayDesignImportIssue.NoMatchingDevice, null)]
        [DataRow("Ultra7-155H+ArcGraphics", "R7-9800X3D+RTX5090", OverlayDesignImportIssue.NoMatchingDevice, null)]
        [DataRow("HX370+RTX4060Laptop", "i5-13600K+RX9070XT", OverlayDesignImportIssue.NoMatchingDevice, null)]
        public void GpuMapsOntoItsModelElseOntoTheVendorsGpuThatReportsMostReadings(string sourceId, string targetId,
            OverlayDesignImportIssue? issue, string expectedGpu)
        {
            var source = SimHardware.Machine(sourceId);
            var result = ExportAndImport(HardwareDesign(source, out _).ToJson(), SimHardware.Machine(targetId));
            var gpu = result.Devices.Single(device => device.SourceDevice == source.Gpus[0].Name);
            Assert.AreEqual(OverlayDeviceKind.Gpu, gpu.Kind);
            Assert.AreEqual(source.Gpus[0].Vendor, gpu.Vendor);
            Assert.AreEqual(issue, gpu.Issue);
            Assert.AreEqual(expectedGpu, gpu.TargetDevice);
        }

        [TestMethod]
        public void TwinGpusKeepTheirCardsAndAreNotGuessedFromAnotherPc()
        {
            // One tile per card; only the runtime identifier in the sensor ID tells identical cards apart.
            var twins = SimHardware.Machine("R9-9950X3D+2xRX7900XTX");
            var single = SimHardware.Machine("R9-9950X3D+RX7900XTX");
            var design = new OsdDesignDocument
            {
                Name = "Both cards", IsCanvas = true, Width = 520, Scale = 1, Columns = 2, GridSize = 4, Padding = 6, Gap = 6, CanvasHeight = 64
            };
            foreach (var (gpu, column) in twins.Gpus.Select((gpu, index) => (gpu, index)))
                design.Tiles.Add(new OsdDesignTile
                {
                    Kind = "metric", Label = "GPU " + (column + 1) + " LOAD", Key = twins.Source(gpu, "Load", "GPU Core").Id, Unit = "",
                    LabelPosition = "left", ValueSize = 16, Padding = 6, X = column * 264, Y = 0, FrameWidth = 256, FrameHeight = 44
                });
            design.Validate();
            Assert.IsTrue(design.Tiles.All(tile => tile.Key.Contains(":runtime:")));
            Assert.AreNotEqual(design.Tiles[0].Key, design.Tiles[1].Key);

            var own = ExportAndImport(design.ToJson(), twins, "own.json");
            Assert.AreEqual(design.ToJson(), own.DesignJson, "On its own PC every tile keeps its card.");
            Assert.AreEqual(0, own.Unresolved.Count);
            var cards = own.Devices.Single();
            Assert.AreEqual(SimHardware.Rx7900Xtx.Name, cards.TargetDevice);
            Assert.IsNull(cards.Issue, "Cards the design already reads are not ambiguous.");

            // On a PC with one such card both tiles read it.
            var elsewhere = ExportAndImport(design.ToJson(), single, "single.json");
            string card = single.Source(SimHardware.Rx7900Xtx, "Load", "GPU Core").Id;
            CollectionAssert.AreEqual(new[] { card, card }, OsdDesignDocument.FromJson(elsewhere.DesignJson).Tiles.Select(tile => tile.Key).ToArray());
            Assert.AreEqual(0, elsewhere.Unresolved.Count);

            var back = ExportAndImport(elsewhere.DesignJson, twins, "back.json");
            Assert.AreEqual(2, back.Unresolved.Count);
            Assert.IsTrue(back.Unresolved.All(item => item.Issue == OverlayDesignImportIssue.AmbiguousDevice), "Which of the two cards is not guessed.");
            Assert.AreEqual(OverlayDesignImportIssue.AmbiguousDevice, back.Devices.Single().Issue);
            Assert.AreEqual(elsewhere.DesignJson, back.DesignJson, "The readings keep their keys.");
        }

        [TestMethod]
        public void GroupKeepsCustomLabelsAndHiddenCoresOfTheSameNameAndFollowsTheCoreCount()
        {
            var source = SimHardware.Machine("Ultra9-285K+RTX5090");
            var design = HardwareDesign(source, out _);
            var group = design.Tiles.Single(tile => tile.Label == "P-CORES");
            group.GroupMembers[0].Label = "Favourite core";
            group.GroupMembers[1].IsEnabled = false;
            string first = group.GroupMembers[0].SourceName, second = group.GroupMembers[1].SourceName;

            var result = ExportAndImport(design.ToJson(), SimHardware.Machine("Ultra5-245K+RTX5070Ti"));

            var mapped = OsdDesignDocument.FromJson(result.DesignJson).Tiles.Single(tile => tile.Label == "P-CORES");
            Assert.AreEqual(6, mapped.GroupMembers.Count, "The 245K has six P-cores.");
            Assert.IsTrue(mapped.GroupMembers.All(member => member.CoreType == "P"));
            Assert.AreEqual("Favourite core", mapped.GroupMembers.Single(member => member.SourceName == first).Label);
            Assert.IsFalse(mapped.GroupMembers.Single(member => member.SourceName == second).IsEnabled);
            Assert.IsTrue(mapped.GroupMembers.Where(member => member.SourceName != second).All(member => member.IsEnabled));
            StringAssert.EndsWith(mapped.GroupKey, "|P", "Refreshing the members later adds P-cores only.");
        }

        [TestMethod]
        [DataRow("Ultra7-155H+ArcGraphics", "Ultra9-285K+RTX5090", "LPE-CORES", OverlayDesignImportIssue.MissingCoreType)]
        [DataRow("Ultra9-285K+RTX5090", "i5-12400+ArcB580", "P-CORES", OverlayDesignImportIssue.MissingCoreType)]
        [DataRow("i5-12400+ArcB580", "Ultra9-285K+RTX5090", "THREADS", OverlayDesignImportIssue.MissingCoreType)]
        [DataRow("R7-9800X3D+RTX5090", "HX370+RTX4060Laptop", "THREADS", OverlayDesignImportIssue.MissingCoreType)]
        [DataRow("HX370+RTX4060Laptop", "R9-9950X3D+RX7900XTX", "D-CORES", OverlayDesignImportIssue.MissingCoreType)]
        [DataRow("Ultra9-285K+RTX5090", "R7-9800X3D+RTX5090", "P-CORES", OverlayDesignImportIssue.NoMatchingDevice)]
        [DataRow("R7-9800X3D+RTX5090", "i9-14900K+RTX4060+UHD770", "HEATMAP", OverlayDesignImportIssue.NoMatchingDevice)]
        public void CoreGroupsMapOntoTheSameCoreTypesOfTheSameVendorOnly(string sourceId, string targetId, string label,
            OverlayDesignImportIssue issue)
        {
            var design = HardwareDesign(SimHardware.Machine(sourceId), out _);
            var members = design.Tiles.Single(tile => tile.Label == label).GroupMembers.Select(member => member.Key).ToArray();

            var result = ExportAndImport(design.ToJson(), SimHardware.Machine(targetId));

            var group = OsdDesignDocument.FromJson(result.DesignJson).Tiles.Single(tile => tile.Label == label);
            CollectionAssert.AreEqual(members, group.GroupMembers.Select(member => member.Key).ToArray(), "An unmapped group keeps its members.");
            Assert.AreEqual(issue, result.Unresolved.Single(item => item.Tile == label).Issue);
        }

        [TestMethod]
        public void MixedGroupShowsOnlyTheCoreTypesThatWereVisible()
        {
            // The design hides the E-cores of the 285K; on the 155H the group shows its P-cores, not its E- or LP-E-cores.
            var design = HardwareDesign(SimHardware.Machine("Ultra9-285K+RTX5090"), out _);
            var result = ExportAndImport(design.ToJson(), SimHardware.Machine("Ultra7-155H+ArcGraphics"));
            var mixed = OsdDesignDocument.FromJson(result.DesignJson).Tiles.Single(tile => tile.Label == "MIXED");
            Assert.AreEqual(12, mixed.GroupMembers.Count, "Six P-cores with two threads each.");
            Assert.IsTrue(mixed.GroupMembers.All(member => member.CoreType == "P" && member.IsEnabled));
        }

        [TestMethod]
        public void GenericReadingsOfAHardwareDesignBindWithoutTheVendorRule()
        {
            // A design for an NVIDIA GPU with one portable GPU load tile: on a Radeon PC the portable tile binds,
            // the NVIDIA readings do not.
            var source = SimHardware.Machine("Ultra9-285K+RTX5090");
            var target = SimHardware.Machine("i5-13600K+RX9070XT");
            var design = HardwareDesign(source, out _);
            var portable = design.Tiles.Single(tile => tile.Label == "GPU LOAD").Copy();
            portable.Label = "PORTABLE GPU LOAD";
            portable.Key = "gpuLoad";
            portable.Y = design.CanvasHeight;
            design.CanvasHeight += 48;
            design.Tiles.Add(portable);

            var result = ExportAndImport(design.ToJson(), target);

            var tiles = OsdDesignDocument.FromJson(result.DesignJson).Tiles.ToDictionary(tile => tile.Label);
            Assert.AreEqual(target.Source(SimHardware.Rx9070Xt, "Load", "GPU Core").Id, tiles["PORTABLE GPU LOAD"].Key);
            Assert.AreEqual(OverlayDesignImportIssue.NoMatchingDevice, result.Unresolved.Single(item => item.Tile == "GPU LOAD").Issue);
            Assert.AreEqual("NVIDIA", result.Devices.Single(device => device.Kind == OverlayDeviceKind.Gpu).Vendor);
        }

        [TestMethod]
        public void SameMeaningStandsInForAReadingTheOtherModelNamesDifferently()
        {
            // The RX 9070 XT reports its board power as "GPU TBP", the RX 7900 XTX only as "GPU Power".
            var source = SimHardware.Machine("R5-7600X+RX9070XT");
            var target = SimHardware.Machine("R9-9950X3D+RX7900XTX");
            var design = HardwareDesign(source, out _);
            Assert.AreEqual(source.Source(SimHardware.Rx9070Xt, "Power", "GPU TBP").Id, design.Tiles.Single(tile => tile.Label == "GPU POWER").Key);

            var result = ExportAndImport(design.ToJson(), target);

            var power = OsdDesignDocument.FromJson(result.DesignJson).Tiles.Single(tile => tile.Label == "GPU POWER");
            Assert.AreEqual(target.Source(SimHardware.Rx7900Xtx, "Power", "GPU Power").Id, power.Key);
            Assert.AreEqual("AMD Radeon RX 7900 XTX", result.Devices.Single(device => device.Kind == OverlayDeviceKind.Gpu).TargetDevice);
        }

        [TestMethod]
        public void DrivesAndOtherDevicesOnlyMapOntoTheSameModel()
        {
            var design = HardwareDesign(SimHardware.Machine("Ultra9-285K+RTX5090"), out _);
            var sameDrive = ExportAndImport(design.ToJson(), SimHardware.Machine("R7-9800X3D+RTX5090"), "same.json");
            var otherDrive = ExportAndImport(design.ToJson(), SimHardware.Machine("Ultra5-245K+RTX5070Ti"), "other.json");
            Assert.IsFalse(sameDrive.Unresolved.Any(item => item.Tile == "DRIVE TEMP"), "Both PCs have a Samsung SSD 990 PRO 2TB.");
            Assert.AreEqual(OverlayDesignImportIssue.NoMatchingDevice, otherDrive.Unresolved.Single(item => item.Tile == "DRIVE TEMP").Issue);
            Assert.IsFalse(otherDrive.Unresolved.Any(item => item.Tile == "RAM"), "Memory has the same name on every PC.");
        }

        [TestMethod]
        public void GenericFusionArlOnARyzenReportsTheMissingCoreTypes()
        {
            var starter = OverlayDesignStarterCatalog.Profiles.Single(profile => profile.PresetId == "RTX-50-Core-Ultra-FUSION-ARL");
            var result = ExportAndImport(starter.DesignJson, SimHardware.Machine("R7-9800X3D+RTX5090"));
            Assert.IsTrue(result.IsGeneric);
            CollectionAssert.AreEquivalent(new[] { "P-CORES", "E-CORES" },
                result.Unresolved.Where(item => item.Issue == OverlayDesignImportIssue.MissingCoreType).Select(item => item.Tile).ToArray());
            CollectionAssert.IsSubsetOf(new[] { "P-CORE MAX CLOCK", "E-CORE MAX CLOCK" },
                result.Unresolved.Where(item => item.Issue == OverlayDesignImportIssue.MissingReading).Select(item => item.Tile).ToArray());
            var heatmap = OsdDesignDocument.FromJson(result.DesignJson).EnumerateTiles().Single(tile => tile.GroupDisplay == "heatmap");
            Assert.AreEqual(16, heatmap.GroupMembers.Count, "The heatmap is generic and shows the Ryzen's 16 threads.");
        }

        // ---------------------------------------------------------------------------------------------
        // Identifiers and device names.
        // ---------------------------------------------------------------------------------------------

        [TestMethod]
        [DataRow("NVIDIA GeForce RTX 5090", "Load", "GPU Core")]
        [DataRow("AMD Ryzen 9 7950X3D", "Temperature", "CPU Package (Tctl/Tdie)")]
        [DataRow("Intel Core i9-14900K", "Clock", "Core #1 P Thread #2 (Effective)")]
        [DataRow("Weird/Device: 100% #1", "SmallData", "Name / with: odd % chars")]
        public void SensorIdsRoundTrip(string hardware, string type, string name)
        {
            string stable = hardware + "/" + type.ToLowerInvariant() + "/" + name;
            foreach (string id in new[] { OverlaySensorSources.SensorId(stable), OverlaySensorSources.SensorId(stable, "/gpu-nvidia/0/load/1") })
            {
                Assert.IsTrue(OverlaySensorSources.TryParseSensorId(id, out string parsedHardware, out string parsedType, out string parsedName), id);
                Assert.AreEqual(hardware, parsedHardware);
                Assert.AreEqual(type.ToLowerInvariant(), parsedType);
                Assert.AreEqual(name, parsedName);
            }
            string device = OverlaySensorSources.DeviceKey("GpuNvidia", hardware, "/gpu-nvidia/0");
            Assert.IsTrue(OverlaySensorSources.TryParseDeviceKey(device, out string hardwareType, out string deviceName));
            Assert.AreEqual("GpuNvidia", hardwareType);
            Assert.AreEqual(hardware, deviceName);
            Assert.IsFalse(OverlaySensorSources.TryParseSensorId("gpuLoad", out _, out _, out _));
            Assert.IsFalse(OverlaySensorSources.TryParseSensorId("metric/Framerate", out _, out _, out _));
        }

        [TestMethod]
        public void TheCatalogBuildsTheIdsTheParserReads()
        {
            foreach (var machine in SimHardware.Machines)
                foreach (var source in machine.Catalog.Where(candidate => candidate.IsHardwareSensor))
                {
                    Assert.IsTrue(OverlaySensorSources.TryParseSensorId(source.Id, out string hardware, out string type, out string name), source.Id);
                    Assert.AreEqual(source.HardwareName, hardware, source.Id);
                    Assert.AreEqual(source.SensorType.ToLowerInvariant(), type, source.Id);
                    Assert.AreEqual(source.Name, name, source.Id);
                    Assert.IsTrue(OverlaySensorSources.TryParseDeviceKey(source.DeviceKey, out string hardwareType, out string device), source.DeviceKey);
                    Assert.AreEqual(source.HardwareType, hardwareType);
                    Assert.AreEqual(source.HardwareName, device);
                }
        }

        [TestMethod]
        [DataRow("NVIDIA GeForce RTX 4060 Laptop GPU", "GPU Core", OverlayDeviceKind.Gpu, "NVIDIA")]
        [DataRow("AMD Radeon RX 9070 XT", "GPU Core", OverlayDeviceKind.Gpu, "AMD")]
        [DataRow("AMD Radeon(TM) Graphics", "GPU Core", OverlayDeviceKind.Gpu, "AMD")]
        [DataRow("Intel(R) Arc(TM) B580 Graphics", "GPU Core", OverlayDeviceKind.Gpu, "Intel")]
        [DataRow("Intel(R) UHD Graphics 770", null, OverlayDeviceKind.Gpu, "Intel")]
        [DataRow("Intel Core Ultra 9 285K", "Core #3 E", OverlayDeviceKind.Cpu, "Intel")]
        [DataRow("Intel Core i5-12400", null, OverlayDeviceKind.Cpu, "Intel")]
        [DataRow("AMD Ryzen 7 9800X3D", "CPU Total", OverlayDeviceKind.Cpu, "AMD")]
        [DataRow("AMD Ryzen 7 8700G w/ Radeon 780M Graphics", null, OverlayDeviceKind.Cpu, "AMD")]
        [DataRow("AMD Ryzen AI 9 HX 370 w/ Radeon 890M", "CPU Total", OverlayDeviceKind.Cpu, "AMD")]
        [DataRow("Total Memory", null, OverlayDeviceKind.Memory, null)]
        [DataRow("Samsung SSD 990 PRO 2TB", "Temperature", OverlayDeviceKind.Other, null)]
        public void DeviceKindAndVendorFollowTheNameAndTheSensors(string name, string sensor, OverlayDeviceKind kind, string vendor)
        {
            var actual = OverlaySensorSources.InferDeviceKind(name, sensor == null ? Array.Empty<string>() : new[] { sensor });
            Assert.AreEqual(kind, actual);
            Assert.AreEqual(vendor, OverlaySensorSources.InferVendor(actual, name));
        }
    }
}
