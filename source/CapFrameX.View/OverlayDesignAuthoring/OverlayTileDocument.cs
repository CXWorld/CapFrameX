using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Windows;
using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Controls;
using Newtonsoft.Json.Linq;

namespace CapFrameX.View.OverlayDesignAuthoring
{
    /// <summary>
    /// The tile document exchanged with automation clients: the editor's own document settings
    /// plus its tile model, exactly as the editor stores it in each widget's metadata. Building
    /// a document runs the editor's validation, so whatever is accepted here opens in the editor.
    /// </summary>
    public static class OverlayTileDocument
    {
        public const string CanvasLayout = "canvas";
        public const string GridLayout = "grid";
        private const double DefaultWidth = 520;

        // The editor serializes its tiles with these settings; any other naming would not round-trip.
        private static readonly JsonSerializerOptions TileJsonOptions = new JsonSerializerOptions
        {
            MaxDepth = 64,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };
        private static readonly string[] DocumentFields =
        {
            "name", "layout", "width", "canvasHeight", "columns", "gap", "padding", "radius", "scale",
            "gridSize", "showGrid", "snapToGrid", "accent", "tileColor", "textColor", "tileOpacity", "fontFamily", "pixelShift", "tiles"
        };
        private static readonly Lazy<IReadOnlyDictionary<string, string>> TileFields =
            new Lazy<IReadOnlyDictionary<string, string>>(() => FieldsOf(typeof(OsdDesignTile)));
        private static readonly Lazy<IReadOnlyDictionary<string, string>> MemberFields =
            new Lazy<IReadOnlyDictionary<string, string>>(() => FieldsOf(typeof(OsdGroupMember)));
        private static readonly Lazy<IReadOnlyDictionary<string, string>> ChartLineFields =
            new Lazy<IReadOnlyDictionary<string, string>>(() => FieldsOf(typeof(OsdChartLine)));

        /// <summary>Every field a tile accepts, in the editor's spelling.</summary>
        public static IEnumerable<string> TileFieldNames => TileFields.Value.Values.OrderBy(name => name, StringComparer.Ordinal);

        public static JObject ToJson(OsdDesignDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);
            var result = new JObject
            {
                ["name"] = document.Name,
                ["layout"] = document.IsCanvas ? CanvasLayout : GridLayout,
                ["width"] = document.Width
            };
            if (document.IsCanvas) result["canvasHeight"] = document.CanvasHeight;
            result["columns"] = document.Columns;
            result["gap"] = document.Gap;
            result["padding"] = document.Padding;
            result["radius"] = document.Radius;
            result["scale"] = document.Scale;
            if (document.IsCanvas)
            {
                result["gridSize"] = document.GridSize;
                result["showGrid"] = document.ShowGrid;
                result["snapToGrid"] = document.SnapToGrid;
            }
            result["accent"] = document.Accent;
            result["tileColor"] = document.TileColor;
            result["textColor"] = document.TextColor;
            result["tileOpacity"] = document.TileOpacity;
            result["fontFamily"] = document.FontFamily;
            if (document.PixelShiftEnabled)
                result["pixelShift"] = new JObject
                {
                    ["intervalSeconds"] = document.PixelShiftInterval,
                    ["radius"] = document.PixelShiftRadius
                };
            result["tiles"] = TilesToJson(document.Tiles, null);
            return result;
        }

        private static JArray TilesToJson(IEnumerable<OsdDesignTile> tiles, string parentPath)
        {
            var array = new JArray();
            int index = 0;
            foreach (var tile in tiles)
            {
                string path = parentPath == null ? index.ToString(CultureInfo.InvariantCulture)
                    : parentPath + "/" + index.ToString(CultureInfo.InvariantCulture);
                var serialized = JObject.Parse(JsonSerializer.Serialize(tile, TileJsonOptions));
                serialized.Remove("children");
                // A design saved before tiles had styles may keep colors it never drew. The document
                // shows what the tile draws, so rebuilding it from the document never brings them back.
                foreach (string field in OsdTileStyle.Fields)
                    if (OsdTileStyle.GetColor(tile, field) != null && OsdTileStyle.DrawnColor(tile, field) == null)
                        serialized.Remove(field);
                var item = new JObject { ["path"] = path };
                foreach (var property in serialized.Properties()) item.Add(property.Name, property.Value);
                if (tile.Children.Count > 0) item["children"] = TilesToJson(tile.Children, path);
                array.Add(item);
                index++;
            }
            return array;
        }

        /// <summary>
        /// Builds and validates an editor document. Omitted frames and positions are laid out
        /// automatically, omitted kind-specific fields get the editor's defaults, group tiles that
        /// name a sensor family receive its members, and portable roles are bound to <paramref name="sources"/>.
        /// </summary>
        public static OsdDesignDocument FromJson(JObject json, string name, IReadOnlyList<OsdTelemetrySource> sources,
            ICollection<string> warnings = null)
        {
            ArgumentNullException.ThrowIfNull(json);
            sources ??= Array.Empty<OsdTelemetrySource>();
            CheckFields(json, DocumentFields, "The tile document");
            var document = new OsdDesignDocument();
            document.Name = string.IsNullOrWhiteSpace(name) ? ReadString(json, "name", "document") ?? "Tile overlay" : name;
            string layout = ReadString(json, "layout", "document") ?? CanvasLayout;
            if (layout != CanvasLayout && layout != GridLayout)
                throw Error($"The document field 'layout' must be \"{CanvasLayout}\" or \"{GridLayout}\", not \"{layout}\".");
            document.IsCanvas = layout == CanvasLayout;
            bool autoWidth = json["width"] == null || json["width"].Type == JTokenType.Null;
            bool autoHeight = json["canvasHeight"] == null || json["canvasHeight"].Type == JTokenType.Null;
            document.Width = ReadDouble(json, "width", "document") ?? DefaultWidth;
            document.CanvasHeight = ReadDouble(json, "canvasHeight", "document") ?? 720;
            document.Columns = ReadInt(json, "columns", "document") ?? 2;
            document.Gap = ReadDouble(json, "gap", "document") ?? document.Gap;
            document.Padding = ReadDouble(json, "padding", "document") ?? document.Padding;
            document.Radius = ReadDouble(json, "radius", "document") ?? document.Radius;
            document.Scale = ReadDouble(json, "scale", "document") ?? 1;
            document.GridSize = ReadDouble(json, "gridSize", "document") ?? document.GridSize;
            document.ShowGrid = ReadBool(json, "showGrid", "document") ?? true;
            document.SnapToGrid = ReadBool(json, "snapToGrid", "document") ?? true;
            document.Accent = ReadString(json, "accent", "document") ?? document.Accent;
            document.TileColor = ReadString(json, "tileColor", "document") ?? document.TileColor;
            document.TextColor = ReadString(json, "textColor", "document") ?? document.TextColor;
            document.TileOpacity = ReadDouble(json, "tileOpacity", "document") ?? 1;
            document.FontFamily = ReadString(json, "fontFamily", "document") ?? document.FontFamily;
            if (json["pixelShift"] is JObject pixelShift)
            {
                CheckFields(pixelShift, new[] { "intervalSeconds", "radius" }, "The document field 'pixelShift'");
                document.PixelShiftEnabled = true;
                document.PixelShiftInterval = ReadInt(pixelShift, "intervalSeconds", "pixelShift") ?? OsdDesignDocument.DefaultPixelShiftInterval;
                document.PixelShiftRadius = ReadInt(pixelShift, "radius", "pixelShift") ?? OsdDesignDocument.DefaultPixelShiftRadius;
            }
            else if (json["pixelShift"] != null && json["pixelShift"].Type != JTokenType.Null)
                throw Error("The document field 'pixelShift' must be an object {\"intervalSeconds\": 10..3600, \"radius\": 1..32} or null.");
            CheckDocumentRanges(document);

            if (json["tiles"] is not JArray tiles || tiles.Count == 0)
                throw Error("The tile document needs a non-empty 'tiles' array.");
            if (tiles.Count > OsdDesignDocument.MaximumTiles)
                throw Error($"A design holds at most {OsdDesignDocument.MaximumTiles} tiles, including nested ones.");
            var groups = OsdSourceGroup.Build(sources.Where(source => source.IsAvailable));
            var placement = new HashSet<OsdDesignTile>();
            for (int index = 0; index < tiles.Count; index++)
                document.Tiles.Add(ReadTile(tiles[index], index.ToString(CultureInfo.InvariantCulture), sources, groups, placement));

            // Roles and portable keys resolve exactly as when the editor applies a template.
            document.BindPresetSources(sources);
            if (document.IsCanvas)
                LayOutCanvas(document, placement, autoWidth, autoHeight, warnings);
            Validate(document);
            return document;
        }

        /// <summary>Runs the editor's validation and names the tile that failed.</summary>
        public static void Validate(OsdDesignDocument document)
        {
            try
            {
                document.Validate();
            }
            catch (Exception error) when (IsEditorError(error))
            {
                string path = FindFailingTile(document, document.Tiles, null);
                if (path == null) throw Error("The design is invalid: " + error.Message, error);
                var tile = Resolve(document, path);
                throw Error($"Tile {path} ({tile.Kind} '{tile.Label}') is invalid: {error.Message}", error);
            }
        }

        /// <summary>Returns the path of the deepest tile whose addition makes the document invalid.</summary>
        private static string FindFailingTile(OsdDesignDocument document, IList<OsdDesignTile> tiles, string parentPath)
        {
            for (int count = 1; count <= tiles.Count; count++)
            {
                var probe = CopySettings(document);
                if (parentPath == null)
                {
                    foreach (var tile in tiles.Take(count)) probe.Tiles.Add(tile.Copy());
                }
                else
                {
                    // Keep the ancestors complete so nested bounds are checked against their real parent.
                    var root = document.Tiles[int.Parse(parentPath.Split('/')[0], CultureInfo.InvariantCulture)].Copy();
                    var parent = ResolveIn(root, parentPath);
                    var kept = parent.Children.Take(count).ToArray();
                    parent.Children.Clear();
                    foreach (var child in kept) parent.Children.Add(child);
                    probe.Tiles.Add(root);
                }
                try
                {
                    probe.Validate();
                }
                catch (Exception error) when (IsEditorError(error))
                {
                    string path = parentPath == null ? (count - 1).ToString(CultureInfo.InvariantCulture)
                        : parentPath + "/" + (count - 1).ToString(CultureInfo.InvariantCulture);
                    var failing = tiles[count - 1];
                    if (failing.Children.Count > 0)
                        return FindFailingTile(document, failing.Children, path) ?? path;
                    return path;
                }
            }
            return null;
        }

        private static OsdDesignTile ResolveIn(OsdDesignTile root, string path)
        {
            var current = root;
            foreach (string part in path.Split('/').Skip(1))
                current = current.Children[int.Parse(part, CultureInfo.InvariantCulture)];
            return current;
        }

        private static OsdDesignDocument CopySettings(OsdDesignDocument document) => new OsdDesignDocument
        {
            Name = document.Name, Accent = document.Accent, TileColor = document.TileColor, TextColor = document.TextColor,
            Columns = document.Columns, Width = document.Width, Gap = document.Gap, Padding = document.Padding,
            Radius = document.Radius, Scale = document.Scale, IsCanvas = document.IsCanvas, CanvasHeight = document.CanvasHeight,
            GridSize = document.GridSize, ShowGrid = document.ShowGrid, SnapToGrid = document.SnapToGrid,
            PixelShiftEnabled = document.PixelShiftEnabled, PixelShiftInterval = document.PixelShiftInterval,
            PixelShiftRadius = document.PixelShiftRadius, FontFamily = document.FontFamily, TileOpacity = document.TileOpacity
        };

        /// <summary>Resolves a tile path such as "2" or "2/0" (container 2, its first child).</summary>
        public static OsdDesignTile Resolve(OsdDesignDocument document, string path)
        {
            var indices = ParsePath(path);
            IList<OsdDesignTile> tiles = document.Tiles;
            OsdDesignTile tile = null;
            foreach (int index in indices)
            {
                if (index >= tiles.Count) throw Error($"There is no tile at path '{path}'.");
                tile = tiles[index];
                tiles = tile.Children;
            }
            return tile;
        }

        public static IEnumerable<(string Path, OsdDesignTile Tile)> EnumerateWithPaths(OsdDesignDocument document)
        {
            IEnumerable<(string, OsdDesignTile)> Visit(IList<OsdDesignTile> tiles, string parent)
            {
                for (int index = 0; index < tiles.Count; index++)
                {
                    string path = parent == null ? index.ToString(CultureInfo.InvariantCulture)
                        : parent + "/" + index.ToString(CultureInfo.InvariantCulture);
                    yield return (path, tiles[index]);
                    foreach (var child in Visit(tiles[index].Children, path)) yield return child;
                }
            }
            return Visit(document.Tiles, null);
        }

        /// <summary>Keys that no available reading on this machine provides; such tiles show no value.</summary>
        public static IReadOnlyList<string> FindUnboundKeys(OsdDesignDocument document, IReadOnlyList<OsdTelemetrySource> sources)
        {
            var findings = new List<string>();
            if (sources == null || sources.Count == 0) return findings;
            void Check(string path, OsdDesignTile tile, string field, string key)
            {
                if (string.IsNullOrWhiteSpace(key)) return;
                var exact = sources.Where(source => source.Key == key).ToArray();
                if (exact.Any(source => source.IsAvailable)) return;
                if (exact.Length > 0)
                {
                    findings.Add($"Tile {path} ('{tile.Label}'): {field} '{key}' is currently unavailable on this machine.");
                    return;
                }
                int semantic = sources.Count(source => source.IsAvailable && source.SemanticKey == key);
                findings.Add(semantic > 1
                    ? $"Tile {path} ('{tile.Label}'): {field} '{key}' matches several readings; use one exact key from cfx_list_tile_sources."
                    : $"Tile {path} ('{tile.Label}'): {field} '{key}' matches no reading; the tile will show no value. Use a key from cfx_list_tile_sources.");
            }
            foreach (var (path, tile) in EnumerateWithPaths(document))
            {
                if (tile.Kind == "metric" || tile.Kind == "bar" || tile.Kind == "gauge")
                {
                    Check(path, tile, "key", tile.Key);
                    Check(path, tile, "secondaryKey", tile.SecondaryKey);
                }
                else if (tile.Kind == "group" && tile.GroupMembers != null)
                {
                    foreach (var member in tile.GroupMembers.Where(member => member?.IsEnabled == true))
                        Check(path, tile, "group member key", member.Key);
                }
            }
            return findings;
        }

        /// <summary>
        /// Applies edit operations to a tile document (as returned by <see cref="ToJson"/>) and
        /// returns the edited copy. Each operation sees the result of the previous ones.
        /// </summary>
        public static JObject ApplyOperations(JObject document, JArray operations)
        {
            ArgumentNullException.ThrowIfNull(document);
            if (operations == null || operations.Count == 0)
                throw Error("Provide at least one edit operation.");
            var result = (JObject)document.DeepClone();
            for (int index = 0; index < operations.Count; index++)
            {
                if (operations[index] is not JObject operation)
                    throw Error($"Operation {index} must be an object such as {{\"op\": \"updateTile\", \"path\": \"0\", \"values\": {{...}}}}.");
                string op = ((string)operation["op"] ?? "").Replace("_", "", StringComparison.Ordinal);
                string context = $"Operation {index} ({operation["op"]})";
                try
                {
                    switch (op.ToLowerInvariant())
                    {
                        case "setdocument": SetDocument(result, operation); break;
                        case "addtile": AddTile(result, operation); break;
                        case "updatetile": UpdateTile(result, operation); break;
                        case "removetile": RemoveTile(result, operation); break;
                        case "movetile": MoveTile(result, operation); break;
                        default:
                            throw Error("'op' must be setDocument, addTile, updateTile, removeTile or moveTile.");
                    }
                }
                catch (OverlayDesignAuthoringException error)
                {
                    throw Error(context + ": " + error.Message, error);
                }
            }
            return result;
        }

        private static void SetDocument(JObject document, JObject operation)
        {
            var values = RequireObject(operation, "values");
            foreach (var property in values.Properties())
            {
                if (property.Name == "tiles")
                    throw Error("Change tiles with addTile, updateTile, removeTile and moveTile.");
                if (property.Name == "name")
                    throw Error("The document takes the profile's name; rename the profile with cfx_rename_tile_profile.");
                if (!DocumentFields.Contains(property.Name, StringComparer.Ordinal))
                    throw Error($"Unknown document field '{property.Name}'. Known fields: {string.Join(", ", DocumentFields.Where(field => field != "tiles"))}.");
                if (property.Value.Type == JTokenType.Null) document.Remove(property.Name);
                else document[property.Name] = property.Value.DeepClone();
            }
        }

        private static void AddTile(JObject document, JObject operation)
        {
            var tile = RequireObject(operation, "tile");
            var siblings = ChildrenOf(document, (string)operation["parent"], create: true);
            int index = ReadIndex(operation, siblings.Count);
            siblings.Insert(index, StripPaths((JObject)tile.DeepClone()));
        }

        private static void UpdateTile(JObject document, JObject operation)
        {
            var (_, tile) = Locate(document, RequirePath(operation));
            var values = RequireObject(operation, "values");
            foreach (var property in values.Properties())
            {
                if (property.Name == "path") continue;
                if (property.Name == "children")
                    throw Error("Change a container's children with addTile, removeTile and moveTile.");
                if (property.Value.Type == JTokenType.Null) tile.Remove(property.Name);
                else tile[property.Name] = property.Value.DeepClone();
            }
        }

        private static void RemoveTile(JObject document, JObject operation)
        {
            var (siblings, tile) = Locate(document, RequirePath(operation));
            siblings.Remove(tile);
        }

        private static void MoveTile(JObject document, JObject operation)
        {
            string path = RequirePath(operation);
            var (siblings, tile) = Locate(document, path);
            string parent = (string)operation["parent"];
            string currentParent = path.Contains('/') ? path.Substring(0, path.LastIndexOf('/')) : "";
            parent = string.IsNullOrWhiteSpace(parent) ? "" : parent.Trim();
            if (parent == path || parent.StartsWith(path + "/", StringComparison.Ordinal))
                throw Error("A tile cannot be moved into itself.");
            // Resolve the target before detaching: paths always refer to the state before this operation.
            var target = ChildrenOf(document, parent, create: true);
            siblings.Remove(tile);
            if (parent != currentParent)
            {
                // Coordinates are relative to the parent canvas; place the tile anew there.
                tile.Remove("x");
                tile.Remove("y");
            }
            target.Insert(ReadIndex(operation, target.Count), tile);
        }

        private static (JArray Siblings, JObject Tile) Locate(JObject document, string path)
        {
            var indices = ParsePath(path);
            JArray siblings = document["tiles"] as JArray ?? throw Error("The document has no tiles.");
            JObject tile = null;
            for (int depth = 0; depth < indices.Count; depth++)
            {
                if (depth > 0) siblings = tile["children"] as JArray ?? throw Error($"There is no tile at path '{path}'.");
                if (indices[depth] >= siblings.Count || siblings[indices[depth]] is not JObject next)
                    throw Error($"There is no tile at path '{path}'.");
                tile = next;
            }
            return (siblings, tile);
        }

        private static JArray ChildrenOf(JObject document, string parentPath, bool create)
        {
            if (string.IsNullOrWhiteSpace(parentPath))
            {
                if (document["tiles"] is not JArray tiles)
                    document["tiles"] = tiles = new JArray();
                return tiles;
            }
            var (_, parent) = Locate(document, parentPath.Trim());
            if (!string.Equals((string)parent["kind"], "container", StringComparison.Ordinal))
                throw Error($"Tile '{parentPath}' is not a container; only containers hold other tiles.");
            if (parent["children"] is not JArray children)
            {
                if (!create) throw Error($"Container '{parentPath}' has no children.");
                parent["children"] = children = new JArray();
            }
            return children;
        }

        private static int ReadIndex(JObject operation, int count)
        {
            var token = operation["index"];
            if (token == null || token.Type == JTokenType.Null) return count;
            if (token.Type != JTokenType.Integer || (int)token < 0 || (int)token > count)
                throw Error($"'index' must be an integer from 0 to {count}.");
            return (int)token;
        }

        private static string RequirePath(JObject operation)
        {
            string path = (string)operation["path"];
            if (string.IsNullOrWhiteSpace(path)) throw Error("'path' is required, for example \"0\" or \"2/1\".");
            return path.Trim();
        }

        private static JObject RequireObject(JObject operation, string field) =>
            operation[field] as JObject ?? throw Error($"'{field}' must be an object.");

        private static JObject StripPaths(JObject tile)
        {
            tile.Remove("path");
            if (tile["children"] is JArray children)
                foreach (var child in children.OfType<JObject>()) StripPaths(child);
            return tile;
        }

        private static List<int> ParsePath(string path)
        {
            var indices = new List<int>();
            foreach (string part in (path ?? "").Trim().Split('/'))
            {
                if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out int index))
                    throw Error($"'{path}' is not a tile path. Paths are indices separated by '/', for example \"0\" or \"2/1\".");
                indices.Add(index);
            }
            return indices;
        }

        private static OsdDesignTile ReadTile(JToken token, string path, IReadOnlyList<OsdTelemetrySource> sources,
            IReadOnlyList<OsdSourceGroup> groups, ISet<OsdDesignTile> placement)
        {
            if (token is not JObject input)
                throw Error($"Tile {path} must be an object.");
            var json = Normalize(input, TileFields.Value, $"Tile {path}");
            json.Remove("path");
            var children = json["children"];
            json.Remove("children");
            string kind = json["kind"]?.Type == JTokenType.String ? (string)json["kind"] : "metric";
            if (!OsdDesignDocument.WidgetKinds.Contains(kind) && kind != "group")
                throw Error($"Tile {path}: unknown kind '{kind}'. Use one of {string.Join(", ", OsdDesignDocument.WidgetKinds.Append("group"))}.");
            if (json["groupMembers"] is JArray members)
                for (int index = 0; index < members.Count; index++)
                    members[index] = members[index] is JObject member
                        ? Normalize(member, MemberFields.Value, $"Tile {path} group member {index}")
                        : throw Error($"Tile {path}: group member {index} must be an object.");
            if (json["chartLines"] is JArray lines)
                for (int index = 0; index < lines.Count; index++)
                    lines[index] = lines[index] is JObject line
                        ? Normalize(line, ChartLineFields.Value, $"Tile {path} chart line {index}")
                        : throw Error($"Tile {path}: chart line {index} must be an object.");
            if (kind == "group") json = ExpandGroup(json, path, groups);
            ApplyKindDefaults(json, kind, sources);

            OsdDesignTile tile;
            try
            {
                tile = JsonSerializer.Deserialize<OsdDesignTile>(json.ToString(Newtonsoft.Json.Formatting.None), TileJsonOptions)
                    ?? throw Error($"Tile {path} is empty.");
            }
            catch (JsonException error)
            {
                string field = error.Path?.TrimStart('$', '.');
                throw Error($"Tile {path}: field '{field}' has a value of the wrong type ({error.Message}).", error);
            }
            // x and y go together: a tile with only one of them is placed automatically.
            if (!tile.X.HasValue || !tile.Y.HasValue) placement.Add(tile);

            if (children != null && children.Type != JTokenType.Null)
            {
                if (kind != "container")
                    throw Error($"Tile {path}: only containers have 'children'.");
                if (children is not JArray childArray)
                    throw Error($"Tile {path}: 'children' must be an array.");
                for (int index = 0; index < childArray.Count; index++)
                    tile.Children.Add(ReadTile(childArray[index], path + "/" + index.ToString(CultureInfo.InvariantCulture), sources, groups, placement));
            }
            return tile;
        }

        private static JObject ExpandGroup(JObject json, string path, IReadOnlyList<OsdSourceGroup> groups)
        {
            bool hasMembers = json["groupMembers"] is JArray existing && existing.Count > 0;
            string groupKey = json["groupKey"]?.Type == JTokenType.String ? (string)json["groupKey"] : null;
            if (!hasMembers)
            {
                // Template roles ("role:...") keep placeholder members until the preset binding
                // replaces them, so a role without members has nothing to lay out.
                if (string.IsNullOrWhiteSpace(groupKey) || groupKey.StartsWith("role:", StringComparison.Ordinal))
                    throw Error($"Tile {path}: a group tile needs 'groupKey' (a sensor family from cfx_list_tile_sources) or 'groupMembers'.");
                var group = groups.FirstOrDefault(candidate => candidate.Key == groupKey)
                    ?? throw Error($"Tile {path}: no sensor family has the groupKey '{groupKey}'. List the families with cfx_list_tile_sources.");
                int span = json["columnSpan"]?.Type == JTokenType.Integer ? (int)json["columnSpan"] : 1;
                var template = JObject.Parse(JsonSerializer.Serialize(group.CreateTile(span), TileJsonOptions));
                template.Remove("children");
                // Explicit fields win over the family's defaults.
                foreach (var property in json.Properties()) template[property.Name] = property.Value.DeepClone();
                json = template;
            }
            if (json["groupDisplay"] == null)
                json["groupDisplay"] = string.Equals((string)json["unit"], "%", StringComparison.Ordinal) ? "bar" : "metric";
            if (json["groupColumns"] == null)
            {
                int count = (json["groupMembers"] as JArray)?.Count ?? 2;
                json["groupColumns"] = Math.Clamp(count > 8 ? 4 : 2, 1, Math.Max(1, count));
            }
            if (json["key"] == null && groupKey != null) json["key"] = groupKey;
            if (json["label"] == null && groupKey != null) json["label"] = "Group";
            return json;
        }

        /// <summary>
        /// Gives omitted fields the values the editor assigns when a tile of that kind is created.
        /// Only fields the editor always stores qualify: an optional field is legitimately absent
        /// from a saved tile, and defaulting it would change designs on every unrelated edit.
        /// </summary>
        private static void ApplyKindDefaults(JObject json, string kind, IReadOnlyList<OsdTelemetrySource> sources)
        {
            // Only absent fields: an explicit null is a deliberate value the editor also stores.
            bool Missing(string field) => json[field] == null;
            switch (kind)
            {
                case "chart":
                    if (Missing("key")) json["key"] = "frametimes";
                    if (Missing("unit")) json["unit"] = (string)json["key"] == "framerates" ? "FPS" : "ms";
                    if (Missing("label")) json["label"] = (string)json["key"] == "framerates" ? "Framerate"
                        : (string)json["key"] == "displaytimes" ? "Display time" : "Frame time";
                    break;
                case "classicRows":
                    if (Missing("key")) json["key"] = null;
                    if (Missing("unit")) json["unit"] = "";
                    if (Missing("height")) json["height"] = 240;
                    if (Missing("label")) json["label"] = "Classic rows";
                    break;
                case "metric":
                case "bar":
                case "gauge":
                    if (Missing("key")) json["key"] = "fps";
                    var source = sources.FirstOrDefault(candidate => candidate.Key == (string)json["key"])
                        ?? sources.FirstOrDefault(candidate => candidate.IsAvailable && candidate.SemanticKey == (string)json["key"]);
                    if (Missing("unit") && source != null && json["valueScale"] == null && json["valueFormat"] == null)
                        json["unit"] = source.Unit ?? "";
                    if (Missing("label")) json["label"] = source?.Name ?? (string)json["key"];
                    break;
                case "text":
                    if (Missing("label")) json["label"] = "Text";
                    break;
                case "container":
                    if (Missing("label")) json["label"] = "Container";
                    break;
                case "icon":
                    // The renderer draws icons in Segoe MDL2 Assets unless a family is set.
                    if (Missing("label")) json["label"] = "Icon";
                    if (Missing("key")) json["key"] = null;
                    break;
                case "image":
                    if (Missing("label")) json["label"] = "Image";
                    break;
            }
        }

        private static void LayOutCanvas(OsdDesignDocument document, ISet<OsdDesignTile> unplaced, bool autoWidth,
            bool autoHeight, ICollection<string> warnings)
        {
            foreach (var tile in document.Tiles)
            {
                if (!tile.FrameWidth.HasValue) tile.FrameWidth = NaturalWidth(document, tile, document.Width);
                if (tile.Kind == "container") LayOutContainer(document, tile, document.Width, unplaced, warnings);
                if (!tile.FrameHeight.HasValue) tile.FrameHeight = document.DefaultFrame(tile).Height;
            }
            Place(document.Tiles, unplaced, document.Width, document.Gap);

            double right = document.Tiles.Max(tile => tile.X.Value + tile.FrameWidth.Value);
            double bottom = document.Tiles.Max(tile => tile.Y.Value + tile.FrameHeight.Value);
            double fittedWidth = Math.Max(220, Math.Ceiling(right / document.GridSize) * document.GridSize);
            double fittedHeight = Math.Max(64, Math.Ceiling(bottom / document.GridSize) * document.GridSize);
            if (autoWidth)
                document.Width = fittedWidth;
            else if (right > document.Width)
            {
                warnings?.Add($"The canvas width grew from {document.Width:0.###} to {fittedWidth:0.###} to fit every tile.");
                document.Width = fittedWidth;
            }
            if (autoHeight)
                document.CanvasHeight = fittedHeight;
            else if (bottom > document.CanvasHeight)
            {
                warnings?.Add($"The canvas height grew from {document.CanvasHeight:0.###} to {fittedHeight:0.###} to fit every tile.");
                document.CanvasHeight = fittedHeight;
            }
        }

        private static void LayOutContainer(OsdDesignDocument document, OsdDesignTile container, double available,
            ISet<OsdDesignTile> unplaced, ICollection<string> warnings)
        {
            double padding = document.EffectivePadding(container);
            if (!container.FrameWidth.HasValue) container.FrameWidth = NaturalWidth(document, container, available);
            double inner = Math.Max(8, container.FrameWidth.Value - 2 * padding);
            foreach (var child in container.Children)
            {
                if (!child.FrameWidth.HasValue) child.FrameWidth = NaturalWidth(document, child, inner);
                if (child.Kind == "container") LayOutContainer(document, child, inner, unplaced, warnings);
                if (!child.FrameHeight.HasValue) child.FrameHeight = document.DefaultFrame(child).Height;
            }
            Place(container.Children, unplaced, inner, document.Gap);
            if (container.Children.Count == 0) return;
            double required = container.Children.Max(child => child.Y.Value + child.FrameHeight.Value) + 2 * padding;
            if (!container.FrameHeight.HasValue)
                container.FrameHeight = Math.Max(24, Math.Ceiling(required / document.GridSize) * document.GridSize);
            else if (required > container.FrameHeight.Value + 1e-7)
            {
                warnings?.Add($"Container '{container.Label}' grew from {container.FrameHeight.Value:0.###} to {required:0.###} px to fit its children.");
                container.FrameHeight = required;
            }
        }

        /// <summary>
        /// The width a tile of its columnSpan takes in the document's columns. Unlike the editor's
        /// grid-rounded default, two half-width tiles and their gap fit the canvas exactly.
        /// </summary>
        private static double NaturalWidth(OsdDesignDocument document, OsdDesignTile tile, double available)
        {
            double padding = document.EffectivePadding(tile);
            // An icon is square around its glyph rather than a column wide.
            if (tile.Kind == "icon") return Math.Round(Math.Min(available, tile.ValueSize * 1.4 + 2 * padding), 3);
            int span = Math.Clamp(tile.ColumnSpan, 1, Math.Max(1, document.Columns));
            double column = (document.Width - document.Gap * (document.Columns - 1)) / document.Columns;
            double minimum = (tile.Kind == "container" || tile.Kind == "icon" || tile.Kind == "image" ? 8 : 64) + 2 * padding;
            return Math.Round(Math.Min(available, Math.Max(minimum, column * span + document.Gap * (span - 1))), 3);
        }

        /// <summary>First fit, the way the editor drops a new tile: left to right, then downwards.</summary>
        private static void Place(IList<OsdDesignTile> tiles, ISet<OsdDesignTile> unplaced, double width, double gap)
        {
            var occupied = tiles.Where(tile => !unplaced.Contains(tile))
                .Select(tile => new Rect(tile.X.Value, tile.Y.Value, tile.FrameWidth.Value, tile.FrameHeight.Value)).ToList();
            foreach (var tile in tiles.Where(unplaced.Contains))
            {
                var size = new Size(Math.Min(tile.FrameWidth.Value, width), tile.FrameHeight.Value);
                var xs = occupied.Select(r => Math.Round(r.Right + gap, 3)).Prepend(0).Distinct().OrderBy(v => v).ToArray();
                var ys = occupied.Select(r => Math.Round(r.Bottom + gap, 3)).Prepend(0).Distinct().OrderBy(v => v).ToArray();
                Point? found = null;
                foreach (double y in ys)
                {
                    foreach (double x in xs)
                    {
                        var candidate = new Rect(x + .1, y + .1, Math.Max(1, size.Width - .2), Math.Max(1, size.Height - .2));
                        if (x + size.Width <= width + 1e-7 && !occupied.Any(r => r.IntersectsWith(candidate)))
                        {
                            found = new Point(x, y);
                            break;
                        }
                    }
                    if (found.HasValue) break;
                }
                tile.X = found?.X ?? 0;
                tile.Y = found?.Y ?? 0;
                occupied.Add(new Rect(tile.X.Value, tile.Y.Value, size.Width, size.Height));
                unplaced.Remove(tile);
            }
        }

        private static void CheckDocumentRanges(OsdDesignDocument document)
        {
            void Range(double value, double min, double max, string field)
            {
                if (!double.IsFinite(value) || value < min || value > max)
                    throw Error($"The document field '{field}' must be between {min.ToString(CultureInfo.InvariantCulture)} and {max.ToString(CultureInfo.InvariantCulture)}.");
            }
            Range(document.Columns, 1, 4, "columns");
            Range(document.Width, 220, 1600, "width");
            Range(document.Gap, 0, 40, "gap");
            Range(document.Padding, 0, 40, "padding");
            Range(document.Radius, 0, 32, "radius");
            Range(document.Scale, .5, 2, "scale");
            Range(document.GridSize, 1, 128, "gridSize");
            Range(document.CanvasHeight, 64, 4096, "canvasHeight");
            Range(document.TileOpacity, 0, 1, "tileOpacity");
            if (document.FontFamily.Length > OsdDesignDocument.MaximumFontFamilyLength)
                throw Error($"The document field 'fontFamily' holds at most {OsdDesignDocument.MaximumFontFamilyLength} characters.");
            if (document.PixelShiftEnabled)
            {
                Range(document.PixelShiftInterval, 10, 3600, "pixelShift.intervalSeconds");
                Range(document.PixelShiftRadius, 1, 32, "pixelShift.radius");
            }
        }

        /// <summary>Rejects unknown fields and fixes the case of known ones, so a typo never passes silently.</summary>
        private static JObject Normalize(JObject input, IReadOnlyDictionary<string, string> fields, string context)
        {
            var result = new JObject();
            foreach (var property in input.Properties())
            {
                if (property.Name == "path" || property.Name == "children") { result[property.Name] = property.Value.DeepClone(); continue; }
                if (!fields.TryGetValue(property.Name.ToLowerInvariant(), out string name))
                    throw Error($"{context}: unknown field '{property.Name}'. {Suggest(property.Name, fields.Values)}");
                result[name] = property.Value.DeepClone();
            }
            return result;
        }

        private static void CheckFields(JObject input, IEnumerable<string> fields, string context)
        {
            var known = fields.ToArray();
            foreach (var property in input.Properties())
                if (!known.Contains(property.Name, StringComparer.Ordinal))
                    throw Error($"{context} has an unknown field '{property.Name}'. {Suggest(property.Name, known)}");
        }

        private static string Suggest(string name, IEnumerable<string> known)
        {
            var close = known.Where(candidate => candidate.Contains(name, StringComparison.OrdinalIgnoreCase)
                || name.Contains(candidate, StringComparison.OrdinalIgnoreCase)
                || Distance(candidate.ToLowerInvariant(), name.ToLowerInvariant()) <= 2).Take(3).ToArray();
            return close.Length > 0 ? $"Did you mean {string.Join(" or ", close.Select(c => "'" + c + "'"))}?"
                : "Call cfx_get_tile_design_guide for the accepted fields.";
        }

        private static int Distance(string a, string b)
        {
            var row = Enumerable.Range(0, b.Length + 1).ToArray();
            for (int i = 1; i <= a.Length; i++)
            {
                int previous = row[0];
                row[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int current = row[j];
                    row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), previous + (a[i - 1] == b[j - 1] ? 0 : 1));
                    previous = current;
                }
            }
            return row[b.Length];
        }

        private static IReadOnlyDictionary<string, string> FieldsOf(Type type) =>
            TileJsonOptions.GetTypeInfo(type).Properties
                .ToDictionary(property => property.Name.ToLowerInvariant(), property => property.Name, StringComparer.Ordinal);

        private static string ReadString(JObject json, string field, string context)
        {
            var token = json[field];
            if (token == null || token.Type == JTokenType.Null) return null;
            if (token.Type != JTokenType.String) throw Error($"The {context} field '{field}' must be a string.");
            return (string)token;
        }

        private static double? ReadDouble(JObject json, string field, string context)
        {
            var token = json[field];
            if (token == null || token.Type == JTokenType.Null) return null;
            if (token.Type != JTokenType.Integer && token.Type != JTokenType.Float) throw Error($"The {context} field '{field}' must be a number.");
            return (double)token;
        }

        private static int? ReadInt(JObject json, string field, string context)
        {
            var token = json[field];
            if (token == null || token.Type == JTokenType.Null) return null;
            if (token.Type != JTokenType.Integer) throw Error($"The {context} field '{field}' must be an integer.");
            return (int)token;
        }

        private static bool? ReadBool(JObject json, string field, string context)
        {
            var token = json[field];
            if (token == null || token.Type == JTokenType.Null) return null;
            if (token.Type != JTokenType.Boolean) throw Error($"The {context} field '{field}' must be true or false.");
            return (bool)token;
        }

        internal static bool IsEditorError(Exception error) => error is InvalidOperationException || error is ArgumentException
            || error is FormatException || error is NotSupportedException || error is System.IO.InvalidDataException
            || error is JsonException;

        private static OverlayDesignAuthoringException Error(string message, Exception inner = null) =>
            inner == null ? new OverlayDesignAuthoringException(message) : new OverlayDesignAuthoringException(message, inner);
    }
}
