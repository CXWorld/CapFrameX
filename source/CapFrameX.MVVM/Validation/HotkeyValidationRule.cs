using System.Globalization;
using System.Windows.Controls;

namespace CapFrameX.MVVM.Validation
{
    public class HotkeyValidationRule : ValidationRule
    {
        public const string UnsetText = "Not set";

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            // TargetNullValue is presentation only; the configuration stores an empty string.
            if (value == null || Equals(value, UnsetText))
                return ValidationResult.ValidResult;

            var inputString = value.ToString();

            return !CapFrameX.Hotkey.CXHotkey.IsValidSetting(inputString)
                 ? new ValidationResult(false, "Hotkey is not valid.")
                 : ValidationResult.ValidResult;
        }
    }
}
