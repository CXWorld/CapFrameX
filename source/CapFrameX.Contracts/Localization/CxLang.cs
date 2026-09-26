using CapFrameX.Extensions.NetStandard;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CapFrameX.Contracts.Localization
{
    /// <summary>
    /// UI strings and OSD labels. The interface language and the overlay language
    /// are independent. Call sites use stable catalog keys. English text lives in en.json.
    /// </summary>
    public sealed class CxLang : INotifyPropertyChanged
    {
        public static CxLang Instance { get; } = new CxLang();

        private readonly Dictionary<string, Catalog> _catalogs = new Dictionary<string, Catalog>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _loadErrors = new List<string>();

        private volatile string _uiLanguage = "en";
        private static readonly string[] AppPlaceholder = { "<APP>" };
        private IReadOnlyList<LanguageOption> _languages = Array.Empty<LanguageOption>();

        // Everything the overlay translation needs, swapped as one reference. OSD threads read
        // it once per call, so a language switch can never mix phrases, catalog and cache from
        // two languages, and a result computed for the old language lands in the old cache.
        private volatile OverlayState _overlay = OverlayState.English;

        // The same "overlay" and "phrases" rules in the interface language, for desktop views
        // that show sensor units. Kept apart from _overlay so those views follow the UI
        // language even when the overlay uses another one.
        private volatile OverlayState _uiText = OverlayState.English;

        public event PropertyChangedEventHandler PropertyChanged;
        public event Action OverlayLanguageChanged;

        private CxLang()
        {
            // Nothing in here may throw: this runs in a static initializer, and a failure would
            // take down every caller of CxLang, including the fatal error handler.
            try
            {
                Load();
            }
            catch (Exception ex)
            {
                _loadErrors.Add("Localization catalogs could not be loaded: " + ex.Message);
            }

            if (_languages.Count == 0)
                _languages = new[] { new LanguageOption("en", "English") };
        }

        public string UiLanguage => _uiLanguage;
        public string OverlayLanguage => _overlay.Language;
        public IReadOnlyList<LanguageOption> Languages => _languages;

        /// <summary>Catalogs that were skipped because they could not be parsed. Empty when all loaded.</summary>
        public IReadOnlyList<string> LoadErrors => _loadErrors;

        public string this[string key] => T(key);

        public void SetUiLanguage(string language)
        {
            var next = Normalize(language);
            if (next == _uiLanguage)
                return;
            _uiLanguage = next;
            _uiText = CreateOverlayState(next);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UiLanguage)));
        }

        public void SetOverlayLanguage(string language)
        {
            var next = Normalize(language);
            if (next == _overlay.Language)
                return;
            _overlay = CreateOverlayState(next);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OverlayLanguage)));
            OverlayLanguageChanged?.Invoke();
        }

        public static string T(string key)
        {
            if (string.IsNullOrEmpty(key))
                return key ?? string.Empty;
            if (Instance.TryGet(Instance._uiLanguage, key, out var translated))
                return translated;
            if (Instance.TryGet("en", key, out var english))
                return english;
            return key;
        }

        /// <summary>
        /// Looks up <paramref name="key"/> and formats it with <paramref name="args"/>. Enum
        /// arguments are shown through their translation. If a translation has broken
        /// placeholders, the English text is used instead of throwing.
        /// </summary>
        public static string Format(string key, params object[] args)
        {
            var template = T(key);
            if (args == null || args.Length == 0)
                return template;

            var values = new object[args.Length];
            for (int i = 0; i < args.Length; i++)
                values[i] = args[i] is Enum enumValue ? TranslateEnum(enumValue) : args[i];

            try
            {
                return string.Format(CultureInfo.CurrentCulture, template, values);
            }
            catch (FormatException)
            {
                if (Instance.TryGet("en", key, out var english))
                {
                    try
                    {
                        return string.Format(CultureInfo.CurrentCulture, english, values);
                    }
                    catch (FormatException)
                    {
                    }
                }
                return template;
            }
        }

        /// <summary>
        /// For code that must work even if localization itself is broken, such as the fatal
        /// error handler. Never throws.
        /// </summary>
        public static string TOrDefault(string key, string fallback)
        {
            try
            {
                var value = T(key);
                return string.IsNullOrEmpty(value) || value == key ? fallback : value;
            }
            catch
            {
                return fallback;
            }
        }

        /// <summary>
        /// Parses a catalog exactly like the app does at startup and throws if it is invalid,
        /// including phrase patterns that are not valid regular expressions. Used by the tests.
        /// </summary>
        public static void ValidateCatalog(string json, string language)
        {
            var catalog = Catalog.Parse(json, language);
            foreach (var (pattern, replacement) in catalog.RawPhrases)
                _ = new Phrase(pattern, replacement);
        }

        public string TranslateOverlayLabel(string label)
        {
            if (string.Equals(label, "<APP>", StringComparison.Ordinal))
            {
                var state = _overlay;
                if (state.Catalog != null
                    && state.Catalog.Overlay.TryGetValue("<APP>", out var translated)
                    && !string.IsNullOrEmpty(translated))
                    return translated;
                return label;
            }
            return TranslateOverlay(label);
        }

        public static string TranslateEnum(Enum value, bool useShortDescription = false)
        {
            if (value == null)
                return string.Empty;
            var key = value.GetType().Name + "_" + value + (useShortDescription ? "_Short" : "");
            if (Instance.TryGet(Instance._uiLanguage, key, out var translated)
                || Instance.TryGet("en", key, out translated))
                return translated;
            if (useShortDescription)
            {
                var shortDesc = value.GetShortDescription();
                if (!string.IsNullOrEmpty(shortDesc) && shortDesc != value.ToString())
                    return shortDesc;
            }
            var desc = value.GetDescription();
            if (!string.IsNullOrEmpty(desc))
                return desc;
            return value.ToString();
        }

        public string TranslateOverlay(string label) => TranslateWith(_overlay, label);

        /// <summary>
        /// Like <see cref="TranslateOverlay"/>, but in the interface language. For desktop views,
        /// which must follow the UI language rather than the overlay language.
        /// </summary>
        public string TranslateUiText(string label) => TranslateWith(_uiText, label);

        private static string TranslateWith(OverlayState state, string label)
        {
            if (string.IsNullOrEmpty(label) || state.Catalog == null)
                return label ?? string.Empty;
            try
            {
                if (label.IndexOf("<APP>", StringComparison.Ordinal) >= 0)
                {
                    var parts = label.Split(AppPlaceholder, StringSplitOptions.None);
                    for (int i = 0; i < parts.Length; i++)
                        parts[i] = TranslateOverlayCore(state, parts[i]);
                    return string.Join("<APP>", parts);
                }
                return TranslateOverlayCore(state, label);
            }
            catch
            {
                return label;
            }
        }

        private static string TranslateOverlayCore(OverlayState state, string label)
        {
            if (string.IsNullOrEmpty(label))
                return label ?? string.Empty;
            if (state.Cache.TryGetValue(label, out var cached))
                return cached;

            var result = label;
            if (state.Catalog.Overlay.TryGetValue(label, out var exact)
                && !string.IsNullOrEmpty(exact)
                && exact.IndexOf("<APP>", StringComparison.Ordinal) < 0)
            {
                result = exact;
            }
            else
            {
                foreach (var phrase in state.Phrases)
                    result = phrase.Pattern.Replace(result, phrase.Replacement);
                result = CapitalizeStart(result, state.Culture);
            }

            state.Cache[label] = result;
            return result;
        }

        private bool TryGet(string language, string key, out string value)
        {
            value = null;
            return _catalogs.TryGetValue(language, out var catalog)
                && catalog.Strings.TryGetValue(key, out value)
                && !string.IsNullOrEmpty(value);
        }

        private OverlayState CreateOverlayState(string language)
        {
            if (language == "en" || !_catalogs.TryGetValue(language, out var catalog))
                return OverlayState.English;

            var culture = CultureInfo.InvariantCulture;
            if (!string.IsNullOrWhiteSpace(catalog.Culture))
            {
                try
                {
                    culture = CultureInfo.GetCultureInfo(catalog.Culture);
                }
                catch (CultureNotFoundException)
                {
                }
            }
            return new OverlayState(language, catalog, catalog.GetOrBuildPhrases(), culture);
        }

        private static string CapitalizeStart(string text, CultureInfo culture)
        {
            if (string.IsNullOrEmpty(text) || !char.IsLower(text[0]))
                return text;
            return char.ToUpper(text[0], culture) + text.Substring(1);
        }

        private void Load()
        {
            var assembly = typeof(CxLang).Assembly;
            foreach (var name in assembly.GetManifestResourceNames())
            {
                var match = Regex.Match(name, @"\.([A-Za-z0-9]+)\.json$", RegexOptions.CultureInvariant);
                if (!match.Success)
                    continue;
                var language = match.Groups[1].Value.ToLowerInvariant();
                try
                {
                    using var stream = assembly.GetManifestResourceStream(name);
                    if (stream == null)
                        continue;
                    using var reader = new StreamReader(stream);
                    var catalog = Catalog.Parse(reader.ReadToEnd(), language);
                    catalog.GetOrBuildPhrases();
                    _catalogs[language] = catalog;
                }
                catch (Exception ex)
                {
                    // One broken catalog must not take the others (or the app) down with it.
                    _loadErrors.Add($"Localization catalog '{name}' was skipped: {ex.Message}");
                }
            }
            _languages = _catalogs
                .Select(pair => new LanguageOption(pair.Key, pair.Value.DisplayName))
                .OrderBy(option => option.Code.Equals("en", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(option => option.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private string Normalize(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
                return "en";
            language = language.Trim().ToLowerInvariant();
            return _catalogs.ContainsKey(language) ? language : "en";
        }

        private sealed class OverlayState
        {
            public static readonly OverlayState English = new OverlayState("en", null, Array.Empty<Phrase>(), CultureInfo.InvariantCulture);

            public OverlayState(string language, Catalog catalog, Phrase[] phrases, CultureInfo culture)
            {
                Language = language;
                Catalog = catalog;
                Phrases = phrases;
                Culture = culture;
            }

            public string Language { get; }
            /// <summary>Null for English, which is shown untranslated.</summary>
            public Catalog Catalog { get; }
            public Phrase[] Phrases { get; }
            public CultureInfo Culture { get; }
            public ConcurrentDictionary<string, string> Cache { get; } = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        }

        private sealed class Phrase
        {
            public Phrase(string pattern, string replacement)
            {
                // Patterns come from contributed catalogs and run on OSD threads, so bound them.
                Pattern = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
                Replacement = replacement ?? string.Empty;
            }

            public static bool TryCreate(string pattern, string replacement, out Phrase phrase)
            {
                phrase = null;
                if (string.IsNullOrEmpty(pattern))
                    return false;
                try
                {
                    phrase = new Phrase(pattern, replacement);
                    return true;
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }

            public Regex Pattern { get; }
            public string Replacement { get; }
        }

        private sealed class Catalog
        {
            public string DisplayName { get; private set; }
            public string Culture { get; private set; }
            public Dictionary<string, string> Strings { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
            public Dictionary<string, string> Overlay { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
            public List<(string pattern, string replacement)> RawPhrases { get; } = new List<(string pattern, string replacement)>();
            private Phrase[] _builtPhrases;

            public Phrase[] GetOrBuildPhrases()
            {
                if (_builtPhrases != null)
                    return _builtPhrases;

                var list = new List<Phrase>();
                foreach (var (pattern, replacement) in RawPhrases)
                {
                    if (Phrase.TryCreate(pattern, replacement, out var phrase))
                        list.Add(phrase);
                }
                list.Sort((a, b) => b.Pattern.ToString().Length.CompareTo(a.Pattern.ToString().Length));
                _builtPhrases = list.ToArray();
                return _builtPhrases;
            }

            public static Catalog Parse(string json, string language)
            {
                var catalog = new Catalog();
                var options = new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };
                using var doc = JsonDocument.Parse(json, options);
                var root = doc.RootElement;
                catalog.DisplayName = ReadString(root, "name") ?? language;
                catalog.Culture = ReadString(root, "culture");
                ReadMap(root, "strings", catalog.Strings);
                ReadMap(root, "overlay", catalog.Overlay);
                if (root.TryGetProperty("phrases", out var phrases) && phrases.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in phrases.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Object
                            || !item.TryGetProperty("pattern", out var patternElement)
                            || patternElement.ValueKind != JsonValueKind.String)
                            throw new FormatException("Every entry in \"phrases\" needs a string \"pattern\".");
                        var pattern = patternElement.GetString();
                        var replacement = item.TryGetProperty("replacement", out var replacementElement)
                            && replacementElement.ValueKind == JsonValueKind.String
                                ? replacementElement.GetString()
                                : string.Empty;
                        if (!string.IsNullOrEmpty(pattern))
                            catalog.RawPhrases.Add((pattern, replacement));
                    }
                }
                return catalog;
            }

            private static string ReadString(JsonElement root, string name)
            {
                if (root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.String)
                    return element.GetString();
                return null;
            }

            private static void ReadMap(JsonElement root, string name, Dictionary<string, string> target)
            {
                if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Object)
                    return;
                foreach (var item in element.EnumerateObject())
                {
                    if (item.Value.ValueKind != JsonValueKind.String)
                        throw new FormatException($"\"{name}.{item.Name}\" must be a string.");
                    target[item.Name] = item.Value.GetString() ?? string.Empty;
                }
            }
        }
    }
}
