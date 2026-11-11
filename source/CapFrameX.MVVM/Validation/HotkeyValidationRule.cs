using System.Collections.Concurrent;
using System.Globalization;
using System.Windows.Controls;
using CapFrameX.Contracts.Localization;

namespace CapFrameX.MVVM.Validation
{
    public class HotkeyValidationRule : ValidationRule
    {
        // Every text handed out so far: a field keeps showing the text of the language it was
        // bound in, and that text must stay valid after the UI language changes.
        private static readonly ConcurrentDictionary<string, byte> IssuedUnsetTexts = new ConcurrentDictionary<string, byte>();

        public static string UnsetText
        {
            get
            {
                var text = CxLang.T("HotkeyValidationRule_NotSet");
                IssuedUnsetTexts.TryAdd(text, 0);
                return text;
            }
        }

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            // TargetNullValue is presentation only; the configuration stores an empty string.
            if (value == null || value is string text && (text == UnsetText || IssuedUnsetTexts.ContainsKey(text)))
                return ValidationResult.ValidResult;

            var inputString = value.ToString();

            return !CapFrameX.Hotkey.CXHotkey.IsValidSetting(inputString)
                 ? new ValidationResult(false, CxLang.T("HotkeyValidationRule_HotkeyIsNotValid"))
                 : ValidationResult.ValidResult;
        }
    }
}
