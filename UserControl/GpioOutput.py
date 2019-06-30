import asyncio
import logging
from threading import Thread

import RPi.GPIO as GPIO

__version_info__ = (1, 0, 0)
__version__ = '.'.join(map(str, __version_info__))
__author__ = "Sebastian Hofmann (Kaemmelot)"
__all__ = ['GpioOutputLed', 'gpioOutputStop']


class GpioOutput:
    taskThread = None
    taskLoop = None
    gpioMode = None
    usedGpioPins = []


_initialized = False


def init():
    global _initialized
    if _initialized:
        return
    GpioOutput.taskLoop = asyncio.new_event_loop()
    GpioOutput.taskThread = Thread(
        target=GpioOutput.taskLoop.run_forever, name="GpioOutput.taskLoop")
    GpioOutput.taskThread.start()
    _initialized = True


def gpioOutputStop(gpioCleanup=True):
    global _initialized
    if not _initialized:
        return
    logging.debug("Stopping GPIO Output")
    GpioOutput.taskLoop.call_soon_threadsafe(GpioOutput.taskLoop.stop)
    GpioOutput.taskThread.join()  # wait until loop stopped
    GpioOutput.taskLoop.close()  # now we can safely close the loop
    if gpioCleanup and len(GpioOutput.usedGpioPins) > 0:
        GPIO.setmode(GpioOutput.gpioMode)  # why is this needed??
        GPIO.cleanup(GpioOutput.usedGpioPins)


class GpioOutputLed:
    def __init__(self, pin, inverted=False, pinMode=GPIO.BOARD):
        init()
        self._pin = pin
        self._inverted = inverted
        self._off = GPIO.HIGH if inverted else GPIO.LOW
        self._on = GPIO.LOW if inverted else GPIO.HIGH
        self._value = self._off  # off by default
        self.__taskEndValue = None
        self._currentTask = None
        GPIO.setmode(pinMode)
        GpioOutput.gpioMode = pinMode
        GpioOutput.usedGpioPins.append(pin)
        GPIO.setup(pin, GPIO.OUT, initial=self._value)

    async def __runTask(self, task):
        await task

    async def __runPattern(self, pattern):
        try:
            if not isinstance(pattern, list):
                logging.warning(
                    "Invalid pattern for GpioOutputLed, ignoring...")
                return
            # start with off
            curVal = self._off
            self._value = curVal
            GPIO.output(self._pin, curVal)
            pPos = 0
            while True:
                # sleep for time given by pattern
                await asyncio.sleep(pattern[pPos], loop=GpioOutput.taskLoop)
                curVal = self._off if curVal == self._on else self._on  # switch on/off
                self._value = curVal
                GPIO.output(self._pin, curVal)
                pPos += 1  # go to next pattern entry
                if pPos >= len(pattern):
                    pPos = 0
        except asyncio.CancelledError:
            pass
        if self.__taskEndValue != None:
            # this is used in case the CancelledError was raised AFTER the final change
            self._value = self.__taskEndValue
            GPIO.output(self._pin, self.__taskEndValue)

    def _cancelCurrentTask(self, newValue=None):
        if self._currentTask == None:
            return
        self.__taskEndValue = newValue
        GpioOutput.taskLoop.call_soon_threadsafe(self._currentTask.cancel)
        self._currentTask = None

    def on(self):
        self._cancelCurrentTask(self._on)
        if self._value == self._on:
            return  # already on
        self._value = self._on
        GPIO.output(self._pin, self._on)

    def off(self):
        self._cancelCurrentTask(self._off)
        if self._value == self._off:
            return  # already off
        self._value = self._off
        GPIO.output(self._pin, self._off)

    def setPattern(self, pattern):
        self._cancelCurrentTask()
        self._currentTask = GpioOutput.taskLoop.create_task(
            self.__runPattern(pattern))  # create task
        asyncio.run_coroutine_threadsafe(self.__runTask(
            self._currentTask), GpioOutput.taskLoop)  # and run it here

    def stopPattern(self):
        self._cancelCurrentTask()

    @property
    def pin(self):
        return self._pin
