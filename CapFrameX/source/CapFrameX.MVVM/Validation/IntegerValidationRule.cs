using System.Globalization;
using System.Windows.Controls;

namespace CapFrameX.MVVM.Validation
{
    public class IntegerValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            var inputString = value as string;

            return !IsValidInteger(inputString)
                 ? new ValidationResult(false, "Input not valid.")
                 : ValidationResult.ValidResult;
        }

        public bool IsValidInteger(string inputString)
        {
            return int.TryParse(inputString, out _);
        }
    }
}
