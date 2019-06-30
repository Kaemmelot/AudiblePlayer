
#include "helper.h"

byte hexByteToByte(const byte *hex)
{
    byte res = 0;
    for (byte i = 0; i < 2; i++)
    {
        res = res << 4;
        if (hex[i] >= '0' && hex[i] <= '9')
            res += hex[i] - '0';
        else if (hex[i] >= 'a' && hex[i] <= 'f')
            res += hex[i] - 'a' + 10;
        else if (hex[i] >= 'A' && hex[i] <= 'F')
            res += hex[i] - 'A' + 10;
    }
    return res;
}

// https://stackoverflow.com/a/20707065
byte index_of(const char *str, char search)
{
    const char *moved_string = strchr(str, search);
    /* If not null, return the difference. */
    if (moved_string)
    {
        return moved_string - str;
    }
    /* Character not found. */
    return -1;
}

bool contains(const char *str, char search)
{
    const char *str2 = strchr(str, search);
    return str2 != nullptr;
}
