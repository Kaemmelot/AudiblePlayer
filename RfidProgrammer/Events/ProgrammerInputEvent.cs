using Prism.Events;

namespace RfidProgrammer.Events
{
    public struct ProgrammerInput
    {
        public ProgrammerInput(string text, bool isUserCreated)
        {
            Text = text;
            IsUserCreated = isUserCreated;
        }

        public string Text { get; }
        public bool IsUserCreated { get; }
    }

    public class ProgrammerInputEvent : PubSubEvent<ProgrammerInput>
    {
    }
}
