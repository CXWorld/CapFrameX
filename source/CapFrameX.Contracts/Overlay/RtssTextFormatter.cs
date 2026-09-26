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

            // Leave color/size tags and numeric format placeholders intact. Never translate the
            // formatted number or arbitrary hardware names (nor cache their changing values).
            var format = FormatParts.Replace(entry.ValueFormat, part => part.Groups[2].Success
                ? Translate(part.Value, codePage) : part.Value);
            return string.Format(CultureInfo.InvariantCulture, format, value);
        }
    }
}
