import asyncio
import logging
from threading import BoundedSemaphore, Thread

import RPi.GPIO as GPIO

__version_info__ = (1, 0, 0)
__version__ = '.'.join(map(str, __version_info__))
__author__ = "Sebastian Hofmann (Kaemmelot)"
__all__ = ['GpioInputButton', 'GpioInputRotaryEncoder', 'gpioInputStop']


class GpioInput:
    eventThread = None
    eventLoop = None
    gpioMode = None
    usedGpioPins = []


_initialized = False


def init():
    global _initialized
    if _initialized:
        return
    GpioInput.eventLoop = asyncio.new_event_loop()
    GpioInput.eventThread = Thread(
        target=GpioInput.eventLoop.run_forever, name="GpioInput.eventLoop")
    GpioInput.eventThread.start()
    _initialized = True


def gpioInputStop(gpioCleanup=True):
    global _initialized
    if not _initialized:
        return
    logging.debug("Stopping GPIO Input")
    GpioInput.eventLoop.call_soon_threadsafe(GpioInput.eventLoop.stop)
    GpioInput.eventThread.join()  # wait until loop stopped
    # needed for next run_until_complete call
    asyncio.set_event_loop(GpioInput.eventLoop)
    try:
        GpioInput.eventLoop.run_until_complete(asyncio.gather(
            *asyncio.Task.all_tasks(GpioInput.eventLoop)))  # finish all pending tasks
    except asyncio.CancelledError:
        pass
    GpioInput.eventLoop.close()  # now we can safely close the loop
    if gpioCleanup and len(GpioInput.usedGpioPins) > 0:
        GPIO.setmode(GpioInput.gpioMode)  # why is this needed??
        GPIO.cleanup(GpioInput.usedGpioPins)


class GpioInputButton:
    __stateCheckTime = 0.3

    def __init__(self, pin, pressCallback=None,
                 holdCallback=None, singleHoldCall=False, holdTimeOffset=600, holdTimeRepeatTime=400,
                 pull=GPIO.PUD_UP, edge=GPIO.FALLING, bouncetime=400, pinMode=GPIO.BOARD):
        if edge != GPIO.FALLING and edge != GPIO.RISING:
            raise ValueError(
                "Edge can only be falling or rising in this implementation")
        init()
        self._pin = pin
        self._pressCallback = pressCallback
        self._holdCallback = holdCallback
        self._singleHoldCall = singleHoldCall
        self._holdTimeOffset = holdTimeOffset / 1000
        self._holdTimeRepeatTime = holdTimeRepeatTime / 1000
        self.__sema = BoundedSemaphore(1)
        self._expectedState = 0 if edge == GPIO.FALLING else 1
        GPIO.setmode(pinMode)
        GpioInput.gpioMode = pinMode
        GpioInput.usedGpioPins.append(pin)
        GPIO.setup(pin, GPIO.IN, pull_up_down=pull)
        GPIO.add_event_detect(
            pin, edge, callback=self._buttonEventHandler, bouncetime=bouncetime)

    def _buttonEventHandler(self, pin):
        # run the extensive handler in the other thread
        asyncio.run_coroutine_threadsafe(
            self._buttonLoopHandler(), GpioInput.eventLoop)

    async def _buttonLoopHandler(self):
        if not self.__sema.acquire(False):
            return  # other handler already running
        try:
            if self._pressCallback != None:
                self._pressCallback(self._pin)
            isPressed = self.isPressed
            if self._holdCallback != None:  # call hold callback as long as the input stays the same
                # wait some time before first trigger
                holdTimeOff = self._holdTimeOffset
                while holdTimeOff > 0 and isPressed:  # wait in intervals and recheck GPIO state
                    waitTime = self.__stateCheckTime if self.__stateCheckTime <= holdTimeOff else holdTimeOff
                    holdTimeOff -= self.__stateCheckTime
                    await asyncio.sleep(waitTime, loop=GpioInput.eventLoop)
                    isPressed = self.isPressed
                if isPressed:  # call callback
                    self._holdCallback(self._pin)
                    while isPressed and not self._singleHoldCall:
                        # repeat after given time until released
                        holdTimeRep = self._holdTimeRepeatTime
                        isPressed = self.isPressed
                        while holdTimeRep > 0 and isPressed:  # wait in intervals and recheck GPIO state
                            waitTime = self.__stateCheckTime if self.__stateCheckTime <= holdTimeRep else holdTimeRep
                            holdTimeRep -= self.__stateCheckTime
                            await asyncio.sleep(waitTime, loop=GpioInput.eventLoop)
                            isPressed = self.isPressed
                        if isPressed:
                            self._holdCallback(self._pin)
            else:
                while isPressed:  # wait in intervals and recheck GPIO state
                    # this causes other handlers to abort as long as the button is pressed
                    await asyncio.sleep(self.__stateCheckTime, loop=GpioInput.eventLoop)
                    isPressed = self.isPressed
        except:
            pass
        finally:
            self.__sema.release()

    @property
    def isPressed(self):
        return GPIO.input(self._pin) == self._expectedState


class GpioInputRotaryEncoder:
    def __init__(self, pinClk, pinDt, callback, bouncetime=10, pinMode=GPIO.BOARD):
        init()
        self._pinClk = pinClk
        self._pinDt = pinDt
        self._callback = callback
        GPIO.setmode(pinMode)
        GpioInput.gpioMode = pinMode
        GpioInput.usedGpioPins.append(pinClk)
        GpioInput.usedGpioPins.append(pinDt)
        GPIO.setup(pinClk, GPIO.IN)
        GPIO.setup(pinDt, GPIO.IN)
        self._curClk = GPIO.input(pinClk)
        self._curDt = GPIO.input(pinDt)
        self.__sema = BoundedSemaphore(1)
        GPIO.add_event_detect(
            pinClk, GPIO.RISING, callback=self._rotaryEventHandler)
        GPIO.add_event_detect(
            pinDt, GPIO.RISING, callback=self._rotaryEventHandler)

    def _rotaryEventHandler(self, pin):
        if not self.__sema.acquire(False):
            return  # currently running, would cause problems
        try:
            curClk = GPIO.input(self._pinClk)
            curDt = GPIO.input(self._pinDt)
            direction = -1
            if self._curClk == curClk and self._curDt == curDt:
                return
            elif self._curClk != curClk:
                direction = 1
            self._curClk = curClk
            self._curDt = curDt
        except:
            return
        finally:
            self.__sema.release()
        if curClk and curDt:
            GpioInput.eventLoop.call_soon_threadsafe(lambda: self._callback(
                direction))  # run the callback in the other thread
