using Prism.Commands;
using System.ComponentModel;
using System.IO.Ports;

namespace RfidProgrammer.MainView
{
    public interface IConnectionViewModel : INotifyPropertyChanged
    {
        string CurrentPort { get; }

        string[] AvailablePorts { get; }

        string SelectedPort { get; set; }

        int[] AvailableBaudRates { get; }

        int SelectedBaudRate { get; set; }

        int[] AvailableDataBits { get; }

        int SelectedDataBits { get; set; }

        StopBits[] AvailableStopBits { get; }

        StopBits SelectedStopBits { get; set; }

        Parity[] AvailableParities { get; }

        Parity SelectedParity { get; set; }

        string ConnectionString { get; }

        DelegateCommand ConnectionCommand { get; }
    }
}
