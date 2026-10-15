using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Integration;
using CapFrameX.OSD.Interop;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace CapFrameX.View.OverlayDesignAuthoring
{
    /// <summary>
    /// Automation access to the tile profile library. It writes through the same store, the same
    /// strict editor canonicalization and the same validation as the tile editor, then refreshes
    /// the runtime design and the open editor exactly as an editor save does.
    /// </summary>
    public sealed class OverlayDesignAuthoringService : IOverlayDesignAuthoringService
    {
        private static readonly TimeSpan LiveValuesTimeout = TimeSpan.FromSeconds(3);
        private readonly string _configurationFolder;
        private readonly IOverlayDesignService _designService;
        private readonly IOverlayTelemetryService _telemetry;
        private readonly Dispatcher _dispatcher;
        private readonly IOverlayDesignEditorHost _editor;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        /// <param name="dispatcher">The UI thread that owns the editor and the native preview; null runs inline.</param>
        public OverlayDesignAuthoringService(string configurationFolder, IOverlayDesignService designService,
            IOverlayTelemetryService telemetry, Dispatcher dispatcher, IOverlayDesignEditorHost editor)
        {
            _configurationFolder = configurationFolder ?? throw new ArgumentNullException(nameof(configurationFolder));
            _designService = designService ?? throw new ArgumentNullException(nameof(designService));
            _telemetry = telemetry;
            _dispatcher = dispatcher;
            _editor = editor;
        }

        public Task<OverlayDesignLibraryState> GetLibraryAsync() =>
            RunAsync(() => Task.FromResult(CreateLibraryState(OpenStore())));

        public async Task<OverlayDesignProfileContent> GetProfileAsync(string profile)
        {
            var sources = await LoadSourcesAsync().ConfigureAwait(false);
            return await RunAsync(() =>
            {
                var saved = Resolve(OpenStore(), profile);
                return Task.FromResult(CreateContent(saved, new List<string>(), sources));
            }).ConfigureAwait(false);
        }

        public async Task<OverlayDesignProfileContent> CreateProfileAsync(string name, string documentJson, string templateId, string copyFromProfile)
        {
            int given = new[] { documentJson, templateId, copyFromProfile }.Count(value => !string.IsNullOrWhiteSpace(value));
            if (given != 1)
                throw new OverlayDesignAuthoringException("Give exactly one starting point: a tile document, a templateId or copyFromProfile.");
            name = ValidateName(name);
            var input = string.IsNullOrWhiteSpace(documentJson) ? null : ParseDocument(documentJson);
            var sources = await LoadSourcesAsync().ConfigureAwait(false);
            return await RunAsync(async () =>
            {
                EnsureEditorAllowsChanges();
                var store = OpenStore();
                var existing = store.Profiles.FirstOrDefault(profile => string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                    throw new OverlayDesignAuthoringException($"A tile profile named '{existing.Name}' already exists. Choose another name, or change that profile.");
                var warnings = new List<string>();
                OsdDesignDocument document;
                if (input != null)
                {
                    document = OverlayTileDocument.FromJson(input, name, sources, warnings);
                }
                else if (!string.IsNullOrWhiteSpace(templateId))
                {
                    var template = OsdDesignPresets.All.FirstOrDefault(preset => string.Equals(preset.Id, templateId.Trim(), StringComparison.OrdinalIgnoreCase)
                            || string.Equals(preset.Name, templateId.Trim(), StringComparison.OrdinalIgnoreCase))
                        ?? throw new OverlayDesignAuthoringException($"There is no template '{templateId}'. cfx_list_tile_templates lists them.");
                    document = template.CreateDocument();
                    document.Name = name;
                    document.BindPresetSources(sources);
                    PrepareTemplateCanvas(document);
                    OverlayTileDocument.Validate(document);
                }
                else
                {
                    document = OsdDesignDocument.FromJson(Resolve(store, copyFromProfile).DesignJson);
                    document.Name = name;
                }
                var created = store.Create(name, Serialize(document, name));
                Log.Information("MCP tile authoring: created profile '{name}' ({id})", created.Name, created.Id);
                return await AfterWriteAsync(created, warnings, sources).ConfigureAwait(true);
            }).ConfigureAwait(false);
        }

        public async Task<OverlayDesignProfileContent> UpdateProfileAsync(string profile, string documentJson)
        {
            var input = ParseDocument(documentJson);
            var sources = await LoadSourcesAsync().ConfigureAwait(false);
            return await RunAsync(async () =>
            {
                EnsureEditorAllowsChanges();
                var store = OpenStore();
                var current = Resolve(store, profile);
                var warnings = new List<string>();
                var document = OverlayTileDocument.FromJson(input, current.Name, sources, warnings);
                var saved = store.Save(current.Id, Serialize(document, current.Name));
                Log.Information("MCP tile authoring: replaced profile '{name}' ({id})", saved.Name, saved.Id);
                return await AfterWriteAsync(saved, warnings, sources).ConfigureAwait(true);
            }).ConfigureAwait(false);
        }

        public async Task<OverlayDesignProfileContent> EditProfileAsync(string profile, string operationsJson)
        {
            JArray operations;
            try
            {
                operations = JArray.Parse(operationsJson ?? "");
            }
            catch (JsonException error)
            {
                throw new OverlayDesignAuthoringException("'operations' must be a JSON array of edit operations: " + error.Message, error);
            }
            var sources = await LoadSourcesAsync().ConfigureAwait(false);
            return await RunAsync(async () =>
            {
                EnsureEditorAllowsChanges();
                var store = OpenStore();
                var current = Resolve(store, profile);
                var edited = OverlayTileDocument.ApplyOperations(
                    OverlayTileDocument.ToJson(OsdDesignDocument.FromJson(current.DesignJson)), operations);
                var warnings = new List<string>();
                var document = OverlayTileDocument.FromJson(edited, current.Name, sources, warnings);
                var saved = store.Save(current.Id, Serialize(document, current.Name));
                Log.Information("MCP tile authoring: applied {count} edit(s) to profile '{name}' ({id})", operations.Count, saved.Name, saved.Id);
                return await AfterWriteAsync(saved, warnings, sources).ConfigureAwait(true);
            }).ConfigureAwait(false);
        }

        public Task<OverlayDesignProfileInfo> RenameProfileAsync(string profile, string name)
        {
            name = ValidateName(name);
            return RunAsync(async () =>
            {
                EnsureEditorAllowsChanges();
                var store = OpenStore();
                var current = Resolve(store, profile);
                var renamed = store.Rename(current.Id, name);
                Log.Information("MCP tile authoring: renamed profile '{old}' to '{name}' ({id})", current.Name, renamed.Name, renamed.Id);
                RefreshRuntime(new List<string>());
                await ReloadEditorAsync(renamed.Id).ConfigureAwait(true);
                return Describe(renamed, EditorState);
            });
        }

        public Task<OverlayDesignLibraryState> DeleteProfileAsync(string profile)
        {
            return RunAsync(async () =>
            {
                EnsureEditorAllowsChanges();
                var store = OpenStore();
                var current = Resolve(store, profile);
                if (store.Profiles.Count < 2)
                    throw new OverlayDesignAuthoringException("The last tile profile cannot be deleted.");
                // The runtime service deletes atomically and returns an active design to row overlay.
                _designService.Delete(current.Id);
                Log.Information("MCP tile authoring: deleted profile '{name}' ({id})", current.Name, current.Id);
                store = OpenStore();
                string editorProfile = EditorState.ProfileId;
                await ReloadEditorAsync(editorProfile != null && editorProfile != current.Id
                    ? editorProfile : store.ActiveProfileId).ConfigureAwait(true);
                return CreateLibraryState(store);
            });
        }

        public Task<OverlayDesignLibraryState> ActivateProfileAsync(string profile)
        {
            return RunAsync(() =>
            {
                if (!_designService.CanActivate)
                    throw new OverlayDesignAuthoringException(
                        "Tile profiles need the CapFrameX in-game or hook-free renderer. Select one with cfx_set_osd_options (renderer InGame or HookFree) first.");
                var store = OpenStore();
                var target = Resolve(store, profile);
                _designService.RefreshProfiles();
                _designService.Activate(target.Id);
                Log.Information("MCP tile authoring: activated profile '{name}' ({id})", target.Name, target.Id);
                return Task.FromResult(CreateLibraryState(store));
            });
        }

        public Task<OverlayDesignLibraryState> DeactivateProfileAsync()
        {
            return RunAsync(() =>
            {
                _designService.Deactivate();
                Log.Information("MCP tile authoring: returned the OSD to row overlay");
                return Task.FromResult(CreateLibraryState(OpenStore()));
            });
        }

        public async Task<OverlayDesignPreview> RenderPreviewAsync(string profile, string documentJson, bool liveValues)
        {
            bool hasProfile = !string.IsNullOrWhiteSpace(profile), hasDocument = !string.IsNullOrWhiteSpace(documentJson);
            if (hasProfile == hasDocument)
                throw new OverlayDesignAuthoringException("Preview either a saved profile or an unsaved tile document, not both.");
            var input = hasDocument ? ParseDocument(documentJson) : null;
            var sources = await LoadSourcesAsync().ConfigureAwait(false);
            var warnings = new List<string>();
            Dictionary<string, object> live = null;
            if (liveValues)
            {
                live = await CaptureMetricsAsync().ConfigureAwait(false);
                if (live == null)
                    warnings.Add("No live sensor readings arrived in time; the preview shows demo values instead.");
            }
            return await RunAsync(() =>
            {
                OsdDesignDocument document;
                string name;
                if (input != null)
                {
                    document = OverlayTileDocument.FromJson(input, null, sources, warnings);
                    name = document.Name;
                }
                else
                {
                    var saved = Resolve(OpenStore(), profile);
                    document = OsdDesignDocument.FromJson(saved.DesignJson);
                    name = saved.Name;
                }
                warnings.AddRange(OverlayTileDocument.FindUnboundKeys(document, sources));
                var runtime = OverlayRuntimeDesign.Parse(new string('0', 32), name, Serialize(document, name));
                var metrics = live ?? OverlayDesignPreviewRenderer.DemoMetrics(document, sources);
                // Portable roles that stayed unbound still find their reading, as at runtime.
                foreach (var source in sources.Where(source => source.IsAvailable && !string.IsNullOrEmpty(source.SemanticKey)))
                    if (!metrics.ContainsKey(source.SemanticKey) && metrics.TryGetValue(source.Key, out object value))
                        metrics[source.SemanticKey] = value;
                static double? Number(object value) => value switch
                {
                    double number => number, float number => number, int number => number, long number => number,
                    decimal number => (double)number, _ => null
                };
                var fps = sources.FirstOrDefault(source => source.SemanticKey == "fps");
                double frameRate = (fps != null && metrics.TryGetValue(fps.Key, out object rate) ? Number(rate) : null)
                    ?? (metrics.TryGetValue("fps", out object semantic) ? Number(semantic) : null) ?? 120;
                var image = OverlayDesignPreviewRenderer.Render(runtime.TemplateJson, JsonConvert.SerializeObject(metrics),
                    OverlayDesignPreviewRenderer.FrameSamples(frameRate),
                    document.IsCanvas ? document.Width * document.Scale : 0,
                    document.IsCanvas ? document.CanvasHeight * document.Scale : 0);
                var tiles = OverlayTileDocument.EnumerateWithPaths(document).Where(entry => !entry.Path.Contains('/')).ToArray();
                return Task.FromResult(new OverlayDesignPreview
                {
                    PngBytes = image.PngBytes, Width = image.Width, Height = image.Height, Scale = Math.Round(image.Scale, 3),
                    Values = live == null ? "demo" : "live", ProfileName = name, Warnings = warnings,
                    Tiles = image.Tiles.Select((bounds, index) => new OverlayDesignPreviewTile
                    {
                        Path = index < tiles.Length ? tiles[index].Path : index.ToString(),
                        Kind = index < tiles.Length ? tiles[index].Tile.Kind : null,
                        Label = index < tiles.Length ? tiles[index].Tile.Label : null,
                        X = bounds.X, Y = bounds.Y, Width = bounds.Width, Height = bounds.Height
                    }).ToArray()
                });
            }).ConfigureAwait(false);
        }

        public Task<bool> OpenEditorAsync(string profile)
        {
            if (_editor == null)
                throw new OverlayDesignAuthoringException("The tile editor is not available in this CapFrameX instance.");
            return RunAsync(async () =>
            {
                string id = string.IsNullOrWhiteSpace(profile) ? null : Resolve(OpenStore(), profile).Id;
                var state = _editor.State;
                // Switching would ask the user about their unsaved edits and block this call until
                // they answer. Bring the editor forward unchanged instead.
                bool keep = id != null && state.IsOpen && id != state.ProfileId && (state.HasUnsavedChanges || state.IsBusy);
                await _editor.OpenAsync(keep ? null : id).ConfigureAwait(true);
                return !keep;
            });
        }

        public async Task<OverlayDesignSourceCatalog> GetSourcesAsync()
        {
            var sources = await LoadSourcesAsync().ConfigureAwait(false);
            var groups = OsdSourceGroup.Build(sources.Where(source => source.IsAvailable));
            var membership = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var group in groups)
                foreach (var member in group.Sources)
                    membership.TryAdd(member.Key, group.Key);
            return new OverlayDesignSourceCatalog
            {
                Sources = sources.Select(source => new OverlayDesignSourceInfo
                {
                    Key = source.Key, Name = source.Name, Unit = source.Unit, Category = source.Category,
                    Subcategory = source.Subcategory, Device = source.DeviceName, SemanticKey = source.SemanticKey,
                    IsText = source.IsText, IsAvailable = source.IsAvailable,
                    GroupKey = membership.TryGetValue(source.Key, out string groupKey) ? groupKey : null
                }).ToArray(),
                Groups = groups.Select(group => new OverlayDesignSourceGroupInfo
                {
                    Key = group.Key, Name = group.Name, Category = group.Category, Device = group.DeviceName,
                    Unit = group.Unit, MemberCount = group.Sources.Count
                }).ToArray()
            };
        }

        public IReadOnlyList<OverlayDesignTemplateInfo> GetTemplates() =>
            OsdDesignPresets.All.Select(preset => new OverlayDesignTemplateInfo
            {
                Id = preset.Id, Name = preset.Name, Category = preset.Category, Description = preset.Description,
                Columns = preset.Columns, TileCount = preset.TileCount
            }).ToArray();

        public string GetDesignGuide() => OverlayTileDesignGuide.Text;

        private async Task<T> RunAsync<T>(Func<Task<T>> action)
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_dispatcher == null || _dispatcher.CheckAccess())
                    return await action().ConfigureAwait(false);
                return await _dispatcher.InvokeAsync(action).Task.Unwrap().ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        private OverlayDesignProfileStore OpenStore()
        {
            // The editor's canonicalization: a stored design must reopen as the same tile document.
            var store = new OverlayDesignProfileStore(_configurationFolder, json => OsdDesignDocument.FromJson(json).ToJson());
            OverlayDesignStarterCatalog.EnsureProfiles(store);
            return store;
        }

        private static OverlayDesignProfile Resolve(OverlayDesignProfileStore store, string profile)
        {
            if (string.IsNullOrWhiteSpace(profile))
                throw new OverlayDesignAuthoringException("Name a tile profile by its id or name; cfx_list_tile_profiles lists them.");
            string wanted = profile.Trim();
            return store.Profiles.FirstOrDefault(candidate => string.Equals(candidate.Id, wanted, StringComparison.OrdinalIgnoreCase))
                ?? store.Profiles.FirstOrDefault(candidate => string.Equals(candidate.Name, wanted, StringComparison.OrdinalIgnoreCase))
                ?? throw new OverlayDesignAuthoringException($"There is no tile profile '{wanted}'. Saved profiles: "
                    + string.Join(", ", store.Profiles.Select(candidate => "'" + candidate.Name + "'")) + ".");
        }

        private static string ValidateName(string name)
        {
            name = name?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > OverlayDesignProfileStore.MaximumNameLength
                || name.Any(char.IsControl) || name.IndexOfAny(new[] { '/', '\\' }) >= 0)
                throw new OverlayDesignAuthoringException(
                    $"A profile name needs 1 to {OverlayDesignProfileStore.MaximumNameLength} characters without '/', '\\' or control characters.");
            return name;
        }

        private static JObject ParseDocument(string documentJson)
        {
            try
            {
                return JObject.Parse(documentJson ?? "");
            }
            catch (JsonException error)
            {
                throw new OverlayDesignAuthoringException("The tile document is not a JSON object: " + error.Message, error);
            }
        }

        /// <summary>The design as the store keeps it, rejected up front when the OSD renderers could not load it.</summary>
        private static string Serialize(OsdDesignDocument document, string name)
        {
            string json = document.ToJson();
            try
            {
                OverlayRuntimeDesign.Parse(new string('0', 32), name, json);
            }
            catch (Exception error) when (OverlayTileDocument.IsEditorError(error))
            {
                throw new OverlayDesignAuthoringException("The OSD renderer cannot load this design: " + error.Message, error);
            }
            return json;
        }

        private OverlayDesignEditorState EditorState => _editor?.State ?? default;

        private void EnsureEditorAllowsChanges()
        {
            var state = EditorState;
            if (state.HasUnsavedChanges)
                throw new OverlayDesignAuthoringException(
                    "The tile editor has unsaved changes. Ask the user to save or discard them in the editor first; changing the library now would make the editor's save fail.");
            if (state.IsBusy)
                throw new OverlayDesignAuthoringException(
                    "The tile editor is waiting for the user (a dialog is open or it is closing). Try again once it is idle.");
        }

        private async Task<OverlayDesignProfileContent> AfterWriteAsync(OverlayDesignProfile saved, List<string> warnings,
            IReadOnlyList<OsdTelemetrySource> sources)
        {
            bool overlayUpdated = RefreshRuntime(warnings) && _designService.IsEnabled && _designService.ActiveProfileId == saved.Id;
            bool editorReloaded = await ReloadEditorAsync(saved.Id).ConfigureAwait(true);
            var content = CreateContent(saved, warnings, sources);
            content.OverlayUpdated = overlayUpdated;
            content.EditorReloaded = editorReloaded;
            return content;
        }

        private bool RefreshRuntime(List<string> warnings)
        {
            try
            {
                // Same step as an editor save: an active design picks up the new scene at once.
                _designService.RefreshProfiles();
                return true;
            }
            catch (Exception error)
            {
                Log.Warning(error, "MCP tile authoring: the runtime design refresh failed");
                warnings.Add("The OSD could not reload the profile library: " + error.Message);
                return false;
            }
        }

        private async Task<bool> ReloadEditorAsync(string profileId)
        {
            if (_editor == null || !_editor.State.IsOpen) return false;
            try
            {
                return await _editor.ReloadAsync(profileId).ConfigureAwait(true);
            }
            catch (Exception error)
            {
                Log.Warning(error, "MCP tile authoring: the tile editor could not reload the library");
                return false;
            }
        }

        private OverlayDesignProfileContent CreateContent(OverlayDesignProfile saved, List<string> warnings,
            IReadOnlyList<OsdTelemetrySource> sources)
        {
            var document = OsdDesignDocument.FromJson(saved.DesignJson);
            return new OverlayDesignProfileContent
            {
                Profile = Describe(saved, EditorState, document),
                DocumentJson = OverlayTileDocument.ToJson(document).ToString(Formatting.None),
                Warnings = warnings.Concat(OverlayTileDocument.FindUnboundKeys(document, sources)).Distinct().ToArray()
            };
        }

        private OverlayDesignLibraryState CreateLibraryState(OverlayDesignProfileStore store)
        {
            var editor = EditorState;
            return new OverlayDesignLibraryState
            {
                Profiles = store.Profiles.Select(profile => Describe(profile, editor)).ToArray(),
                ActiveProfileId = _designService.IsEnabled ? _designService.ActiveProfileId : null,
                CanActivate = _designService.CanActivate,
                OverlayStatus = _designService.Status,
                EditorOpen = editor.IsOpen,
                EditorProfileId = editor.ProfileId,
                EditorHasUnsavedChanges = editor.HasUnsavedChanges,
                LibraryWarning = store.RecoveryWarning ?? store.StarterInitializationWarning
            };
        }

        private OverlayDesignProfileInfo Describe(OverlayDesignProfile profile, OverlayDesignEditorState editor, OsdDesignDocument document = null)
        {
            string error = null;
            try
            {
                document ??= OsdDesignDocument.FromJson(profile.DesignJson);
            }
            catch (Exception failure) when (OverlayTileDocument.IsEditorError(failure))
            {
                error = failure.Message;
            }
            return new OverlayDesignProfileInfo
            {
                Id = profile.Id,
                Name = profile.Name,
                ModifiedUtc = profile.ModifiedUtc,
                Layout = document == null ? null : document.IsCanvas ? OverlayTileDocument.CanvasLayout : OverlayTileDocument.GridLayout,
                TileCount = document?.EnumerateTiles().Count() ?? 0,
                IsActiveInOverlay = _designService.IsEnabled && _designService.ActiveProfileId == profile.Id,
                IsOpenInEditor = editor.IsOpen && editor.ProfileId == profile.Id,
                IsUnmodifiedStarter = OverlayDesignStarterCatalog.IsUnmodifiedStarter(profile),
                LoadError = error
            };
        }

        private async Task<IReadOnlyList<OsdTelemetrySource>> LoadSourcesAsync()
        {
            if (_telemetry == null) return Array.Empty<OsdTelemetrySource>();
            try
            {
                return OverlayDesignSources.ToEditorSources(await _telemetry.GetSourcesAsync().ConfigureAwait(false));
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                // Authoring keeps working without a catalog; only key checks and bindings are skipped.
                Log.Warning(error, "MCP tile authoring: the telemetry catalog is unavailable");
                return Array.Empty<OsdTelemetrySource>();
            }
        }

        /// <summary>One current telemetry snapshot; null when none arrives in time.</summary>
        private async Task<Dictionary<string, object>> CaptureMetricsAsync()
        {
            if (_telemetry == null) return null;
            try
            {
                using var lease = _telemetry.AcquirePreview();
                var snapshot = await _telemetry.Snapshots.FirstAsync().Timeout(LiveValuesTimeout).ToTask().ConfigureAwait(false);
                return new Dictionary<string, object>(snapshot.MetricValues, StringComparer.Ordinal);
            }
            catch (Exception error) when (error is TimeoutException || error is InvalidOperationException || error is ObjectDisposedException)
            {
                Log.Debug(error, "MCP tile authoring: no live snapshot for the preview");
                return null;
            }
        }

        /// <summary>The editor's template step: freeze the flowing layout into canvas frames measured by the native renderer.</summary>
        private static void PrepareTemplateCanvas(OsdDesignDocument document)
        {
            try
            {
                using var native = new OsdPreview();
                if (native.CreateBitmap(800, 2000) && native.LoadTemplateJson(document.ToJson()))
                {
                    var bounds = new OsdPreviewTileRect[OsdDesignDocument.MaximumTiles];
                    if (native.GetLayout(out var layout, bounds) == document.Tiles.Count && layout.Scale > 0)
                    {
                        double scale = layout.Scale * document.Scale;
                        document.EnableCanvas(bounds.Take(document.Tiles.Count).Select(b => new System.Windows.Rect(
                            (b.X - layout.X) / scale, (b.Y - layout.Y) / scale, b.Width / scale, b.Height / scale)).ToArray());
                        return;
                    }
                }
            }
            catch (Exception error) when (error is DllNotFoundException || error is EntryPointNotFoundException || error is BadImageFormatException)
            {
            }
            document.EnableCanvas();
        }
    }
}
