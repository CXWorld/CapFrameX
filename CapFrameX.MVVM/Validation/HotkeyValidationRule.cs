using System.Globalization;
using System.Windows.Controls;

namespace CapFrameX.MVVM.Validation
{
    public class HotkeyValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            var inputString = value as string;

            return !CapFrameX.Hotkey.CaptureHotkey.IsValidHotkey(inputString)
                 ? new ValidationResult(false, "Hotkey is not valid.")
                 : ValidationResult.ValidResult;
        }
    }
}
