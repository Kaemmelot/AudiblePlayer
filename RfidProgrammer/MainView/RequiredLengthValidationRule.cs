using System.Globalization;
using System.Windows.Controls;

namespace RfidProgrammer.MainView
{
    public class RequiredLengthValidationRule : ValidationRule
    {
        public int RequiredLength { get; set; }

        public bool IgnoreWhitespaces { get; set; }

        public RequiredLengthValidationRule()
        {
            RequiredLength = 0;
            IgnoreWhitespaces = true;
        }

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            var val = value as string;
            if (val == null || (IgnoreWhitespaces && RequiredLength > val.Replace(" ", "").Length) || (!IgnoreWhitespaces && RequiredLength > val.Length))
                return new ValidationResult(false, "too short");
            return ValidationResult.ValidResult;
        }
    }
}
