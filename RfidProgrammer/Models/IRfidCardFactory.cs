namespace RfidProgrammer.Models
{
    public interface IRfidCardFactory
    {
        IRfidCard CreateRfidCard(byte[] id);

        IRfidCard CreateRfidCard(byte[] id, byte[] content);

        IRfidAccess CreateRfidAccess();

        IRfidAccess CreateRfidAccess(byte[] keyA, byte[] keyB, byte[] accessBits, SelectedKey selectedKey);
    }
}
