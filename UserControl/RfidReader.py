import logging
from threading import Event, RLock, Thread
from time import sleep

import RPi.GPIO as GPIO
from Card import Card
from pirc522 import RFID

__version_info__ = (1, 0, 0)
__version__ = '.'.join(map(str, __version_info__))
__author__ = "Sebastian Hofmann (Kaemmelot)"
__all__ = ['arrayToHexString', 'RfidReader']


def arrayToHexString(arr):
    res = ""
    for byte in arr:
        res += '{:02X}'.format(byte)
    return res


class RfidReader(object):
    """The RFID reader class. Uses pi-rc522 to read cards in a separate thread and returns them as Card object to a given callback"""

    """
    Usable Sectors and Blocks (r = reserved, d = data, t = trailer: keys and access bits)
    Classic 1K Chip:
     0:  0  1  2  3   r d d t
     1:  4  5  6  7   d d d t
     2:  8  9 10 11   d d d t
     3: 12 13 14 15   d d d t
     4: 16 17 18 19   d d d t
     5: 20 21 22 23   d d d t
     6: 24 25 26 27   d d d t
     7: 28 29 30 31   d d d t
     8: 32 33 34 35   d d d t
     9: 36 37 38 39   d d d t
    10: 40 41 42 43   d d d t
    11: 44 45 46 47   d d d t
    12: 48 49 50 51   d d d t
    13: 52 53 54 55   d d d t
    14: 56 57 58 59   d d d t
    15: 60 61 62 63   d d d t
    2 * 1 + 3 * 15 = 47 usable blocks
    47 * 16 = 752 usable bytes
    """

    rfidReadEndChar = chr(0x04)

    def __init__(self, callback=None, pinRst=15, pinCe=24, pinIrq=13, pinMode=GPIO.BOARD,
                 key=[0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF], keyName='A', bus=0, device=0, speed=1000000):
        self._callback = callback
        self._currentCard = None
        logging.debug("Listening for RFID card with pins: rst=%d, ce=%d, irq=%d" % (pinRst, pinCe, pinIrq))
        logging.debug("RFID key %s is %s" % (keyName, arrayToHexString(key)))
        self._rfid = RFID(bus, device, speed, pinRst, pinCe, pinIrq, pinMode)
        self._rfidUtil = self._rfid.util()
        self.__key = key
        self.__keyName = keyName
        #self._rfidUtil.debug = True
        self._currentCardLock = RLock()
        self.__stopThread = Event()
        self.__thread = Thread(target=self._readRfids,
                               name="RfidReader._readRfids")
        self.__thread.start()

    def _readRfids(self):
        try:
            while not self.__stopThread.isSet():
                self.__waitForTag()

                if self.__stopThread.isSet():
                    break

                # only request idle tags
                (error, data) = self._rfid.request(RFID.act_reqidl)
                if error:
                    continue
                logging.debug("RFID connect")
                (error, uid) = self._rfid.anticoll()
                if error:
                    logging.warning("Anticollision failed")
                    continue
                logging.debug("Card %s selected", arrayToHexString(uid))
                # select tag and set authorization key
                error = self._rfidUtil.set_tag(uid)
                if error:
                    logging.warning("Tag selection failed")
                    continue
                self._rfidUtil.auth(
                    self._rfid.auth_a if self.__keyName == 'A' else self._rfid.auth_b, self.__key)

                (error, content) = self.__readCard()

                # stop the crypto module on card
                self._rfid.stop_crypto()
                if not error:
                    # move card in halt state so it won't be detected again
                    self._rfid.halt()

                    logging.debug("Card %s contains:\n%s",
                                  arrayToHexString(uid), repr(content))

                    # set new card
                    self._currentCardLock.acquire()
                    self._currentCard = Card(uid, content)
                    self._currentCardLock.release()
                    self._callback(self._currentCard)

                    # wait until card is removed
                    self.__waitForTagRemoval()
                    if (self.__stopThread.isSet()):
                        break
                    self._currentCardLock.acquire()
                    self._currentCard = None
                    self._currentCardLock.release()
                    self._callback(None)
        except BaseException as e:
            logging.error("RfidReader crashed with error: %s" % str(e))
        except:
            logging.error("RfidReader crashed")

    def __readCard(self):
        """
        Reads the contents of the currently selected and authorized card.
        Returns tuple of (error, content)
        """
        # read text content block for block (skipping reserved and trailer blocks)
        content = ""
        block = 1
        finished = False
        while not finished and not self.__stopThread.isSet():
            # authorize for sector
            if block == 1 or block % 4 == 0:
                error = self._rfidUtil.do_auth(block)
                if error:
                    logging.error("Authorization for block %s (B%s) failed",
                                  self._rfidUtil.sector_string(block), str(block))
                    break
            # read data
            (error, data) = self._rfid.read(block)
            if error:
                logging.error("Could not read block %s (B%s)",
                              self._rfidUtil.sector_string(block), str(block))
                break
            for byte in data:
                # goon until end char is found
                if chr(byte) == self.rfidReadEndChar:
                    finished = True
                    break
                content += chr(byte)
            block += 1
            # skip trailers
            if block % 4 == 3:
                block += 1
            # maximum -> block 63
            if block > 63:
                finished = True
        if not self.__stopThread.isSet():
            self._rfid.stop_crypto()

        return (not finished, content if finished else None)

    def __waitForTag(self):
        """Periodically checks if a tag is in range and returns once a tag was found. Custom version of the rfid library function"""
        self._rfid.init()
        self._rfid.irq.clear()
        # this enables an interrupt for successful read:
        self._rfid.dev_write(0x04, 0x00)
        self._rfid.dev_write(0x02, 0xA0)
        waiting = True
        while waiting and not self.__stopThread.isSet():
            # this requests all idle cards:
            self._rfid.dev_write(0x09, 0x26)
            self._rfid.dev_write(0x01, 0x0C)
            self._rfid.dev_write(0x0D, 0x87)
            waiting = not self._rfid.irq.wait(0.5)
        if not self.__stopThread.isSet():
            self._rfid.irq.clear()
            self._rfid.init()

    def __waitForTagRemoval(self):
        """Periodically checks if tag is still present and returns once it's gone"""
        found = True
        while found and not self.__stopThread.isSet():
            # wait before next check
            if self.__stopThread.wait(0.75):
                return
            # BUG: The following creates a relative high cpu usage. Is there a better way?
            # TEMP_FIX: higher wait time
            # request all tags (including halted!)
            (error, tag) = self._rfid.request(0x52)
            # try to select current card again and send it back to halt
            if error or self.currentCard == None or not self._rfid.select_tag(self._currentCard.uid):
                found = False
            else:
                self._rfid.halt()

    @property
    def currentCard(self):
        """Returns the currently detected card"""
        self._currentCardLock.acquire()
        card = self._currentCard
        self._currentCardLock.release()
        return card

    def stop(self, gpioCleanup=True):
        logging.debug("Stopping RFID Reader")
        self.__stopThread.set()
        # TODO sometimes the complete process terminates here on shutdown instead of moving on. why?
        self.__thread.join()
        if gpioCleanup:
            self._rfid.cleanup()
