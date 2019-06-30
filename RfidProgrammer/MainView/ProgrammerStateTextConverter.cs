using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RfidProgrammer.MainView
{
    public class ProgrammerStateTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is ProgrammerState))
                return DependencyProperty.UnsetValue;

            switch ((ProgrammerState)value)
            {
                case ProgrammerState.NotConnected:
                    return "Not conntected";

                case ProgrammerState.Connecting:
                    return "Connecting";

                case ProgrammerState.Connected:
                    return "Ready";

                case ProgrammerState.ErrorChecking:
                case ProgrammerState.OperationInProgress:
                    return "Operation in progress";

                case ProgrammerState.OperationSuccess:
                    return "Operation successful";

                case ProgrammerState.OperationFailed:
                    return "Operation failed";

                case ProgrammerState.Unknown:
                default:
                    return "Unknown state, please reconnect";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException(); // not needed
        }
    }
}
