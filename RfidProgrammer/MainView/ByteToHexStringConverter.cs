using System;
using System.Globalization;
using System.Windows.Data;

namespace RfidProgrammer.MainView
{
    public class ByteToHexStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is byte[] ? ByteArrayHelper.ByteToSpacedHexString((byte[])value) : null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is string ? ByteArrayHelper.HexStringToBytes((string)value) : null;
        }
    }
}
