using System;

namespace RfidProgrammer.Models
{
    public class RfidCard : IRfidCard
    {
        public RfidCard(byte[] id, byte[] content)
        {
            if (id == null || id.Length < 4)
                throw new ArgumentOutOfRangeException(nameof(id));

            Id = id;
            Content = content;
        }

        public byte[] Id { get; }

        public byte[] Content { get; }
    }
}
