using RfidProgrammer.Models;
using System;
using System.IO.Ports;

namespace RfidProgrammer
{
    public delegate void ProgrammerOutputChangedEventHandler(ProgrammerOutputChangedEventArgs args);

    public delegate void ProgrammerAccessChangedEventHandler(ProgrammerAccessChangedEventArgs args);

    public delegate void ProgrammerCardChangedEventHandler(ProgrammerCardChangedEventArgs args);

    public delegate void ProgrammerStateChangedEventHandler(ProgrammerStateChangedEventArgs args);

    public interface IProgrammerService
    {
        int MaxBytes { get; }
        byte EndMarker { get; }

        event ProgrammerStateChangedEventHandler StateChanged;

        ProgrammerState CurrentState { get; }
        string CurrentPort { get; }

        void SwitchPort(string port, int baudRate = 38400, int dataBits = 8, Parity parity = Parity.None, StopBits stopBits = StopBits.One);

        void ClosePort();

        void WriteContent(byte[] content, uint start = 0, bool ignorePreviousEnd = true, bool ignoreEndMarker = false);

        void ReadContent(uint start = 0, uint length = 0);

        void EraseContent(uint start = 0);

        void ResetAccessAndKeys();

        void ReadAccessBits();

        void CheckKeys();

        void ChangeKeys(byte[] keyA, byte[] keyB, byte[] accessBits, SelectedKey selKey);

        void UseKeys(byte[] keyA, byte[] keyB, byte[] accessBits, SelectedKey selKey);

        event ProgrammerOutputChangedEventHandler OutputChanged;

        string Output { get; }

        void SendCustomCommand(string command);

        event ProgrammerAccessChangedEventHandler AccessChanged;

        IRfidAccess CurrentAccess { get; }

        event ProgrammerCardChangedEventHandler CardChanged;

        IRfidCard CurrentCard { get; }

        bool HasPendingWork { get; }

        string[] AvailablePorts { get; }
        int DefaultBaudRate { get; }
        int[] AvailableBaudRates { get; }
        int DefaultDataBits { get; }
        int[] AvailableDataBits { get; }
        Parity DefaultParity { get; }
        Parity[] AvailableParities { get; }
        StopBits DefaultStopBits { get; }
        StopBits[] AvailableStopBits { get; }
    }

    public class ProgrammerAccessChangedEventArgs : EventArgs
    {
        public ProgrammerAccessChangedEventArgs(IRfidAccess newAccess)
        {
            NewAccess = newAccess;
        }

        public IRfidAccess NewAccess { get; }
    }

    public class ProgrammerCardChangedEventArgs : EventArgs
    {
        public ProgrammerCardChangedEventArgs(IRfidCard newCard)
        {
            NewCard = newCard;
        }

        public IRfidCard NewCard { get; }
    }

    public class ProgrammerOutputChangedEventArgs : EventArgs
    {
        public ProgrammerOutputChangedEventArgs(string added, bool reset)
        {
            Added = added;
            Reset = reset;
        }

        public string Added { get; }
        public bool Reset { get; }
    }

    public class ProgrammerStateChangedEventArgs : EventArgs
    {
        public ProgrammerStateChangedEventArgs(ProgrammerState newState)
        {
            NewState = newState;
        }

        public ProgrammerState NewState { get; }
    }
}
