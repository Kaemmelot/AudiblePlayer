#ifndef _HELPER_H
#define _HELPER_H

#include "writer_setup.h"

/**
 * Helper routine to dump a byte array as hex values to Serial.
 */
inline void dumpByteArray(const byte *arr, const byte size)
{
    for (byte i = 0; i < size; i++)
    {
        Serial.print(arr[i] < 0x10 ? "0" : "");
        Serial.print(arr[i], HEX);
    }
}

inline bool isHexChar(const byte ch)
{
    return (ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F');
}

byte hexByteToByte(const byte *hex);
byte index_of(const char *str, char search);
bool contains(const char *str, char search);

#endif
