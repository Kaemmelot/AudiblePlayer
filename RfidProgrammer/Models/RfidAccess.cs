using System;
using System.Runtime.Serialization;

namespace RfidProgrammer.Models
{
    [DataContract(Namespace = "https://rfid.kaemmelot.de/")]
    public class RfidAccess : IRfidAccess, IExtensibleDataObject
    {
        public RfidAccess(byte[] keyA, byte[] keyB, byte[] accessBits, SelectedKey selectedKey)
        {
            if (keyA == null || keyA.Length != 6)
                throw new ArgumentOutOfRangeException(nameof(keyA));
            if (keyB == null || keyB.Length != 6)
                throw new ArgumentOutOfRangeException(nameof(keyB));
            if (accessBits == null || accessBits.Length != 4)
                throw new ArgumentOutOfRangeException(nameof(accessBits));

            KeyA = keyA;
            KeyB = keyB;
            AccessBits = accessBits;
            SelectedKey = selectedKey;
        }

        [DataMember(IsRequired = true)]
        public byte[] KeyA { get; private set; }

        [DataMember(IsRequired = true)]
        public byte[] KeyB { get; private set; }

        [DataMember(IsRequired = true)]
        public byte[] AccessBits { get; private set; }

        [DataMember(IsRequired = true)]
        public SelectedKey SelectedKey { get; private set; }

        public ExtensionDataObject ExtensionData { get; set; }
    }
}
