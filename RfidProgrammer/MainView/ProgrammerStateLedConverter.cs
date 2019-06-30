using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace RfidProgrammer.MainView
{
    public class ProgrammerStateLedConverter : IValueConverter
    {
        private RadialGradientBrush notConnectedBrush;
        private RadialGradientBrush connectedBrush;
        private RadialGradientBrush operationInProgressBrush;
        private RadialGradientBrush operationSuccessBrush;
        private RadialGradientBrush operationFailedBrush;
        private SolidColorBrush unknownStatusBrush;

        public ProgrammerStateLedConverter() : base()
        {
            notConnectedBrush = new RadialGradientBrush();
            notConnectedBrush.GradientOrigin = new Point(.75, .25);
            notConnectedBrush.GradientStops.Add(new GradientStop(Colors.Yellow, 0.0));
            notConnectedBrush.GradientStops.Add(new GradientStop(Colors.Orange, .4));
            notConnectedBrush.GradientStops.Add(new GradientStop(Colors.Red, 0.9));

            connectedBrush = new RadialGradientBrush();
            connectedBrush.GradientOrigin = new Point(.75, .25);
            connectedBrush.GradientStops.Add(new GradientStop(Colors.White, 0.0));
            connectedBrush.GradientStops.Add(new GradientStop(Colors.LimeGreen, .5));
            connectedBrush.GradientStops.Add(new GradientStop(Colors.Green, 1.0));

            operationInProgressBrush = new RadialGradientBrush();
            operationInProgressBrush.GradientOrigin = new Point(.75, .25);
            operationInProgressBrush.GradientStops.Add(new GradientStop(Colors.White, 0.0));
            operationInProgressBrush.GradientStops.Add(new GradientStop(Colors.LightYellow, .15));
            operationInProgressBrush.GradientStops.Add(new GradientStop(Colors.Orange, 1.0));

            operationSuccessBrush = new RadialGradientBrush();
            operationSuccessBrush.GradientOrigin = new Point(.75, .25);
            operationSuccessBrush.GradientStops.Add(new GradientStop(Colors.White, 0.0));
            operationSuccessBrush.GradientStops.Add(new GradientStop(Colors.LimeGreen, .4));
            operationSuccessBrush.GradientStops.Add(new GradientStop(Colors.DarkCyan, 1.0));

            operationFailedBrush = new RadialGradientBrush();
            operationFailedBrush.GradientOrigin = new Point(.75, .25);
            operationFailedBrush.GradientStops.Add(new GradientStop(Colors.White, 0.0));
            operationFailedBrush.GradientStops.Add(new GradientStop(Colors.MediumVioletRed, .5));
            operationFailedBrush.GradientStops.Add(new GradientStop(Colors.BlueViolet, 1.0));

            unknownStatusBrush = new SolidColorBrush(Colors.WhiteSmoke);
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is ProgrammerState))
                return DependencyProperty.UnsetValue;

            switch ((ProgrammerState)value)
            {
                case ProgrammerState.NotConnected:
                    return notConnectedBrush;

                case ProgrammerState.Connected:
                    return connectedBrush;

                case ProgrammerState.ErrorChecking:
                case ProgrammerState.OperationInProgress:
                    return operationInProgressBrush;

                case ProgrammerState.OperationSuccess:
                    return operationSuccessBrush;

                case ProgrammerState.OperationFailed:
                    return operationFailedBrush;

                case ProgrammerState.Connecting:
                case ProgrammerState.Unknown:
                default:
                    return unknownStatusBrush;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException(); // not needed
        }
    }
}
