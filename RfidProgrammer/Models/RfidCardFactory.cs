using System.ComponentModel.Composition;

namespace RfidProgrammer.Models
{
    [Export(typeof(IRfidCardFactory))]
    public class RfidCardFactory : IRfidCardFactory
    {
        private byte[] defaultKeyA = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
        private byte[] defaultKeyB = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
        private byte[] defaultAccessBits = new byte[] { 0xFF, 0x07, 0x80, 0x69 };

        public IRfidCard CreateRfidCard(byte[] id)
        {
            return CreateRfidCard(id, null);
        }

        public IRfidCard CreateRfidCard(byte[] id, byte[] content)
        {
            return new RfidCard(id, content);
        }

        public IRfidAccess CreateRfidAccess()
        {
            return new RfidAccess(defaultKeyA, defaultKeyB, defaultAccessBits, SelectedKey.A);
        }

        public IRfidAccess CreateRfidAccess(byte[] keyA, byte[] keyB, byte[] accessBits, SelectedKey selectedKey)
        {
            return new RfidAccess(keyA, keyB, accessBits, selectedKey);
        }
    }
}
