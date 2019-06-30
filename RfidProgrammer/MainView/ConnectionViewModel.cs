using Prism.Commands;
using Prism.Mvvm;
using System;
using System.ComponentModel.Composition;
using System.IO.Ports;

namespace RfidProgrammer.MainView
{
    [Export(typeof(IConnectionViewModel))]
    public class ConnectionViewModel : BindableBase, IConnectionViewModel
    {
        private IProgrammerService programmer;
        public ProgrammerState State => programmer.CurrentState;
        public string CurrentPort => programmer.CurrentPort ?? "...";


        public string[] AvailablePorts => programmer.AvailablePorts;

        private string selectedPort;

        public string SelectedPort
        {
            get { return selectedPort; }
            set { SetProperty(ref selectedPort, value); }
        }

        public int[] AvailableBaudRates => programmer.AvailableBaudRates;

        private int selectedBaudRate;

        public int SelectedBaudRate
        {
            get { return selectedBaudRate; }
            set { SetProperty(ref selectedBaudRate, value); }
        }

        public int[] AvailableDataBits => programmer.AvailableDataBits;

        private int selectedDataBits;

        public int SelectedDataBits
        {
            get { return selectedDataBits; }
            set { SetProperty(ref selectedDataBits, value); }
        }

        public StopBits[] AvailableStopBits => programmer.AvailableStopBits;

        private StopBits selectedStopBits;

        public StopBits SelectedStopBits
        {
            get { return selectedStopBits; }
            set { SetProperty(ref selectedStopBits, value); }
        }

        public Parity[] AvailableParities => programmer.AvailableParities;

        private Parity selectedParity;

        public Parity SelectedParity
        {
            get { return selectedParity; }
            set { SetProperty(ref selectedParity, value); }
        }

        private bool connected;
        public string ConnectionString => programmer.CurrentPort == null ? "Connect" : "Disconnect";

        public DelegateCommand ConnectionCommand { get; }

        [ImportingConstructor]
        public ConnectionViewModel(IProgrammerService programmer) : base()
        {
            if (programmer == null)
                throw new ArgumentNullException(nameof(programmer));
            // initialize values
            this.programmer = programmer;
            programmer.StateChanged += args =>
            {
                RaisePropertyChanged(nameof(State));
                RaisePropertyChanged(nameof(CurrentPort));
            };
            connected = programmer.CurrentState != ProgrammerState.NotConnected;
            programmer.StateChanged += args =>
            {
                var con = programmer.CurrentState != ProgrammerState.NotConnected;
                if (connected != con)
                {
                    connected = con;
                    RaisePropertyChanged(nameof(ConnectionString));
                }
            };
            SelectedPort = AvailablePorts.Length != 0 ? AvailablePorts[0] : null;
            SelectedBaudRate = programmer.DefaultBaudRate;
            SelectedDataBits = programmer.DefaultDataBits;
            SelectedStopBits = programmer.DefaultStopBits;
            SelectedParity = programmer.DefaultParity;
            ConnectionCommand = new DelegateCommand(ConnectionAction, () => SelectedPort != null).ObservesProperty(() => SelectedPort);
        }

        private void ConnectionAction()
        {
            if (programmer.CurrentPort == null)
                programmer.SwitchPort(SelectedPort, SelectedBaudRate, SelectedDataBits, SelectedParity, SelectedStopBits);
            else
                programmer.ClosePort();
        }
    }
}
