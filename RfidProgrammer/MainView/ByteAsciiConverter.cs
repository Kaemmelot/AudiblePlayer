using System;
using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace RfidProgrammer.MainView
{
    public class ByteAsciiConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is byte[] ? Encoding.ASCII.GetString((byte[])value) : null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is string ? Encoding.ASCII.GetBytes((string)value) : null;
        }
    }
}
