using Microsoft.Win32;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using RfidProgrammer.Models;
using RfidProgrammer.Properties;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace RfidProgrammer.MainView
{
    [Export]
    public class RfidCardViewModel : BindableBase
    {
        protected IRfidCard cardModel;
        protected IRfidAccess accessModel;
        protected IProgrammerService programmer;

        [Import(typeof(IRfidCardFactory))]
        protected IRfidCardFactory factory;

        public DelegateCommand WriteCommand { get; }
        public DelegateCommand ReadCommand { get; }
        public DelegateCommand EraseCardCommand { get; }
        public DelegateCommand ResetAccessAndKeysCommand { get; }
        public DelegateCommand ReadAccessBitsCommand { get; }
        public DelegateCommand CheckKeysCommand { get; }
        public DelegateCommand ChangeKeysCommand { get; }
        public DelegateCommand UseKeysCommand { get; }
        public DelegateCommand SendCommandCommand { get; }

        public IConnectionViewModel ConnectionViewModel { get; }

        public DelegateCommand<Window> SaveKeysCommand { get; }
        public DelegateCommand<Window> LoadKeysCommand { get; }

        [ImportingConstructor]
        public RfidCardViewModel(IEventAggregator eventAggr, IProgrammerService programmer, IConnectionViewModel conViewModel) : base()
        {
            if (eventAggr == null)
                throw new ArgumentNullException(nameof(eventAggr));
            if (programmer == null)
                throw new ArgumentNullException(nameof(programmer));
            if (conViewModel == null)
                throw new ArgumentNullException(nameof(conViewModel));
            // initialize values
            this.programmer = programmer;
            var dispatcher = Application.Current.Dispatcher;
            UpdateCard(new ProgrammerCardChangedEventArgs(programmer.CurrentCard));
            UpdateAccess(new ProgrammerAccessChangedEventArgs(programmer.CurrentAccess));
            // subscribe to events
            programmer.CardChanged += card => dispatcher.BeginInvoke(new Action<ProgrammerCardChangedEventArgs>(UpdateCard), new object[] { card });
            programmer.AccessChanged += access => dispatcher.BeginInvoke(new Action<ProgrammerAccessChangedEventArgs>(UpdateAccess), new object[] { access });
            var stateChangedAction = new Action(() =>
            {
                RaisePropertyChanged(nameof(State));
                RaisePropertyChanged(nameof(CardReady));
                RaisePropertyChanged(nameof(ConnectedAndReady));
                RaisePropertyChanged(nameof(AccessChanged));
                RaisePropertyChanged(nameof(AccessChangedCardReady));
                RaisePropertyChanged(nameof(Connected));
            });
            programmer.StateChanged += args =>
            {
                dispatcher.BeginInvoke(stateChangedAction);
            };

            // commands
            WriteCommand = new DelegateCommand(() => {
                if (Content != null)
                {
                    var currContent = cardModel.Content != null ? Encoding.ASCII.GetString(cardModel.Content) : null;
                    var start = currContent != null ? currContent.TakeWhile((c, i) => i < Content.Length && c == Content[i]).Count() : 0;
                    if (start != Content.Length)
                    {
                        var newContent = Content.Substring(start); // ignore start if equal
                        programmer.WriteContent(Encoding.ASCII.GetBytes(newContent), (uint)start);
                    }
                    else
                        programmer.WriteContent(Encoding.ASCII.GetBytes(Content)); // overwrite all
                }
                else
                    programmer.WriteContent(null);
            }).ObservesCanExecute(() => CardReady);
            ReadCommand = new DelegateCommand(() => programmer.ReadContent()).ObservesCanExecute(() => CardReady);
            EraseCardCommand = new DelegateCommand(() => programmer.EraseContent()).ObservesCanExecute(() => CardReady);
            ResetAccessAndKeysCommand = new DelegateCommand(() => programmer.ResetAccessAndKeys()).ObservesCanExecute(() => ConnectedAndReady);
            ReadAccessBitsCommand = new DelegateCommand(() => programmer.ReadAccessBits()).ObservesCanExecute(() => CardReady);
            CheckKeysCommand = new DelegateCommand(() => programmer.CheckKeys()).ObservesCanExecute(() => AccessChangedCardReady);
            ChangeKeysCommand = new DelegateCommand(() => programmer.ChangeKeys(KeyA, KeyB, AccessBits, SelectedKey)).ObservesCanExecute(() => AccessChangedCardReady);
            UseKeysCommand = new DelegateCommand(() => programmer.UseKeys(KeyA, KeyB, AccessBits, SelectedKey)).ObservesCanExecute(() => AccessChanged);
            SendCommandCommand = new DelegateCommand(() =>
            {
                if (ConsoleInput != null && ConsoleInput.Length > 0)
                {
                    programmer.SendCustomCommand(ConsoleInput);
                    ConsoleInput = "";
                }
            }).ObservesCanExecute(() => Connected);
            ConnectionViewModel = conViewModel;
            SaveKeysCommand = new DelegateCommand<Window>(SaveKeys).ObservesCanExecute(() => ConnectedAndReady);
            LoadKeysCommand = new DelegateCommand<Window>(LoadKeys).ObservesCanExecute(() => ConnectedAndReady);
        }

        private static string DialogFilter = "Rfid-Programmer Access Keys|*.rpk|All Files|*.*";

        private void SaveKeys(Window currentWindow)
        {
            // create a copy of the visible keys
            var copy = factory.CreateRfidAccess(KeyA, KeyB, AccessBits, SelectedKey);

            // create dialog
            var dialog = new SaveFileDialog();
            dialog.FileName = "programmer";
            dialog.DefaultExt = "rpk";
            dialog.Filter = DialogFilter;
            if (string.IsNullOrEmpty(Settings.Default.AccessKeysDirectory))
                Settings.Default.AccessKeysDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            dialog.InitialDirectory = Settings.Default.AccessKeysDirectory;

            // ask user
            if (dialog.ShowDialog(currentWindow) != true)
                return;

            // handle result
            Settings.Default.AccessKeysDirectory = Path.GetDirectoryName(dialog.FileName);
            Settings.Default.Save();
            try
            {
                XmlHelper.WriteXmlFile(dialog.FileName, copy);
            }
            catch (Exception) { }
        }

        private void LoadKeys(Window currentWindow)
        {
            // create dialog
            var dialog = new OpenFileDialog();
            dialog.FileName = "programmer";
            dialog.DefaultExt = "rpk";
            dialog.Filter = DialogFilter;
            if (string.IsNullOrEmpty(Settings.Default.AccessKeysDirectory))
                Settings.Default.AccessKeysDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            // ask user
            dialog.InitialDirectory = Settings.Default.AccessKeysDirectory;
            if (dialog.ShowDialog(currentWindow) != true)
                return;

            // handle result
            Settings.Default.AccessKeysDirectory = Path.GetDirectoryName(dialog.FileName);
            Settings.Default.Save();
            IRfidAccess newAccess;
            try
            {
                newAccess = XmlHelper.ReadXmlFile<IRfidAccess>(dialog.FileName, accessModel != null ? accessModel.GetType() : factory.CreateRfidAccess().GetType());
            }
            catch (Exception)
            {
                return;
            }

            // set loaded access values
            KeyA = newAccess.KeyA;
            KeyB = newAccess.KeyB;
            AccessBits = newAccess.AccessBits;
            SelectedKey = newAccess.SelectedKey;
            RaisePropertyChanged(nameof(AccessChanged));
            RaisePropertyChanged(nameof(AccessChangedCardReady));
        }

        protected void UpdateCard(ProgrammerCardChangedEventArgs args)
        {
            cardModel = args.NewCard;
            if (cardModel != null)
            {
                Id = cardModel.Id;
                Content = cardModel.Content != null ? Encoding.ASCII.GetString(cardModel.Content) : null;
            }
            else
            {
                Id = null;
                Content = null;
            }
            RaisePropertyChanged(nameof(CardReady));
            RaisePropertyChanged(nameof(AccessChangedCardReady));
        }

        protected void UpdateAccess(ProgrammerAccessChangedEventArgs args)
        {
            accessModel = args.NewAccess;
            if (accessModel != null)
            {
                KeyA = accessModel.KeyA;
                KeyB = accessModel.KeyB;
                AccessBits = accessModel.AccessBits;
                SelectedKey = accessModel.SelectedKey;
            }
            else
            {
                KeyA = null;
                KeyB = null;
                AccessBits = null;
                SelectedKey = SelectedKey.A;
            }
            RaisePropertyChanged(nameof(AccessChanged));
            RaisePropertyChanged(nameof(AccessChangedCardReady));
        }

        protected byte[] id;

        public byte[] Id
        {
            get { return id; }
            private set { SetProperty(ref id, value); }
        }

        protected string content;

        public string Content
        {
            get { return content; }
            set { SetProperty(ref content, value); }
        }

        protected byte[] keyA;

        public byte[] KeyA
        {
            get { return keyA; }
            set
            {
                SetProperty(ref keyA, value);
                RaisePropertyChanged(nameof(AccessChanged));
                RaisePropertyChanged(nameof(AccessChangedCardReady));
            }
        }

        protected byte[] keyB;

        public byte[] KeyB
        {
            get { return keyB; }
            set
            {
                SetProperty(ref keyB, value);
                RaisePropertyChanged(nameof(AccessChanged));
                RaisePropertyChanged(nameof(AccessChangedCardReady));
            }
        }

        protected byte[] accessBits;

        public byte[] AccessBits
        {
            get { return accessBits; }
            set
            {
                SetProperty(ref accessBits, value);
                RaisePropertyChanged(nameof(AccessChanged));
                RaisePropertyChanged(nameof(AccessChangedCardReady));
            }
        }

        protected SelectedKey selectedKey;

        public SelectedKey SelectedKey
        {
            get { return selectedKey; }
            set
            {
                SetProperty(ref selectedKey, value);
                RaisePropertyChanged(nameof(AccessChanged));
                RaisePropertyChanged(nameof(AccessChangedCardReady));
            }
        }

        public string ConsoleOutput => programmer.Output;

        protected string consoleInput = "";

        public string ConsoleInput
        {
            get { return consoleInput; }
            set { SetProperty(ref consoleInput, value); }
        }

        public ProgrammerState State => programmer.CurrentState;

        public int MaxContentLength => programmer.MaxBytes;

        public bool CardReady
        {
            get
            {
                return !programmer.HasPendingWork && cardModel != null && (State == ProgrammerState.Connected ||
                    State == ProgrammerState.OperationFailed ||
                    State == ProgrammerState.OperationSuccess);
            }
        }

        public bool AccessChanged
        {
            get
            {
                return ConnectedAndReady && KeyA != null && KeyA.Length == 6 && KeyB != null && KeyB.Length == 6 && AccessBits != null && AccessBits.Length == 4
                    && (!KeyA.SequenceEqual(accessModel.KeyA) || !KeyB.SequenceEqual(accessModel.KeyB) || !AccessBits.SequenceEqual(accessModel.AccessBits) || SelectedKey != accessModel.SelectedKey);
            }
        }

        public bool AccessChangedCardReady
        {
            get
            {
                return CardReady && KeyA != null && KeyA.Length == 6 && KeyB != null && KeyB.Length == 6 && AccessBits != null && AccessBits.Length == 4
                    && (!KeyA.SequenceEqual(accessModel.KeyA) || !KeyB.SequenceEqual(accessModel.KeyB) || !AccessBits.SequenceEqual(accessModel.AccessBits) || SelectedKey != accessModel.SelectedKey);
            }
        }

        public bool ConnectedAndReady
        {
            get
            {
                return Connected && !programmer.HasPendingWork;
            }
        }

        public bool Connected
        {
            get
            {
                return State != ProgrammerState.NotConnected && State != ProgrammerState.Connecting && State != ProgrammerState.Unknown;
            }
        }
    }
}
