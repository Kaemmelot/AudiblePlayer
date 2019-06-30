namespace RfidProgrammer.Models
{
    public interface IRfidCard
    {
        byte[] Id { get; }
        byte[] Content { get; }
    }
}
