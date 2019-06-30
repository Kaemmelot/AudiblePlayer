/**
 * This writer is based on the MFRC522 library examples.
 */

#include "writer_setup.h"
#include "softReset.h"
#include "rfid.h"
#include "cmd.h"
#include "helper.h"

void setup()
{
    Serial.begin(SERIAL_BAUDRATE, SERIAL_MODE); // Initialize serial communications with the PC
    while (!Serial)
        ; // Do nothing if no serial port is opened (added for Arduinos based on ATMEGA32U4)
    Serial.print(F("\n"));
    Serial.print(F("\n"));
    rfidSetup();
    Serial.print(F("Initialization complete - "));
    Serial.println(F(VERSION));
}

/*
  SerialEvent occurs whenever a new data comes in the hardware serial RX. This
  routine is run between each time loop() runs, so using delay inside loop can
  delay response. Multiple bytes of data may be available.
*/
void serialEvent()
{
    readSerial();
}

void loop()
{
    // reconnect card or find new one and execute command if exists
    if (reconnectCard() && cmdAcked)
    {
        char cmdType = cmd[0];
        switch (cmdType)
        {
        case READ_CARD:
            if (!validCardConnected() || cmdLen != 4)
            {
                invalidCommand(false);
                break;
            }
            readRfid(cmd[1], cmd[2], cmd[3]);
            break;
        case WRITE_CARD:
            if (!validCardConnected() || cmdLen != 19)
            {
                invalidCommand(false);
                break;
            }
            writeRfid(cmd[1], cmd[2], cmd + 3, cmdLen - 3);
            break;
        case SET_TRAILERS:
            setCurrentTrailers(cmd + 1, cmdLen - 1);
            break;
        case CHANGE_TRAILERS:
            if (!validCardConnected())
            {
                invalidCommand(false);
                break;
            }
            changeTrailers(cmd + 1, cmdLen - 1);
            break;
        case CHECK_TRAILERS:
            if (!validCardConnected() || cmdLen != 1)
            {
                invalidCommand(false);
                break;
            }
            checkTrailers();
            break;
        case SELF_TEST:
            if (!validCardConnected() || cmdLen != 1)
            {
                invalidCommand(false);
                break;
            }
            selfTest();
            break;
        case RESET:
            softReset(); // resets everything with watchdog timer
            break;
        default:
            invalidCommand(false);
            break; // this case should have been handled already
        }
        sendCardToSleep(); // this is needed, so the select doesn't fail once the card is checked again
        resetCommand();
        return;
    }
    else if (cmdAcked)
    {
        if (cmd[0] == RESET)
            softReset();
        else if (cmd[0] == SET_TRAILERS)
        { // allowed even if no card connected
            setCurrentTrailers(cmd + 1, cmdLen - 1);
            resetCommand();
        }
        else
            invalidCommand(false);
        return;
    }

    sendCardToSleep();

    // if not returned: sleep for a moment
    delay(500);
}
