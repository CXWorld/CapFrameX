using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CapFrameX.Contracts.Localization
{
    /// <summary>
    /// UI strings and OSD labels. The interface language and the overlay language
    /// are independent. Lookup keys are the original English strings.
    /// </summary>
    public sealed class CxLang : INotifyPropertyChanged
    {
        public static CxLang Instance { get; } = new CxLang();

        private readonly Dictionary<string, string> _ui = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _overlay = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<(string En, string Ru)> _phrases = new List<(string, string)>();

        private string _uiLanguage = "en";
        private string _overlayLanguage = "en";

        public event PropertyChangedEventHandler PropertyChanged;
        public event Action OverlayLanguageChanged;

        private CxLang()
        {
            Load();
        }

        public string UiLanguage => _uiLanguage;
        public string OverlayLanguage => _overlayLanguage;

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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OverlayLanguage)));
            OverlayLanguageChanged?.Invoke();
        }

        public string T(string english)
        {
            if (string.IsNullOrEmpty(english) || _uiLanguage == "en")
                return english ?? string.Empty;
            return _ui.TryGetValue(english, out var translated) && !string.IsNullOrEmpty(translated)
                ? translated
                : english;
        }

        public string TranslateOverlay(string english)
        {
            if (string.IsNullOrEmpty(english) || _overlayLanguage == "en")
                return english ?? string.Empty;
            if (english.Any(c => c >= '\u0400' && c <= '\u04FF'))
                return english;
            if (_overlay.TryGetValue(english, out var exact) && !string.IsNullOrEmpty(exact))
                return exact;
            if (_ui.TryGetValue(english, out var fromUi) && !string.IsNullOrEmpty(fromUi))
                return fromUi;

            var phrase = english;
            foreach (var (en, ru) in _phrases)
                phrase = Regex.Replace(phrase, en, ru, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return phrase;
        }

        private void Load()
        {
            var assembly = typeof(CxLang).Assembly;
            using var stream = assembly.GetManifestResourceStream("CapFrameX.Contracts.Localization.Strings.ru.json");
            if (stream == null)
                return;

            using var reader = new StreamReader(stream);
            using var doc = JsonDocument.Parse(reader.ReadToEnd());
            var root = doc.RootElement;
            if (root.TryGetProperty("ui", out var ui))
            {
                foreach (var item in ui.EnumerateObject())
                    _ui[item.Name] = item.Value.GetString() ?? item.Name;
            }
            if (root.TryGetProperty("overlay", out var overlay))
            {
                foreach (var item in overlay.EnumerateObject())
                    _overlay[item.Name] = item.Value.GetString() ?? item.Name;
            }
            if (root.TryGetProperty("phrases", out var phrases))
            {
                foreach (var item in phrases.EnumerateArray())
                {
                    var en = item.GetProperty("en").GetString();
                    var ru = item.GetProperty("ru").GetString();
                    if (!string.IsNullOrEmpty(en) && ru != null)
                        _phrases.Add((en, ru));
                }
                _phrases.Sort((a, b) => b.En.Length.CompareTo(a.En.Length));
            }
        }

        private static string Normalize(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
                return "en";
            language = language.Trim().ToLowerInvariant();
            if (language.StartsWith("ru", StringComparison.Ordinal))
                return "ru";
            return "en";
        }
    }
}
