using Prism.Events;
using RfidProgrammer.Events;
using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace RfidProgrammer.MainView
{
    [Export(typeof(MainWindow))]
    public partial class MainWindow : Window
    {
        protected TextBox contentTextBox;
        protected Label lengthLabel;
        protected FlowDocument outputFlowDocument;
        protected Paragraph outputParagraph = null;
        protected Brush ProgrammerColorBrush = new SolidColorBrush(Colors.Black);
        protected Brush ServiceMessageColorBrush = new SolidColorBrush(Colors.Crimson);
        protected Brush ServiceColorBrush = new SolidColorBrush(Colors.Blue);
        protected Brush UserColorBrush = new SolidColorBrush(Colors.DarkOrange);

        protected Dispatcher dispatcher;

        [ImportingConstructor]
        public MainWindow(IEventAggregator eventAggr)
        {
            if (eventAggr == null)
                throw new ArgumentNullException(nameof(eventAggr));
            InitializeComponent();
            contentTextBox = (TextBox)FindName("ContentTextBox");
            lengthLabel = (Label)FindName("LengthLabel");
            outputFlowDocument = (FlowDocument)FindName("OutputDocument");
            eventAggr.GetEvent<ProgrammerOutputEvent>().Subscribe(ProgrammerOutputEvent);
            eventAggr.GetEvent<ProgrammerInputEvent>().Subscribe(ProgrammerInputEvent);
            lengthLabel.Content = string.Format("{0}/{1}", contentTextBox.Text.Length, contentTextBox.MaxLength > 0 ? contentTextBox.MaxLength : '?'); // initialize
            dispatcher = Application.Current.Dispatcher;
            Closing += (sender, e) => eventAggr.GetEvent<ShutdownEvent>().Publish(new Shutdown());
        }

        private void InitializeDocument()
        {
            outputParagraph = new Paragraph();
            outputFlowDocument.Blocks.Clear();
            outputFlowDocument.Blocks.Add(outputParagraph);
        }

        private Action<ProgrammerOutput> programmerOutputAction = null;

        private void ProgrammerOutputEvent(ProgrammerOutput programmerOutput)
        {
            if (programmerOutputAction == null)
                programmerOutputAction = new Action<ProgrammerOutput>(programmerOut =>
                {
                    if (outputParagraph == null)
                        InitializeDocument();

                    var newRun = new Run(programmerOut.Text);
                    newRun.Foreground = programmerOut.IsServiceMessage ? ServiceMessageColorBrush : ProgrammerColorBrush;
                    outputParagraph.Inlines.Add(newRun);
                    outputParagraph.Inlines.Add(new LineBreak());
                });

            dispatcher.BeginInvoke(programmerOutputAction, new object[] { programmerOutput });
        }

        private Action<ProgrammerInput> programmerInputAction = null;

        private void ProgrammerInputEvent(ProgrammerInput programmerInput)
        {
            if (programmerInputAction == null)
                programmerInputAction = new Action<ProgrammerInput>(programmerIn =>
                {
                    if (outputParagraph == null)
                        InitializeDocument();

                    var newRun = new Run(programmerIn.Text);
                    newRun.Foreground = programmerIn.IsUserCreated ? UserColorBrush : ServiceColorBrush;
                    outputParagraph.Inlines.Add(newRun);
                    outputParagraph.Inlines.Add(new LineBreak());
                });

            dispatcher.BeginInvoke(programmerInputAction, new object[] { programmerInput });
        }

        private void ContentTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // check for unallowed lineendings and show remaining length
            var content = contentTextBox.Text.Replace("\r", "");
            if (content != contentTextBox.Text)
                contentTextBox.Text = content;
            lengthLabel.Content = string.Format("{0}/{1}", contentTextBox.Text.Length, contentTextBox.MaxLength > 0 ? contentTextBox.MaxLength : '?');
            e.Handled = true; // TODO what does this do?
        }

        private void ContentTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            // non windows line endings
            if (e.Key == Key.Return && contentTextBox.Text.Length < contentTextBox.MaxLength)
            {
                var pos = contentTextBox.CaretIndex;
                contentTextBox.Text = contentTextBox.Text.Insert(pos, "\n");
                contentTextBox.CaretIndex = pos + 1;
                e.Handled = true;
            }
        }

        private string reformatHex(string str, int maxLength)
        {
            var chars = str.Where(ch => !char.IsWhiteSpace(ch)).ToList();
            char[] result = new char[Math.Min(maxLength, (int)(chars.Count * 1.5))]; // 1.5 => every second character one space
            for (int i = 0, j = 0; i < result.Length; i++)
                result[i] = i % 3 == 2 ? ' ' : chars[j++];
            return new string(result);
        }

        private void FormatHex_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = (TextBox)sender;
            if (textBox.Text == null)
                return;

            // needs reformatting
            // TODO caret could jump unexpected
            var sizeBefore = textBox.Text.Length;
            var caretBefore = textBox.CaretIndex;
            textBox.Text = reformatHex(textBox.Text, textBox.MaxLength);
            textBox.CaretIndex = Math.Max(0, Math.Min(textBox.Text.Length, caretBefore + textBox.Text.Length - sizeBefore)); // move caret to new position
            e.Handled = true;
        }

        private void FormattedHex_KeyDown(object sender, KeyEventArgs e)
        {
            var textBox = (TextBox)sender;
            if (textBox.Text == null)
                return;

            // if the user just removed a whitespace, remove the next character instead
            if (e.Key == Key.Delete && textBox.CaretIndex % 3 == 2 && textBox.CaretIndex < textBox.Text.Length)
            {
                var caretBefore = textBox.CaretIndex;
                textBox.Text = textBox.Text.Remove(textBox.CaretIndex + 1, 1); // remove one more on the right side
                textBox.CaretIndex = caretBefore + 1;
                e.Handled = true;
            }
            else if (e.Key == Key.Back && textBox.CaretIndex % 3 == 0)
            {
                var caretBefore = textBox.CaretIndex;
                textBox.Text = textBox.Text.Remove(textBox.CaretIndex - 2, 1); // remove one more on the left side
                textBox.CaretIndex = caretBefore - 2;
                e.Handled = true;
            }
        }

        public Action ComAction => new Action(() => new ConnectionWindow() { Owner = this }.ShowDialog());
    }
}
