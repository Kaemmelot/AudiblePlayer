#ifndef _RFID_H
#define _RFID_H

#include <SPI.h>
#include <MFRC522.h>

#include "writer_setup.h"

/*
 * Typical pin layout used:
 * -----------------------------------------------------------------------------------------
 *             MFRC522      Arduino       Arduino   Arduino    Arduino          Arduino
 *             Reader/PCD   Uno/101       Mega      Nano v3    Leonardo/Micro   Pro Micro
 * Signal      Pin          Pin           Pin       Pin        Pin              Pin
 * -----------------------------------------------------------------------------------------
 * RST/Reset   RST          9             5         D9         RESET/ICSP-5     RST
 * SPI SS      SDA(SS)      10            53        D10        10               10
 * SPI MOSI    MOSI         11 / ICSP-4   51        D11        ICSP-4           16
 * SPI MISO    MISO         12 / ICSP-1   50        D12        ICSP-1           14
 * SPI SCK     SCK          13 / ICSP-3   52        D13        ICSP-3           15
 */

#define RST_PIN 9
#define SS_PIN 10

enum SelectedKey
{
    KEYA,
    KEYB
};

#define KEYA_CHAR 'A'
#define KEYB_CHAR 'B'

extern byte rfidBuf[18];
extern const byte rfidBufSize;

void rfidSetup();
bool isCardStillPresent();
bool isNewCardAvailable();
bool validCardConnected();
bool reconnectCard();
void readRfid(byte sector, byte block, byte blockCnt);
void writeRfid(byte sector, byte block, byte *content, byte contentLen);
void setCurrentTrailers(byte *trailer, byte trailerLen);
void changeTrailers(byte *trailer, byte trailerLen);
void checkTrailers();
void selfTest();
void sendCardToSleep();

#endif
