#include <avr/wdt.h>

void wdt_init(void) __attribute__((naked)) __attribute__((section(".init3")));

void wdt_init(void)
{
    // disable watchdog after reset
    MCUSR = 0;
    wdt_disable();
}
