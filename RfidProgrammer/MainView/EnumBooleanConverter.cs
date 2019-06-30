using System;
using System.Globalization;
using System.Windows.Data;

namespace RfidProgrammer.MainView
{
    public class EnumBooleanConverter : IValueConverter
    {
        // https://stackoverflow.com/a/20730096/5516047
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value.Equals(parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool && ((bool)value) ? parameter : Binding.DoNothing;
        }
    }
}
