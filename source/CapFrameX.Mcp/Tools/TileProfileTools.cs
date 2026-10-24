using CapFrameX.Contracts.Overlay;
using CapFrameX.Mcp.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace CapFrameX.Mcp.Tools
{
    /// <summary>
    /// Creates, edits, previews and activates tile overlay profiles: the designs of the CapFrameX
    /// tile editor. The work happens in <see cref="IOverlayDesignAuthoringService"/>; this class
    /// shapes arguments and results for MCP clients.
    /// </summary>
    [McpServerToolType]
    public class TileProfileTools
    {
        private const string OperationsSchema = @"{
  ""type"": ""array"",
  ""items"": {
    ""type"": ""object"",
    ""properties"": {
      ""op"": { ""type"": ""string"", ""enum"": [""setDocument"", ""addTile"", ""updateTile"", ""removeTile"", ""moveTile""] },
      ""path"": { ""type"": ""string"", ""description"": ""Tile path for updateTile, removeTile and moveTile: indices joined by '/', e.g. \""0\"" or \""2/1\""."" },
      ""parent"": { ""type"": ""string"", ""description"": ""addTile/moveTile: path of the container to insert into; omit for the top level."" },
      ""index"": { ""type"": ""integer"", ""description"": ""addTile/moveTile: position among the siblings (layer order); omit for the end."" },
      ""tile"": { ""type"": ""object"", ""description"": ""addTile: the new tile."" },
      ""values"": { ""type"": ""object"", ""description"": ""setDocument/updateTile: fields to merge; null removes a field (default or automatic value)."" }
    },
    ""required"": [""op""]
  }
}";

        private static readonly JsonSerializer CamelCase = JsonSerializer.Create(new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
        });

        private readonly IOverlayDesignAuthoringService _authoring;

        public TileProfileTools(IOverlayDesignAuthoringService authoring)
        {
            _authoring = authoring;
        }

        [McpServerTool(Name = "cfx_get_tile_design_guide",
            Description = "Read this first before creating or changing tile overlay profiles. Returns the authoring guide for the " +
                "CapFrameX tile overlay (the designs of the tile editor): the workflow, the tile document format, every widget kind " +
                "(metric, bar, gauge, chart, text, icon, image, group, container, classicRows) with its fields and ranges, how keys bind " +
                "to sensors, the edit operations, and design advice.")]
        public string GetTileDesignGuide() => Authoring.GetDesignGuide();

        [McpServerTool(Name = "cfx_list_tile_profiles",
            Description = "Lists the saved tile overlay profiles (id, name, layout, tile count, last change) and which one the OSD " +
                "currently shows, whether a tile profile can be activated with the selected renderer, and the state of the tile " +
                "editor window (open profile, unsaved changes).")]
        public async Task<JObject> ListTileProfiles()
        {
            return ToJson(await Authoring.GetLibraryAsync().ConfigureAwait(false));
        }

        [McpServerTool(Name = "cfx_list_tile_templates",
            Description = "Lists the tile editor's gallery templates (id, name, category, description, columns, tile count). " +
                "Pass a template id to cfx_create_tile_profile to start a new profile from it; hardware roles bind to this machine's sensors.")]
        public JObject ListTileTemplates(
            [Description("Optional case-insensitive filter on name, category and description.")] string search = null)
        {
            var templates = Authoring.GetTemplates()
                .Where(template => Matches(search, template.Id, template.Name, template.Category, template.Description))
                .ToArray();
            return new JObject
            {
                ["count"] = templates.Length,
                ["templates"] = JArray.FromObject(templates, CamelCase),
            };
        }

        [McpServerTool(Name = "cfx_list_tile_sources",
            Description = "Lists the readings a tile can show on this machine: 'key' is the value for a tile's key or secondaryKey, " +
                "'semanticKey' a portable role (fps, gpuTemp, ...) usable as a key too. Also lists sensor families ('groups', e.g. all " +
                "CPU core clocks) whose 'key' fills a group tile via groupKey. Filter with search; all terms must match.")]
        public async Task<JObject> ListTileSources(
            [Description("Space-separated terms matched against key, name, category, subcategory, device, unit and semanticKey, e.g. \"gpu temp\".")] string search = null,
            [Description("Only this category, e.g. Performance, Graphics, Processor, Memory, System, Power, Capture.")] string category = null,
            [Description("Include readings that currently report no value.")] bool includeUnavailable = false,
            [Description("Include sensor families for group tiles.")] bool includeGroups = true,
            [Description("Maximum number of readings returned, 1..1000.")] int limit = 300)
        {
            if (limit < 1 || limit > 1000)
                throw new ArgumentOutOfRangeException(nameof(limit), limit, "Value must be between 1 and 1000.");
            var catalog = await Authoring.GetSourcesAsync().ConfigureAwait(false);
            var sources = catalog.Sources
                .Where(source => includeUnavailable || source.IsAvailable)
                .Where(source => string.IsNullOrWhiteSpace(category) || string.Equals(source.Category, category.Trim(), StringComparison.OrdinalIgnoreCase))
                .Where(source => Matches(search, source.Key, source.Name, source.Category, source.Subcategory, source.Device, source.Unit, source.SemanticKey))
                .ToArray();
            var result = new JObject
            {
                ["total"] = sources.Length,
                ["truncated"] = sources.Length > limit,
                ["sources"] = JArray.FromObject(sources.Take(limit), CamelCase),
            };
            if (includeGroups)
            {
                result["groups"] = JArray.FromObject(catalog.Groups
                    .Where(group => string.IsNullOrWhiteSpace(category) || string.Equals(group.Category, category.Trim(), StringComparison.OrdinalIgnoreCase))
                    .Where(group => Matches(search, group.Key, group.Name, group.Category, group.Device, group.Unit)), CamelCase);
            }
            return result;
        }

        [McpServerTool(Name = "cfx_get_tile_profile",
            Description = "Returns one tile profile as a tile document (see cfx_get_tile_design_guide): document settings and every tile " +
                "with its path, kind, key, frame and style. Also reports keys that match no reading on this machine.")]
        public async Task<JObject> GetTileProfile(
            [Description("Profile id or name (case-insensitive).")] string profile)
        {
            return ToJson(await Authoring.GetProfileAsync(profile).ConfigureAwait(false), includeDocument: true);
        }

        [McpServerTool(Name = "cfx_create_tile_profile",
            Description = "Creates a tile overlay profile from exactly one starting point: a tile document, a gallery template " +
                "(templateId from cfx_list_tile_templates) or a copy of an existing profile. The design is validated like in the tile " +
                "editor; omitted frames are laid out automatically. Returns the profile, a tile outline and warnings. Render it with " +
                "cfx_render_tile_profile to check the result.")]
        public async Task<JObject> CreateTileProfile(
            [Description("Name of the new profile, unique, up to 80 characters.")] string name,
            [Description("Tile document: {layout, width, tileOpacity, fontFamily, tiles: [...]}; every tile carries its own colors. See cfx_get_tile_design_guide.")] JObject document = null,
            [Description("Gallery template id or name to start from.")] string templateId = null,
            [Description("Id or name of a saved profile to copy.")] string copyFromProfile = null,
            [Description("Show the new profile on the CapFrameX OSD right away (needs the in-game or hook-free renderer).")] bool activate = false,
            [Description("Open the tile editor on the new profile so the user can see and fine-tune it.")] bool openInEditor = false,
            [Description("Return the complete tile document instead of the outline.")] bool includeDocument = false)
        {
            var content = await Authoring.CreateProfileAsync(name, document?.ToString(Formatting.None), templateId, copyFromProfile)
                .ConfigureAwait(false);
            var result = ToJson(content, includeDocument);
            await FollowUpAsync(result, content.Profile.Id, activate, openInEditor).ConfigureAwait(false);
            return result;
        }

        [McpServerTool(Name = "cfx_update_tile_profile",
            Description = "Replaces the whole tile document of a profile (start from cfx_get_tile_profile). The profile keeps its id " +
                "and name; 'path' fields are ignored. The editor reloads the profile when it has no unsaved changes, and an active " +
                "profile updates on the OSD immediately. For small changes prefer cfx_edit_tile_profile.")]
        public async Task<JObject> UpdateTileProfile(
            [Description("Profile id or name.")] string profile,
            [Description("The complete new tile document.")] JObject document,
            [Description("Return the complete tile document instead of the outline.")] bool includeDocument = false)
        {
            if (document == null)
                throw new ArgumentException("document is required.", nameof(document));
            return ToJson(await Authoring.UpdateProfileAsync(profile, document.ToString(Formatting.None)).ConfigureAwait(false), includeDocument);
        }

        [McpServerTool(Name = "cfx_edit_tile_profile",
            Description = "Applies edit operations to a profile atomically (all or none): setDocument, addTile, updateTile, removeTile, " +
                "moveTile. Tiles are addressed by path from cfx_get_tile_profile (\"0\", \"2/1\"). Example: " +
                "[{\"op\":\"updateTile\",\"path\":\"0\",\"values\":{\"valueSize\":40,\"color\":\"#FFB000FF\"}}, " +
                "{\"op\":\"addTile\",\"tile\":{\"kind\":\"gauge\",\"key\":\"gpuTemp\",\"label\":\"GPU\",\"unit\":\"°C\"}}]. " +
                "Set frameWidth/frameHeight (or x and y) to null to let them be recomputed.")]
        public async Task<JObject> EditTileProfile(
            [Description("Profile id or name.")] string profile,
            [Description("Edit operations, applied in order."), McpParameterSchema(OperationsSchema)] JArray operations,
            [Description("Return the complete tile document instead of the outline.")] bool includeDocument = false)
        {
            if (operations == null || operations.Count == 0)
                throw new ArgumentException("Provide at least one edit operation.", nameof(operations));
            return ToJson(await Authoring.EditProfileAsync(profile, operations.ToString(Formatting.None)).ConfigureAwait(false), includeDocument);
        }

        [McpServerTool(Name = "cfx_rename_tile_profile",
            Description = "Renames a tile profile. Names are unique (case-insensitive) and at most 80 characters.")]
        public async Task<JObject> RenameTileProfile(
            [Description("Profile id or current name.")] string profile,
            [Description("The new name.")] string name)
        {
            return JObject.FromObject(await Authoring.RenameProfileAsync(profile, name).ConfigureAwait(false), CamelCase);
        }

        [McpServerTool(Name = "cfx_delete_tile_profile",
            Description = "Permanently deletes a tile profile. Confirm with the user first. The last profile cannot be deleted; " +
                "deleting the active profile returns the OSD to the classic row overlay.")]
        public async Task<JObject> DeleteTileProfile(
            [Description("Profile id or name.")] string profile)
        {
            return ToJson(await Authoring.DeleteProfileAsync(profile).ConfigureAwait(false));
        }

        [McpServerTool(Name = "cfx_activate_tile_profile",
            Description = "Shows a tile profile on the CapFrameX OSD and switches the OSD on. Requires the in-game or hook-free " +
                "renderer; with RTSS or Off selected, change the renderer with cfx_set_osd_options first.")]
        public async Task<JObject> ActivateTileProfile(
            [Description("Profile id or name.")] string profile)
        {
            return ToJson(await Authoring.ActivateProfileAsync(profile).ConfigureAwait(false));
        }

        [McpServerTool(Name = "cfx_deactivate_tile_profile",
            Description = "Returns the CapFrameX OSD from the tile profile to the classic row overlay. Visibility and renderer stay unchanged.")]
        public async Task<JObject> DeactivateTileProfile()
        {
            return ToJson(await Authoring.DeactivateProfileAsync().ConfigureAwait(false));
        }

        [McpServerTool(Name = "cfx_render_tile_profile",
            Description = "Renders a saved tile profile, or an unsaved tile document, with the real OSD renderer and returns a PNG " +
                "(dark checkerboard = transparent) plus the pixel bounds of every top-level tile. Use it after each change to check " +
                "layout, clipping, colors and readability. Live mode shows the current sensor readings (a tile without a reading " +
                "shows a dash); demo mode fills every tile with plausible sample values.")]
        public async Task<McpToolResult> RenderTileProfile(
            [Description("Profile id or name. Omit when passing document.")] string profile = null,
            [Description("An unsaved tile document to preview instead of a saved profile.")] JObject document = null,
            [Description("Use current sensor readings (true) or synthetic demo values (false).")] bool liveValues = true)
        {
            var preview = await Authoring.RenderPreviewAsync(profile, document?.ToString(Formatting.None), liveValues).ConfigureAwait(false);
            var description = new JObject
            {
                ["profile"] = preview.ProfileName,
                ["width"] = preview.Width,
                ["height"] = preview.Height,
                ["scale"] = preview.Scale,
                ["values"] = preview.Values,
                ["tiles"] = JArray.FromObject(preview.Tiles, CamelCase),
            };
            if (preview.Warnings.Count > 0)
                description["warnings"] = new JArray(preview.Warnings);
            return new McpToolResult()
                .AddImage(preview.PngBytes, "image/png")
                .AddText(description.ToString(Formatting.None));
        }

        [McpServerTool(Name = "cfx_open_tile_editor",
            Description = "Opens the CapFrameX tile editor window, optionally on one profile, so the user can see the design and " +
                "adjust it by mouse. An editor with unsaved changes keeps them; the user decides.")]
        public async Task<JObject> OpenTileEditor(
            [Description("Profile id or name to show; omit to keep the editor's current profile.")] string profile = null)
        {
            bool shown = await Authoring.OpenEditorAsync(profile).ConfigureAwait(false);
            var result = ToJson(await Authoring.GetLibraryAsync().ConfigureAwait(false));
            if (!shown)
                result["warnings"] = new JArray("The editor kept its current profile because it has unsaved changes; the user decides when to switch.");
            return result;
        }

        private IOverlayDesignAuthoringService Authoring => _authoring
            ?? throw new InvalidOperationException("Tile profile authoring is not available in this CapFrameX instance.");

        private async Task FollowUpAsync(JObject result, string profileId, bool activate, bool openInEditor)
        {
            var warnings = result["warnings"] as JArray ?? new JArray();
            if (activate)
            {
                try
                {
                    await Authoring.ActivateProfileAsync(profileId).ConfigureAwait(false);
                    result["activated"] = true;
                }
                catch (Exception ex) when (ex is OverlayDesignAuthoringException || ex is InvalidOperationException)
                {
                    result["activated"] = false;
                    warnings.Add("The profile was saved but not activated: " + ex.Message);
                }
            }
            if (openInEditor)
            {
                try
                {
                    if (!await Authoring.OpenEditorAsync(profileId).ConfigureAwait(false))
                        warnings.Add("The tile editor kept its current profile because it has unsaved changes.");
                }
                catch (Exception ex) when (ex is OverlayDesignAuthoringException || ex is InvalidOperationException)
                {
                    warnings.Add("The tile editor could not be opened: " + ex.Message);
                }
            }
            if (warnings.Count > 0) result["warnings"] = warnings;
        }

        private static JObject ToJson(OverlayDesignLibraryState state) => JObject.FromObject(state, CamelCase);

        private static JObject ToJson(OverlayDesignProfileContent content, bool includeDocument)
        {
            var document = JObject.Parse(content.DocumentJson);
            var result = new JObject { ["profile"] = JObject.FromObject(content.Profile, CamelCase) };
            if (includeDocument) result["document"] = document;
            else result["outline"] = Outline(document);
            if (content.Warnings.Count > 0) result["warnings"] = new JArray(content.Warnings);
            result["editorReloaded"] = content.EditorReloaded;
            result["overlayUpdated"] = content.OverlayUpdated;
            return result;
        }

        /// <summary>One line per tile: enough to address and position tiles without the full document.</summary>
        private static JObject Outline(JObject document)
        {
            var tiles = new JArray();
            void Visit(JArray items)
            {
                foreach (var tile in items.OfType<JObject>())
                {
                    var line = new JObject { ["path"] = tile["path"], ["kind"] = tile["kind"], ["label"] = tile["label"] };
                    string kind = (string)tile["kind"];
                    if (kind == "metric" || kind == "bar" || kind == "gauge" || kind == "chart") line["key"] = tile["key"];
                    if (kind == "group") line["groupKey"] = tile["groupKey"];
                    if (kind == "text") line["text"] = tile["text"];
                    if (tile["x"] != null)
                        line["frame"] = string.Join(", ", new[] { "x", "y", "frameWidth", "frameHeight" }
                            .Select(field => ((double?)tile[field] ?? 0).ToString("0.###", CultureInfo.InvariantCulture)));
                    tiles.Add(line);
                    if (tile["children"] is JArray children) Visit(children);
                }
            }
            if (document["tiles"] is JArray top) Visit(top);
            var outline = new JObject();
            foreach (var property in document.Properties().Where(property => property.Name != "tiles"))
                outline[property.Name] = property.Value;
            outline["tiles"] = tiles;
            return outline;
        }

        private static bool Matches(string search, params string[] fields)
        {
            if (string.IsNullOrWhiteSpace(search)) return true;
            string text = string.Join(" ", fields.Where(field => !string.IsNullOrEmpty(field)));
            return search.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
                .All(term => text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
