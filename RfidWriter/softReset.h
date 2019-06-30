// http://nongnu.org/avr-libc/user-manual/FAQ.html#faq_softreset
#ifndef _SOFT_RESET_H
#define _SOFT_RESET_H

#include <avr/wdt.h>

#define softReset()            \
    do                         \
    {                          \
        noInterrupts();        \
        wdt_enable(WDTO_15MS); \
        for (;;)               \
        {                      \
        }                      \
    } while (0)

#endif
