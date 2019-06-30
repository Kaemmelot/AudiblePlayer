using System;
using System.IO.Ports;
using System.Text;

namespace RfidProgrammer.ProgrammerService
{
    public class SerialConnection : IDisposable
    {
        private SerialPort serial;

        public bool IsOpen => serial.IsOpen;
        public string Port => serial.PortName;

        private byte[] buffer;
        private int bufPos = 0;

        private byte[] newLineArr;

        public SerialConnection(string port, int baudRate, int dataBits, Parity parity, StopBits stopBits, bool RtsAndDtrEnabled, int bufferSize = 512)
        {
            serial = new SerialPort(port, baudRate, parity, dataBits, stopBits);
            serial.DtrEnable = RtsAndDtrEnabled;
            serial.RtsEnable = RtsAndDtrEnabled;
            serial.Open();
            buffer = new byte[bufferSize];
            newLineArr = Encoding.ASCII.GetBytes("\r\n"); // we need to use \r\n since otherwise it would cause trouble with the delivered content; Alternative: Implement length prefix
        }

        public bool TryReadLine(out byte[] line, int ignoreNewlineBeforeByte = 0)
        {
            if (!serial.IsOpen)
                throw new InvalidOperationException("Connection was closed already");

            line = null;
            if (bufPos == 0 && serial.BytesToRead == 0)
                return false; // there is nothing to read

            // read new content
            var oldPos = bufPos;
            bufPos += serial.Read(buffer, bufPos, Math.Min(buffer.Length - bufPos, serial.BytesToRead));

            // look for line break
            var lineBreakPos = -1;
            for (var i = ignoreNewlineBeforeByte; i < bufPos - 1; i++)
            {
                if (buffer[i] == newLineArr[0] && buffer[i + 1] == newLineArr[1])
                {
                    lineBreakPos = i;
                    break;
                }
            }

            if (lineBreakPos != -1)
            {
                // move content till linebreak to new array
                line = new byte[lineBreakPos];
                Array.Copy(buffer, line, lineBreakPos);
                // if linebreak wasn't last two bytes: move others to front
                if (bufPos > lineBreakPos + 2)
                    Array.Copy(buffer, lineBreakPos + 2, buffer, 0, bufPos - lineBreakPos - 2);
                bufPos -= lineBreakPos + 2;
                return true;
            }
            else if (bufPos == buffer.Length)
            {
                // Buffer full: return as line
                line = buffer;
                buffer = new byte[buffer.Length];
                bufPos = 0;
                return true;
            }
            return false;
        }

        public bool TryPeekBytes(out byte[] peek, int numOfBytes)
        {
            if (!serial.IsOpen)
                throw new InvalidOperationException("Connection was closed already");

            peek = null;
            if (bufPos == 0 && serial.BytesToRead == 0)
                return false; // there is nothing to read

            if (bufPos != buffer.Length)
                bufPos += serial.Read(buffer, bufPos, Math.Min(buffer.Length - bufPos, serial.BytesToRead));

            if (bufPos > numOfBytes)
            {
                // copy content to new array
                peek = new byte[numOfBytes];
                Array.Copy(buffer, peek, numOfBytes);
                return true;
            }
            return false;
        }

        public void WriteBytes(byte[] bytes, int offset = 0, int count = 0)
        {
            if (!serial.IsOpen)
                throw new InvalidOperationException("Connection was closed already");

            serial.Write(bytes, offset, count > 0 ? count : bytes.Length - offset);
        }

        public void WriteLine()
        {
            if (!serial.IsOpen)
                throw new InvalidOperationException("Connection was closed already");

            serial.Write(newLineArr, 0, 2);
        }

        public void Dispose()
        {
            if (serial.IsOpen)
                serial.Close();
        }
    }
}
