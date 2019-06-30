using Prism.Events;
using RfidProgrammer.Events;
using RfidProgrammer.Models;
using RfidProgrammer.ProgrammerService.Commands;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RfidProgrammer.ProgrammerService
{
    [Export(typeof(IProgrammerService))]
    public class ProgrammerService : IProgrammerService
    {
        [Import(typeof(IRfidCardFactory))]
        internal IRfidCardFactory rfidFactory;

        internal IEventAggregator eventAggr;

        public const byte UsableSectors = 16;
        public const byte Blocks = UsableSectors * 4; // 64
        public const byte UsableBlocks = UsableSectors * 3 - 1; // 47
        public const int UsableBytes = UsableBlocks * 16; // 752

        [ImportingConstructor]
        public ProgrammerService(IEventAggregator eventAggr)
        {
            this.eventAggr = eventAggr;
            stateMachine.StateChanged += args => StateChanged.Invoke(new ProgrammerStateChangedEventArgs(args.NewState));
            eventAggr.GetEvent<ShutdownEvent>().Subscribe(shutdown => stopWorker = true);
        }

        public int MaxBytes => UsableBytes - 1; // with end char!
        public bool HasPendingWork => !cmdQueue.IsEmpty;
        public byte EndMarker => 0x04;

        private StringBuilder output = new StringBuilder();

        public string Output
        {
            get
            {
                lock (output)
                    return output.ToString();
            }
        }

        internal void AppendOutput(string text, bool serviceMessage = false)
        {
            text = new string(text.Select(c => c != '\n' && char.IsControl(c) ? '?' : c).ToArray());
            if (!serviceMessage)
            {
                lock (output)
                    output.Append(text + '\n');
                OutputChanged?.Invoke(new ProgrammerOutputChangedEventArgs(text + '\n', false));
            }
            eventAggr.GetEvent<ProgrammerOutputEvent>().Publish(new ProgrammerOutput(text, serviceMessage));
        }

        internal void SerialWriteLine(byte[] line, bool userInput = false)
        {
            if (connection == null)
                throw new InvalidOperationException("No port connected");

            connection.WriteBytes(line);
            connection.WriteLine();
            var lineStr = new string(Encoding.ASCII.GetString(line).Select(c => char.IsControl(c) ? '?' : c).ToArray());
            eventAggr.GetEvent<ProgrammerInputEvent>().Publish(new ProgrammerInput(lineStr, userInput));
        }

        public event ProgrammerOutputChangedEventHandler OutputChanged;

        private IRfidAccess currentAccess = null;
        private IRfidCard currentCard = null;

        public IRfidAccess CurrentAccess
        {
            get { return currentAccess; }
            internal set
            {
                currentAccess = value;
                AccessChanged?.Invoke(new ProgrammerAccessChangedEventArgs(value));
            }
        }

        public IRfidCard CurrentCard
        {
            get { return currentCard; }
            internal set
            {
                currentCard = value;
                CardChanged?.Invoke(new ProgrammerCardChangedEventArgs(value));
            }
        }

        public event ProgrammerAccessChangedEventHandler AccessChanged;

        public event ProgrammerCardChangedEventHandler CardChanged;

        private Thread workerThread = null;
        private ConcurrentQueue<ICommand> cmdQueue = new ConcurrentQueue<ICommand>();
        private bool stopWorker = false;
        private TaskScheduler scheduler = new ConcurrentExclusiveSchedulerPair(TaskScheduler.Default, 1).ExclusiveScheduler;
        internal byte[] lastCmd = null;
        internal List<byte> result = null;

        private void StopWorker()
        {
            if (workerThread != null && !stopWorker)
            {
                stopWorker = true;
                workerThread.Interrupt();
                workerThread.Join();
                stopWorker = false;
                workerThread = null;
                CurrentCard = null;
                AppendOutput("\n===============", true);
            }
        }

        public int DefaultBaudRate => 115200;
        public int[] AvailableBaudRates => new int[] { 300, 600, 1200, 2400, 4800, 9600, 14400, 19200, 28800, 31250, 38400, 57600, 115200, 230400, 250000, 500000, 1000000 };
        public int DefaultDataBits => 8;
        public int[] AvailableDataBits => new int[] { 5, 6, 7, 8 };
        public Parity DefaultParity => Parity.None;
        public Parity[] AvailableParities => new Parity[] { Parity.None, Parity.Even, Parity.Odd, Parity.Mark, Parity.Space };
        public StopBits DefaultStopBits => StopBits.One;
        public StopBits[] AvailableStopBits => new StopBits[] { StopBits.One, StopBits.OnePointFive, StopBits.Two };

        private SerialConnection connection = null;
        internal ProtocolStateMachine stateMachine = new ProtocolStateMachine();

        public ProgrammerState CurrentState => stateMachine.CurrentState;

        public event ProgrammerStateChangedEventHandler StateChanged;

        public string CurrentPort => connection?.Port;

        private void Worker(string port, int baudRate, int dataBits, Parity parity, StopBits stopBits)
        {
            using (connection = new SerialConnection(port, baudRate, dataBits, parity, stopBits, true))
            {
                try
                {
                    stateMachine.MoveNextOrFail(ServiceEvent.Connect);
                    CurrentAccess = rfidFactory.CreateRfidAccess();
                    // connected once init received
                    ICommand c;
                    byte[] line;
                    while (!stopWorker && connection.IsOpen)
                    {
                        if (stateMachine.CurrentState != ProgrammerState.Connecting && cmdQueue.TryDequeue(out c))
                        {
                            stateMachine.MoveNextOrFail(ServiceEvent.NextOperation);
                            try
                            {
                                c.Task.RunSynchronously(scheduler);
                            }
                            catch (InvalidOperationException)
                            {
                                stateMachine.MoveNextOrFail(ServiceEvent.Failure);
                            }
                        }
                        else
                        {
                            while (!stopWorker && connection.IsOpen && cmdQueue.IsEmpty)
                            {
                                if (connection.TryReadLine(out line))
                                    HandleSerialInput(line);
                                else
                                    Thread.Sleep(250);
                            }
                        }
                    }
                }
                catch (ThreadInterruptedException) { } // can be safely ignored
                catch (Exception)
                {
                    stateMachine.MoveNext(ServiceEvent.Failure);
                }
                finally
                {
                    stopWorker = true;
                    CurrentAccess = null;
                    stateMachine.MoveNext(ServiceEvent.Disconnect);
                }
            }
            connection = null;
        }

        internal bool WriteCommand(ServiceEvent command, byte[] args)
        {
            stateMachine.MoveNext(ServiceEvent.NextOperation); // move on if needed
            if (stateMachine.MoveNext(command)) // if command is allowed right now
            {
                lastCmd = new byte[args.Length + 1];
                lastCmd[0] = Encoding.ASCII.GetBytes(new char[] { (char)command })[0];
                Array.Copy(args, 0, lastCmd, 1, args.Length);
                result = null;
                SerialWriteLine(lastCmd);
                return true;
            }
            return false;
        }

        internal void HandleSerialInput(byte[] line)
        {
            var lineStr = Encoding.ASCII.GetString(line);
            var ev = line.Length > 0 ? (ProgrammerEvent)lineStr[0] : ProgrammerEvent.Nothing;
            if (stateMachine.CurrentState == ProgrammerState.Connecting)
            {
                if (ev != ProgrammerEvent.InitComplete)
                    return; // ignore all before init
                else
                    stateMachine.MoveNextOrFail(ev);
            }

            AppendOutput(lineStr);
            if (ev == ProgrammerEvent.Nothing || ev == ProgrammerEvent.InitComplete)
                return;

            if (!Enum.IsDefined(typeof(ProgrammerEvent), ev) || ev == ProgrammerEvent.Failure)
            {
                AppendOutput("^--- ERROR: This is an invalid message!", true);
                stateMachine.MoveNextOrFail(ProgrammerEvent.Failure);
                return;
            }
            line = line.Skip(1).ToArray();

            stateMachine.MoveNextOrFail(ev); // move state machine

            if (ev == ProgrammerEvent.CardChange)
            {
                if (line.Length > 0)
                    CurrentCard = rfidFactory.CreateRfidCard(line);
                else
                    CurrentCard = null;
            }
            else if (stateMachine.CurrentState == ProgrammerState.ErrorChecking)
            {
                var answer = line.SequenceEqual(lastCmd) ? ServiceEvent.Ack : ServiceEvent.Nack;
                stateMachine.MoveNextOrFail(answer);
                SerialWriteLine(Encoding.ASCII.GetBytes(((char)answer).ToString()));
            }
            else if (ev == ProgrammerEvent.ErrorCheck)
            {
                // not in ErrorChecking state -> User operation
                AppendOutput(line.SequenceEqual(lastCmd) ? "^--- Command matches" : "^--- Command differs", true);
            }
            else if (ev == ProgrammerEvent.PartialResult)
            {
                if (result == null)
                    result = new List<byte>();
                result.AddRange(line);
            }
        }

        internal bool WaitForEndOfOperation()
        {
            byte[] line = null;
            while (!stopWorker && connection.IsOpen && stateMachine.CurrentState == ProgrammerState.OperationInProgress)
            {
                if (connection.TryReadLine(out line))
                    HandleSerialInput(line);
                else
                    Thread.Sleep(250);
            }
            return !stopWorker && stateMachine.CurrentState != ProgrammerState.OperationInProgress;
        }

        public string[] AvailablePorts => SerialPort.GetPortNames();

        public void ClosePort()
        {
            StopWorker();
            workerThread = null;
            ICommand ignored;
            while (cmdQueue.TryDequeue(out ignored))
                ; // Clear
        }

        public void SwitchPort(string port, int baudRate = 38400, int dataBits = 8, Parity parity = Parity.None, StopBits stopBits = StopBits.One)
        {
            if (port == null || !AvailablePorts.Contains(port))
                throw new ArgumentException("Port is unavailable");
            if (connection != null)
                ClosePort();
            stopWorker = false;
            workerThread = new Thread(() => Worker(port, baudRate, dataBits, parity, stopBits));
            workerThread.Start();
        }

        public bool IsConnected => connection != null && connection.IsOpen;

        internal void AddCommandToQueue(ICommand cmd)
        {
            cmdQueue.Enqueue(cmd);
        }

        public void ChangeKeys(byte[] keyA, byte[] keyB, byte[] accessBits, SelectedKey selKey)
        {
            if (!IsConnected)
                throw new InvalidOperationException("No port connected");
            if (CurrentCard == null)
                throw new InvalidOperationException("No card connected");
            if (keyA.Length != 6 || keyB.Length != 6 || accessBits.Length != 4)
                throw new InvalidOperationException("KeyA and KeyB must be 6, AccessBits must be 4 bytes long");

            AddCommandToQueue(new ChangeKeysCommand(this) { KeyA = keyA, KeyB = keyB, AccessBits = accessBits, SelectedKey = selKey });
        }

        public void CheckKeys()
        {
            if (!IsConnected)
                throw new InvalidOperationException("No port connected");
            if (CurrentCard == null)
                throw new InvalidOperationException("No card connected");

            AddCommandToQueue(new CheckKeysCommand(this));
        }

        internal byte GetBlockByContentIndex(uint index)
        {
            if (index < 32) // first two blocks are easy
                return (byte)(index / 16 + 1);
            else if (index >= UsableBlocks * 16)
                throw new InvalidOperationException("Cannot access blocks beyond the content end");
            byte block = 4; // ignore first block with producer information and we already know it's longer than 2 blocks so we skip them
            index -= 32;
            int b = 0;
            while (index >= 16)
            {
                block++;
                index -= 16;
                if (++b == 4) // ignore blocks with access bits
                {
                    b = 0;
                    block++;
                }
            }
            return block;
        }

        internal byte[] TrimContent(byte[] content)
        {
            if (content.All(b => b == 0)) // just empty
                return new byte[0];

            byte[] result;
            var endCharPos = Array.IndexOf(content, EndMarker);
            if (endCharPos != -1) // there is an end char
            {
                result = new byte[endCharPos];
                Array.Copy(content, result, endCharPos);
                return result;
            }

            if (content[content.Length - 1] != 0)
                return content; // cannot trim further

            for (var i = content.Length - 2; i >= 0; i--) // find last on zero byte
            {
                if (content[i] != 0)
                {
                    endCharPos = i + 1;
                    break;
                }
            }

            // trim away all trailing zero bytes
            result = new byte[endCharPos];
            Array.Copy(content, result, endCharPos);
            return result;
        }

        public void ReadContent(uint start = 0, uint length = 0)
        {
            if (!IsConnected)
                throw new InvalidOperationException("No port connected");
            if (CurrentCard == null)
                throw new InvalidOperationException("No card connected");

            AddCommandToQueue(new ReadContentCommand(this) { Start = start, Length = length });
        }

        public void ReadAccessBits()
        {
            if (!IsConnected)
                throw new InvalidOperationException("No port connected");
            if (CurrentCard == null)
                throw new InvalidOperationException("No card connected");

            AddCommandToQueue(new ReadAccessBitsCommand(this));
        }

        public void ResetAccessAndKeys()
        {
            if (!IsConnected)
                throw new InvalidOperationException("No port connected");
            if (CurrentCard == null)
                throw new InvalidOperationException("No card connected");

            AddCommandToQueue(new ResetAccessAndKeysCommand(this));
        }

        public void SendCustomCommand(string command)
        {
            if (command == null || command.Length == 0)
                return;
            if (!IsConnected)
                throw new InvalidOperationException("No port connected");

            AddCommandToQueue(new SendCustomCommandCommand(this) { Command = command });
        }

        public void UseKeys(byte[] keyA, byte[] keyB, byte[] accessBits, SelectedKey selKey)
        {
            if (!IsConnected)
                throw new InvalidOperationException("No port connected");
            if (keyA.Length != 6 || keyB.Length != 6 || accessBits.Length != 4)
                throw new InvalidOperationException("KeyA and KeyB must be 6, AccessBits must be 4 bytes long");

            AddCommandToQueue(new UseKeysCommand(this) { KeyA = keyA, KeyB = keyB, AccessBits = accessBits, SelectedKey = selKey });
        }

        public void WriteContent(byte[] content, uint start = 0, bool ignorePreviousEnd = true, bool ignoreEndMarker = false)
        {
            if (!IsConnected)
                throw new InvalidOperationException("No port connected");
            if (CurrentCard == null)
                throw new InvalidOperationException("No card connected");
            if (content == null)
                content = new byte[0];
            if (start + content.Length > UsableBytes)
                throw new InvalidOperationException("Not enough space to write this kind of content");

            if (CurrentCard.Content == null)
                ReadContent(); // read content first; this is especially needed for replacing certain parts

            AddCommandToQueue(new WriteContentCommand(this) { Content = content, Start = start, IgnorePreviousEnd = ignorePreviousEnd, IgnoreEndMarker = ignoreEndMarker });
        }

        public void EraseContent(uint start = 0)
        {
            if (!IsConnected)
                throw new InvalidOperationException("No port connected");
            if (CurrentCard == null)
                throw new InvalidOperationException("No card connected");
            if (start > UsableBytes) // assuming each char takes one byte (ASCII)
                throw new InvalidOperationException("Cannot erase after card end");

            if (CurrentCard.Content == null)
                ReadContent();

            AddCommandToQueue(new EraseContentCommand(this) { Start = start });
        }
    }
}
