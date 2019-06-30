#include "cmd.h"

bool cmdRcvd = false;
bool cmdAcked = false;
byte cmd[MAX_CMD_LEN + 1];
byte cmdLen = 0;
bool byteMode = true;

byte nextByte[2];
byte nbLen = 0;

const char *allowedCmds = RECEIVE_CMDS;

void invalidCommand(const bool skip)
{
    Serial.println(INVALID_COMMAND);
    // ignore everything else
    while (skip && Serial.available())
        Serial.read();
    resetCommand();
}

void resetCommand()
{
    cmdRcvd = false;
    cmdAcked = false;
    cmdLen = 0;
    nbLen = 0;
}

/*
  Commands are send to this program, then send back and must get acknowledged
  to prevent errors. Therefore a command might be received but not acked yet.
 */
void readSerial()
{
    while (!cmdAcked && Serial.available())
    {
        if (cmdRcvd && Serial.available() < 3)
            break; // wait until 3 bytes can be read for ACK/NACK

        const byte inByte = Serial.read();

        // check for (n)ack if command already received
        if (cmdRcvd)
        {
            const byte lb1Byte = Serial.read();
            const byte lb2Byte = Serial.read();
            if (lb1Byte != '\r' || lb2Byte != '\n' || (inByte != ACK && inByte != NACK))
            {
                resetCommand();
                invalidCommand(true);
            }
            else if (inByte == ACK)
                cmdAcked = true;
            else if (inByte == NACK)
                resetCommand();
            continue;
        }

        if (inByte != '\n' || cmdLen == 0 || cmd[cmdLen - 1] != '\r')
        {
            if (cmdLen >= MAX_CMD_LEN + 1)
            {
                Serial.println(F("#Command too long"));
                invalidCommand(true);
                break;
            }
            else if (inByte != '\r' && cmdLen > 0 && !byteMode && !isHexChar(inByte))
            {
                Serial.println(F("#Only 2 digit hex chars are allowed in text mode"));
                invalidCommand(true);
                break;
            }
            else if (inByte != '\r' && cmdLen > 0 && !byteMode)
            { // first char needs no text mode
                // text mode
                nextByte[nbLen++] = inByte; // record char
                if (nbLen >= 2)
                {
                    // convert and add to cmd
                    cmd[cmdLen++] = hexByteToByte(nextByte);
                    nbLen = 0;
                }
            }
            else
                cmd[cmdLen++] = inByte; // record command
        }
        else if (cmdLen > 1 && cmd[cmdLen - 1] == '\r')
        {
            // cmd finished, send back and wait for error check
            if (contains(allowedCmds, cmd[0]))
            {
                if (cmd[0] == TOGGLE_BYTE_MODE)
                {
                    byteMode = !byteMode;
                    cmdLen = 0;
                    Serial.print(F("#Toggled byte/text mode. Now using: "));
                    Serial.print(byteMode ? F("byte") : F("text"));
                    Serial.println(F(" mode"));
                    continue;
                }
                cmdLen--; // ignore \r at the end
                cmdRcvd = true;
                Serial.print(ERROR_CHECK);
                if (!byteMode)
                {
                    Serial.print((char)cmd[0]);
                    dumpByteArray(cmd + 1, cmdLen - 1);
                    Serial.println();
                    Serial.print(F("#Equals: "));
                    Serial.write(cmd, cmdLen);
                    Serial.println();
                    Serial.print(F("#Please send "));
                    Serial.print(ACK);
                    Serial.print(F(" for ACK or "));
                    Serial.print(NACK);
                    Serial.println(F(" for NACK"));
                }
                else
                {
                    Serial.write(cmd, cmdLen);
                    Serial.println();
                }
            }
            else
                invalidCommand(true);
        }
        // else ignore newline without command
    }
}
