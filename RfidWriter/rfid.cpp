#include "rfid.h"
#include "cmd.h"

MFRC522 mfrc522(SS_PIN, RST_PIN); // MFRC522 instance.

MFRC522::MIFARE_Key keyA;
MFRC522::MIFARE_Key keyB;
byte accessBits[4];
SelectedKey selectedKey = KEYA;

MFRC522::PICC_Type currentPicc = MFRC522::PICC_TYPE_UNKNOWN;

byte rfidBuf[18];
const byte rfidBufSize = sizeof(rfidBuf);

void rfidSetup()
{
    Serial.println(F("##RFID Initialization##"));
    SPI.begin();        // Init SPI bus
    mfrc522.PCD_Init(); // Init MFRC522 card
    // see http://calc.gmss.ru/Mifare1k/
    keyA.keyByte[0] = 0xFF; // default keys and access bits
    keyA.keyByte[1] = 0xFF;
    keyA.keyByte[2] = 0xFF;
    keyA.keyByte[3] = 0xFF;
    keyA.keyByte[4] = 0xFF;
    keyA.keyByte[5] = 0xFF;
    accessBits[0] = 0xFF;
    accessBits[1] = 0x07;
    accessBits[2] = 0x80;
    accessBits[3] = 0x69; // unused/custom data
    keyB.keyByte[0] = 0xFF;
    keyB.keyByte[1] = 0xFF;
    keyB.keyByte[2] = 0xFF;
    keyB.keyByte[3] = 0xFF;
    keyB.keyByte[4] = 0xFF;
    keyB.keyByte[5] = 0xFF;
    selectedKey = KEYA;
}

bool isCardStillPresent()
{
    // wakeup and check if still selectable
    byte bufSize = rfidBufSize;
    MFRC522::StatusCode status;
    /*status =*/mfrc522.PICC_WakeupA(rfidBuf, &bufSize); // possible timeout
    status = mfrc522.PICC_Select(&(mfrc522.uid));
    return status == MFRC522::STATUS_OK;
}

bool isNewCardAvailable()
{
    if (mfrc522.PICC_IsNewCardPresent() && mfrc522.PICC_ReadCardSerial())
    {
        currentPicc = mfrc522.PICC_GetType(mfrc522.uid.sak);
        // new card selected
        if (!byteMode)
        {
            Serial.println(F("#New card connected:"));
            Serial.print(F("#Type: "));
            Serial.println(mfrc522.PICC_GetTypeName(currentPicc));
        }
        return true;
    }
    return false;
}

bool validCardConnected()
{
    return currentPicc == MFRC522::PICC_TYPE_MIFARE_1K || currentPicc == MFRC522::PICC_TYPE_MIFARE_4K;
}

bool reconnectCard()
{
    if (!validCardConnected())
    {
        // look for valid card
        if (isNewCardAvailable())
        {
            // send uid if supported card
            if (validCardConnected())
            {
                Serial.print(CARD_CHANGED);
                if (byteMode)
                    Serial.write(mfrc522.uid.uidByte, mfrc522.uid.size);
                else
                    dumpByteArray(mfrc522.uid.uidByte, mfrc522.uid.size);
                Serial.println();
                MFRC522::StatusCode status = mfrc522.PCD_Authenticate(selectedKey == KEYA ? MFRC522::PICC_CMD_MF_AUTH_KEY_A : MFRC522::PICC_CMD_MF_AUTH_KEY_B,
                                                                      3, selectedKey == KEYA ? &keyA : &keyB, &(mfrc522.uid));
                if (status != MFRC522::STATUS_OK)
                {
                    if (!byteMode)
                    {
                        Serial.print(F("#auth error: "));
                        Serial.println(mfrc522.GetStatusCodeName(status));
                    }
                    Serial.println(AUTH_FAILED); // hint for user
                    //Serial.println(CARD_CHANGED);
                }
                return true; //status == MFRC522::STATUS_OK;
            }
            return false;
        }
    }
    else if (!isCardStillPresent())
    { // card lost; this also wakes the current card from halt to ready
        currentPicc = MFRC522::PICC_TYPE_UNKNOWN;
        if (!byteMode)
            Serial.println(F("#Card disconnected"));
        Serial.println(CARD_CHANGED); // Send card changed without uid to show that no card is connected
        return false;
    }
}

void readRfid(byte sector, byte block, byte blockCnt)
{
    if (blockCnt == 0 || sector > 15 || block > 3 || // invalid position
        sector * 4 + block + blockCnt - 1 > 63)
    { // invalid end
        invalidCommand(false);
        return;
    }

    MFRC522::StatusCode status;
    const byte sKey = (selectedKey == KEYA ? MFRC522::PICC_CMD_MF_AUTH_KEY_A : MFRC522::PICC_CMD_MF_AUTH_KEY_B);
    MFRC522::MIFARE_Key *key = (selectedKey == KEYA ? &keyA : &keyB);
    byte tBlock = 3;

    for (byte b = 0; b < blockCnt; b++)
    {
        const byte currentBlock = sector * 4 + block + b;
        if (b == 0 || (block + b) % 4 == 0)
        {
            // reached new sector, authentication needed
            status = mfrc522.PCD_Authenticate(sKey, currentBlock, key, &(mfrc522.uid));
            if (status != MFRC522::STATUS_OK)
            {
                Serial.println(AUTH_FAILED);
                Serial.print(F("#auth error for block "));
                Serial.print(currentBlock, DEC);
                Serial.print(F(": "));
                Serial.println(mfrc522.GetStatusCodeName(status));
                Serial.println(NACK);
                return;
            }
        }

        // read block
        byte bufSize = rfidBufSize;
        status = mfrc522.MIFARE_Read(currentBlock, rfidBuf, &bufSize);
        if (status != MFRC522::STATUS_OK)
        {
            Serial.print(F("#read error for block "));
            Serial.print(currentBlock, DEC);
            Serial.print(F(": "));
            Serial.println(mfrc522.GetStatusCodeName(status));
            Serial.println(NACK);
            return;
        }

        // send block
        Serial.print(PARTIAL_RESULT);
        if (!byteMode)
        {
            dumpByteArray(rfidBuf, bufSize - 2);
            Serial.println();
            Serial.print(F("#Equals: "));
        }
        Serial.write(rfidBuf, bufSize - 2);
        Serial.println();
    }
    Serial.println(ACK);
}

void writeRfid(byte sector, byte block, byte *content, byte contentLen)
{
    if (sector == 0 && block == 0 || sector > 15 || block > 2 || contentLen != 16)
    { // invalid position
        invalidCommand(false);
        return;
    }

    MFRC522::StatusCode status;
    const byte sKey = (selectedKey == KEYA ? MFRC522::PICC_CMD_MF_AUTH_KEY_A : MFRC522::PICC_CMD_MF_AUTH_KEY_B);
    MFRC522::MIFARE_Key *key = (selectedKey == KEYA ? &keyA : &keyB);
    const byte currentBlock = sector * 4 + block;

    status = mfrc522.PCD_Authenticate(sKey, currentBlock, key, &(mfrc522.uid));
    if (status != MFRC522::STATUS_OK)
    {
        Serial.println(AUTH_FAILED);
        Serial.print(F("#auth error for block "));
        Serial.print(currentBlock, DEC);
        Serial.print(F(": "));
        Serial.println(mfrc522.GetStatusCodeName(status));
        Serial.println(NACK);
        return;
    }

    // write block
    status = mfrc522.MIFARE_Write(currentBlock, content, contentLen);
    if (status != MFRC522::STATUS_OK)
    {
        Serial.print(F("#write error for block "));
        Serial.print(currentBlock, DEC);
        Serial.print(F(": "));
        Serial.println(mfrc522.GetStatusCodeName(status));
        Serial.println(NACK);
        return;
    }
    Serial.println(ACK);
}

void setCurrentTrailers(byte *trailer, byte trailerLen)
{
    if (trailerLen != 17 || (trailer[16] != KEYA_CHAR && trailer[16] != KEYB_CHAR))
    {
        invalidCommand(false);
        return;
    }
    keyA.keyByte[1] = trailer[1];
    keyA.keyByte[0] = trailer[0];
    keyA.keyByte[2] = trailer[2];
    keyA.keyByte[3] = trailer[3];
    keyA.keyByte[4] = trailer[4];
    keyA.keyByte[5] = trailer[5];
    accessBits[0] = trailer[6];
    accessBits[1] = trailer[7];
    accessBits[2] = trailer[8];
    accessBits[3] = trailer[9];
    keyB.keyByte[0] = trailer[10];
    keyB.keyByte[1] = trailer[11];
    keyB.keyByte[2] = trailer[12];
    keyB.keyByte[3] = trailer[13];
    keyB.keyByte[4] = trailer[14];
    keyB.keyByte[5] = trailer[15];
    selectedKey = (trailer[16] == KEYA_CHAR ? KEYA : KEYB);
    Serial.println(ACK);
}

void changeTrailers(byte *trailer, byte trailerLen)
{
    if (trailerLen != 17 || (trailer[16] != KEYA_CHAR && trailer[16] != KEYB_CHAR))
    {
        invalidCommand(false);
        return;
    }

    MFRC522::StatusCode status;
    const byte sKey = (selectedKey == KEYA ? MFRC522::PICC_CMD_MF_AUTH_KEY_A : MFRC522::PICC_CMD_MF_AUTH_KEY_B);
    MFRC522::MIFARE_Key *key = (selectedKey == KEYA ? &keyA : &keyB);

    for (byte tBlock = 3; tBlock <= 63; tBlock = tBlock + 4)
    {
        // authenticate
        status = mfrc522.PCD_Authenticate(sKey, tBlock, key, &(mfrc522.uid));
        if (status != MFRC522::STATUS_OK)
        {
            Serial.println(AUTH_FAILED);
            Serial.print(F("#auth error for block "));
            Serial.print(tBlock, DEC);
            Serial.print(F(": "));
            Serial.println(mfrc522.GetStatusCodeName(status));
            Serial.println(NACK);
            return;
        }

        // write trailer
        status = mfrc522.MIFARE_Write(tBlock, trailer, trailerLen - 1);
        if (status != MFRC522::STATUS_OK)
        {
            Serial.print(F("#write error for block "));
            Serial.print(tBlock, DEC);
            Serial.print(F(": "));
            Serial.println(mfrc522.GetStatusCodeName(status));
            Serial.println(NACK);
            return;
        }
        Serial.print(PARTIAL_RESULT);
        Serial.println(ACK);
    }

    // now change the trailers we use (ack will be sent there:)
    setCurrentTrailers(trailer, trailerLen);
}

void checkTrailers()
{
    MFRC522::StatusCode status;
    const byte sKey = (selectedKey == KEYA ? MFRC522::PICC_CMD_MF_AUTH_KEY_A : MFRC522::PICC_CMD_MF_AUTH_KEY_B);
    MFRC522::MIFARE_Key *key = (selectedKey == KEYA ? &keyA : &keyB);
    bool success = true;

    for (byte tBlock = 3; tBlock <= 63; tBlock = tBlock + 4)
    {
        // authenticate with keyA
        status = mfrc522.PCD_Authenticate(MFRC522::PICC_CMD_MF_AUTH_KEY_A, tBlock, &keyA, &(mfrc522.uid));
        if (status != MFRC522::STATUS_OK)
        {
            Serial.println(AUTH_FAILED);
            Serial.print(F("#auth error for block "));
            Serial.print(tBlock, DEC);
            Serial.print(F(": "));
            Serial.println(mfrc522.GetStatusCodeName(status));
            Serial.print(PARTIAL_RESULT);
            Serial.println(NACK);
            success = false;
            continue;
        }

        // read trailer
        byte bufSize = rfidBufSize;
        status = mfrc522.MIFARE_Read(tBlock, rfidBuf, &bufSize);
        if (status != MFRC522::STATUS_OK)
        {
            Serial.print(F("#read error for block "));
            Serial.print(tBlock, DEC);
            Serial.print(F(": "));
            Serial.println(mfrc522.GetStatusCodeName(status));
            Serial.print(PARTIAL_RESULT);
            Serial.println(NACK);
            success = false;
            continue;
        }

        // check access bits
        bool accessProblem = bufSize < 9;
        Serial.print(F("#access: "));
        for (byte i = 6; i <= 9; i++)
        {
            if (rfidBuf[i] < 0x10)
                Serial.print(F("0"));
            Serial.print(rfidBuf[i], HEX);
            if (accessBits[i - 6] != rfidBuf[i])
                accessProblem = true;
        }
        Serial.println();
        if (accessProblem)
        {
            Serial.print(PARTIAL_RESULT);
            Serial.println(NACK);
            success = false;
            continue;
        }

        // TODO only if possible based on access bits:
        // authenticate with keyB
        /*status = mfrc522.PCD_Authenticate(MFRC522::PICC_CMD_MF_AUTH_KEY_B, tBlock, &keyB, &(mfrc522.uid));
        if (status != MFRC522::STATUS_OK) {
            Serial.println(AUTH_FAILED);
            Serial.print(F("#auth error for block "));
            Serial.print(tBlock, DEC);
            Serial.print(F(": "));
            Serial.println(mfrc522.GetStatusCodeName(status));
            Serial.print(PARTIAL_RESULT);
            Serial.println(NACK);
            success = false;
            continue;
        }*/

        Serial.print(PARTIAL_RESULT);
        Serial.println(ACK);
    }
    if (success)
        Serial.println(ACK);
    else
        Serial.println(NACK);
}

void selfTest()
{
    const byte version = mfrc522.PCD_ReadRegister(MFRC522::VersionReg);
    // is selftest supported?
    if (version != 0x88 && version != 0x90 && version != 0x91 && version != 0x92)
    {
        Serial.print(F("#Selftest is not supported for this card (version: "));
        if (version < 0x10)
            Serial.print(F("0"));
        Serial.print(version, HEX);
        Serial.println(F(")"));
        invalidCommand(false);
        return;
    }
    if (mfrc522.PCD_PerformSelfTest())
        Serial.println(ACK);
    else
        Serial.println(NACK);
}

void sendCardToSleep()
{
    // stop crypto module and halt the card
    mfrc522.PCD_StopCrypto1();
    mfrc522.PICC_HaltA();
}
