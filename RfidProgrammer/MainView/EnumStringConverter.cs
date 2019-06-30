using System;
using System.Globalization;
using System.Windows.Data;

namespace RfidProgrammer.MainView
{
    public class EnumStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var type = value.GetType();
            return type.IsEnum ? Enum.GetName(type, value) : Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var type = parameter != null ? parameter.GetType() : null;
            return type != null && type.IsEnum && value is string ? Enum.Parse(type, (string)value) : Binding.DoNothing;
        }
    }
}
