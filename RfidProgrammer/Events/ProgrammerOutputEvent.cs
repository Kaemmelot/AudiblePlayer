using Prism.Events;

namespace RfidProgrammer.Events
{
    public struct ProgrammerOutput
    {
        public ProgrammerOutput(string text, bool isSrviceMessage)
        {
            Text = text;
            IsServiceMessage = isSrviceMessage;
        }

        public string Text { get; }
        public bool IsServiceMessage { get; }
    }

    public class ProgrammerOutputEvent : PubSubEvent<ProgrammerOutput>
    {
    }
}
