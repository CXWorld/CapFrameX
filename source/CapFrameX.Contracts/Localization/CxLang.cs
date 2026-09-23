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
        private readonly ConcurrentDictionary<string, string> _overlayCache = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        private string _uiLanguage = "en";
        private string _overlayLanguage = "en";
        private static readonly string[] AppPlaceholder = { "<APP>" };
        private Phrase[] _overlayPhrases = Array.Empty<Phrase>();
        private IReadOnlyList<LanguageOption> _languages = Array.Empty<LanguageOption>();

        public event PropertyChangedEventHandler PropertyChanged;
        public event Action OverlayLanguageChanged;

        private CxLang()
        {
            Load();
            ApplyOverlayLanguage("en");
        }

        public string UiLanguage => _uiLanguage;
        public string OverlayLanguage => _overlayLanguage;
        public IReadOnlyList<LanguageOption> Languages => _languages;

        public string this[string key] => T(key);

        public void SetUiLanguage(string language)
        {
            var next = Normalize(language);
            if (next == _uiLanguage)
                return;
            _uiLanguage = next;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UiLanguage)));
        }

        public void SetOverlayLanguage(string language)
        {
            var next = Normalize(language);
            if (next == _overlayLanguage)
                return;
            _overlayLanguage = next;
            ApplyOverlayLanguage(next);
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

        public string TranslateOverlayLabel(string label)
        {
            if (string.Equals(label, "<APP>", StringComparison.Ordinal))
            {
                if (_catalogs.TryGetValue(_overlayLanguage, out var catalog)
                    && catalog.Overlay.TryGetValue("<APP>", out var translated)
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

        public string TranslateOverlay(string label)
        {
            if (string.IsNullOrEmpty(label) || _overlayLanguage == "en")
                return label ?? string.Empty;
            try
            {
                if (label.IndexOf("<APP>", StringComparison.Ordinal) >= 0)
                {
                    var parts = label.Split(AppPlaceholder, StringSplitOptions.None);
                    for (int i = 0; i < parts.Length; i++)
                        parts[i] = TranslateOverlayCore(parts[i]);
                    return string.Join("<APP>", parts);
                }
                return TranslateOverlayCore(label);
            }
            catch
            {
                return label;
            }
        }

        private string TranslateOverlayCore(string label)
        {
            if (string.IsNullOrEmpty(label))
                return label ?? string.Empty;
            if (_overlayCache.TryGetValue(label, out var cached))
                return cached;

            var result = label;
            if (_catalogs.TryGetValue(_overlayLanguage, out var catalog)
                && catalog.Overlay.TryGetValue(label, out var exact)
                && !string.IsNullOrEmpty(exact)
                && exact.IndexOf("<APP>", StringComparison.Ordinal) < 0)
            {
                result = exact;
            }
            else
            {
                foreach (var phrase in _overlayPhrases)
                    result = phrase.Pattern.Replace(result, phrase.Replacement);
                result = CapitalizeStart(result);
            }

            _overlayCache[label] = result;
            return result;
        }

        private bool TryGet(string language, string key, out string value)
        {
            value = null;
            return _catalogs.TryGetValue(language, out var catalog)
                && catalog.Strings.TryGetValue(key, out value)
                && !string.IsNullOrEmpty(value);
        }

        private void ApplyOverlayLanguage(string language)
        {
            _overlayCache.Clear();
            if (!_catalogs.TryGetValue(language, out var catalog) || language == "en")
            {
                _overlayPhrases = Array.Empty<Phrase>();
                return;
            }
            _overlayPhrases = catalog.GetOrBuildPhrases();
        }

        private string CapitalizeStart(string text)
        {
            if (string.IsNullOrEmpty(text) || !char.IsLower(text[0]))
                return text;
            return char.ToUpper(text[0], OverlayCulture) + text.Substring(1);
        }

        private CultureInfo OverlayCulture
        {
            get
            {
                if (_catalogs.TryGetValue(_overlayLanguage, out var catalog)
                    && !string.IsNullOrWhiteSpace(catalog.Culture))
                {
                    try
                    {
                        return CultureInfo.GetCultureInfo(catalog.Culture);
                    }
                    catch (CultureNotFoundException)
                    {
                    }
                }
                return CultureInfo.InvariantCulture;
            }
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
                using var stream = assembly.GetManifestResourceStream(name);
                if (stream == null)
                    continue;
                using var reader = new StreamReader(stream);
                _catalogs[language] = Catalog.Parse(reader.ReadToEnd(), language);
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

        private sealed class Phrase
        {
            public Phrase(string pattern, string replacement)
            {
                Pattern = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
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
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                catalog.DisplayName = ReadString(root, "name") ?? language;
                catalog.Culture = ReadString(root, "culture");
                ReadMap(root, "strings", catalog.Strings);
                ReadMap(root, "overlay", catalog.Overlay);
                if (root.TryGetProperty("phrases", out var phrases) && phrases.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in phrases.EnumerateArray())
                    {
                        var pattern = item.GetProperty("pattern").GetString();
                        var replacement = item.GetProperty("replacement").GetString();
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
                    target[item.Name] = item.Value.GetString() ?? string.Empty;
            }
        }
    }
}
