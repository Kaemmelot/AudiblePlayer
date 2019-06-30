using System;

namespace RfidProgrammer
{
    public static class ByteArrayHelper
    {
        public static string ByteToHexString(byte[] bytes)
        {
            // https://stackoverflow.com/a/14333437
            char[] c = new char[bytes.Length * 2];
            int b;
            for (int i = 0; i < bytes.Length; i++)
            {
                b = bytes[i] >> 4;
                c[i * 2] = (char)(55 + b + (((b - 10) >> 31) & -7));
                b = bytes[i] & 0xF;
                c[i * 2 + 1] = (char)(55 + b + (((b - 10) >> 31) & -7));
            }
            return new string(c);
        }

        public static string ByteToSpacedHexString(byte[] bytes)
        {
            // https://stackoverflow.com/a/14333437
            char[] c = new char[Math.Max(bytes.Length * 3 - 1, 0)];
            int b;
            int i;
            for (i = 0; i < bytes.Length - 1; i++)
            {
                b = bytes[i] >> 4;
                c[i * 3] = (char)(55 + b + (((b - 10) >> 31) & -7));
                b = bytes[i] & 0xF;
                c[i * 3 + 1] = (char)(55 + b + (((b - 10) >> 31) & -7));
                c[i * 3 + 2] = ' ';
            }
            if (i > 0)
            {
                // last element without space
                b = bytes[i] >> 4;
                c[i * 3] = (char)(55 + b + (((b - 10) >> 31) & -7));
                b = bytes[i] & 0xF;
                c[i * 3 + 1] = (char)(55 + b + (((b - 10) >> 31) & -7));
            }
            return new string(c);
        }

        public static byte[] HexStringToBytes(string hexString)
        {
            hexString = hexString.Replace(" ", "");
            // https://stackoverflow.com/a/14335533
            if ((hexString.Length & 1) != 0)
                throw new ArgumentException("Input must have even number of characters");

            byte[] ret = new byte[hexString.Length / 2];
            for (int i = 0; i < ret.Length; i++)
            {
                int high = hexString[i * 2];
                int low = hexString[i * 2 + 1];
                high = (high & 0xf) + ((high & 0x40) >> 6) * 9;
                low = (low & 0xf) + ((low & 0x40) >> 6) * 9;

                ret[i] = (byte)((high << 4) | low);
            }

            return ret;
        }
    }
}
