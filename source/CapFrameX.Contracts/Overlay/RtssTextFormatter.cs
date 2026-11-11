using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CapFrameX.Contracts.Localization;

namespace CapFrameX.Contracts.Overlay
{
    /// <summary>
    /// Localizes only RTSS presentation text. Persisted entries and API values remain neutral.
    /// RTSS consumes the Windows ANSI codepage; translations it cannot represent fall back to English.
    /// </summary>
    public static class RtssTextFormatter
    {
        private static readonly Regex FormatParts = new Regex(@"(<[^>]*>|\{[^}]*\})|([^<{]+)", RegexOptions.Compiled);
        private static readonly ConcurrentDictionary<int, Encoding> Encodings = new ConcurrentDictionary<int, Encoding>();
        private static readonly ConcurrentDictionary<(int CodePage, string Text), bool> SupportedText = new ConcurrentDictionary<(int, string), bool>();
        // Translated value formats per overlay language and code page. A format comes from the
        // entry's settings, not from its value, so few exist; splitting each one with the regex
        // on every refresh, for every entry, was the expensive part of formatting.
        internal const int MaxCachedFormats = 4096;
        private static readonly ConcurrentDictionary<(string Language, int CodePage, string Format), string> TranslatedFormats =
            new ConcurrentDictionary<(string, int, string), string>();

        internal static int CachedFormatCount => TranslatedFormats.Count;

        internal static void ClearCachedFormats() => TranslatedFormats.Clear();

        public static string Translate(string text, int codePage)
        {
            var translated = CxLang.Instance.TranslateOverlay(text);
            if (translated == text || string.IsNullOrEmpty(translated))
                return translated;

            bool supported = SupportedText.GetOrAdd((codePage, translated), key =>
            {
                var encoding = Encodings.GetOrAdd(key.CodePage, page => page == 65001
                    ? new UTF8Encoding(false, true)
                    : CodePagesEncodingProvider.Instance.GetEncoding(page,
                        EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback));
                if (encoding == null)
                    return false;
                try
                {
                    encoding.GetByteCount(key.Text);
                    return true;
                }
                catch (EncoderFallbackException)
                {
                    return false;
                }
            });
            return supported ? translated : text ?? string.Empty;
        }

        public static string FormatGroupName(IOverlayEntry entry, int codePage)
        {
            var name = Translate(entry.GroupName ?? string.Empty, codePage);
            return string.IsNullOrWhiteSpace(entry.GroupNameFormat) ? name
                : string.Format(CultureInfo.InvariantCulture, entry.GroupNameFormat, name);
        }

        public static string FormatValue(IOverlayEntry entry, int codePage)
        {
            object value = entry.Value;
            if (entry.Identifier == "CaptureServiceStatus" || entry.Identifier == "HookOverlayStatus")
                value = Translate(value?.ToString() ?? string.Empty, codePage);

            if (string.IsNullOrWhiteSpace(entry.ValueFormat))
                return value?.ToString() ?? string.Empty;

            return string.Format(CultureInfo.InvariantCulture, GetTranslatedFormat(entry.ValueFormat, codePage), value);
        }

        private static string GetTranslatedFormat(string valueFormat, int codePage)
        {
            string language = CxLang.Instance.OverlayLanguage;
            var key = (language, codePage, valueFormat);
            if (TranslatedFormats.TryGetValue(key, out var format))
                return format;

            // Leave color/size tags and numeric format placeholders intact. Never translate the
            // formatted number or arbitrary hardware names (nor cache their changing values).
            format = FormatParts.Replace(valueFormat, part => part.Groups[2].Success
                ? Translate(part.Value, codePage) : part.Value);

            // A language switch during the translation must not file its result under the old
            // language.
            if (CxLang.Instance.OverlayLanguage == language)
            {
                if (TranslatedFormats.Count >= MaxCachedFormats)
                    TranslatedFormats.Clear();
                TranslatedFormats[key] = format;
            }
            return format;
        }
    }
}
