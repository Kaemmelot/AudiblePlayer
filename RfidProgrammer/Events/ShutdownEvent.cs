using Prism.Events;

namespace RfidProgrammer.Events
{
    public struct Shutdown
    {
    }

    public class ShutdownEvent : PubSubEvent<Shutdown>
    {
    }
}
