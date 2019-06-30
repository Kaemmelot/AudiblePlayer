namespace RfidProgrammer.Models
{
    public interface IRfidAccess
    {
        byte[] KeyA { get; }
        byte[] KeyB { get; }
        byte[] AccessBits { get; }
        SelectedKey SelectedKey { get; }
    }
}
