import logging
import sys
from os import WIFEXITED, getpgid, killpg, setsid, system
from signal import SIGTERM
from subprocess import DEVNULL, Popen
from threading import Lock

__version_info__ = (1, 0, 0)
__version__ = '.'.join(map(str, __version_info__))
__author__ = "Sebastian Hofmann (Kaemmelot)"
__all__ = ['AbstractSoundSynthesizer', 'DefaultSoundSynthesizer']


class AbstractSoundSynthesizer:
    soundLock = Lock()
    soundPopen = None

    def playSound(self, filename, timeOffset=0.5, synchronous=False):
        raise NotImplementedError()

    def playTextOnce(self, text, language, timeOffset=0.5, synchronous=False):
        raise NotImplementedError()

    def stopPlayback(self):
        raise NotImplementedError()


class DefaultSoundSynthesizer(AbstractSoundSynthesizer):
    def __updateSoundHandle(self):
        if AbstractSoundSynthesizer.soundPopen != None:
            AbstractSoundSynthesizer.soundPopen.poll()
            if AbstractSoundSynthesizer.soundPopen.returncode != None:
                AbstractSoundSynthesizer.soundPopen = None  # process finished

    def __executeCmdIfPossible(self, cmd, timeOffset, synchronous):
        self.__updateSoundHandle()
        if AbstractSoundSynthesizer.soundPopen != None:
            logging.debug("Sound blocked")
            return False  # cannot play multiple sounds at the same time

        if timeOffset > 0:
            cmd = "sleep %fs; %s" % (timeOffset, cmd)

        AbstractSoundSynthesizer.soundPopen = Popen(cmd,
                                                    stdin=DEVNULL, stdout=DEVNULL, stderr=DEVNULL,
                                                    close_fds=True, shell=True, preexec_fn=setsid)  # start_new_session=True,
        if synchronous:
            AbstractSoundSynthesizer.soundPopen.wait()
            AbstractSoundSynthesizer.soundPopen = None
        return True

    def playSound(self, filename, timeOffset=0.5, synchronous=False):
        lockAvail = False
        try:
            lockAvail = AbstractSoundSynthesizer.soundLock.acquire(False)
        except Exception:
            logging.warning("Could not acquire sound lock")
        if not lockAvail:
            return False

        try:
            res = self.__executeCmdIfPossible(
                "aplay %s" % filename, timeOffset, synchronous)
            if res:
                logging.debug("Playing sound %s", filename)
            return res
        except Exception as error:
            logging.error("Could not play file %s\nReason: %s",
                          filename, str(error))
        finally:
            AbstractSoundSynthesizer.soundLock.release()
        return False

    def playTextOnce(self, text, language, timeOffset=0.5, synchronous=False):
        lockAvail = False
        try:
            lockAvail = AbstractSoundSynthesizer.soundLock.acquire(False)
        except Exception:
            logging.warning("Could not acquire sound lock")
        if not lockAvail:
            return False

        try:
            res = self.__executeCmdIfPossible(
                "pico2wave -l %s -w /tmp/AudiblePlayer-tmp.wav \"%s\"; aplay /tmp/AudiblePlayer-tmp.wav > /dev/null 2> /dev/null; rm /tmp/AudiblePlayer-tmp.wav" %
                (language, text), timeOffset, synchronous)
            if res:
                logging.debug("Reading text: %s", text)
            return res
        except Exception as error:
            logging.error("Could not play text\nReason: %s", str(error))
            logging.debug("Text: %s", text)
        finally:
            AbstractSoundSynthesizer.soundLock.release()
        return False

    def stopPlayback(self):
        locked = AbstractSoundSynthesizer.soundLock.acquire(timeout=0.5)
        try:
            if locked:  # if lock wasn't possible some sync execution is running
                self.__updateSoundHandle()
            if AbstractSoundSynthesizer.soundPopen != None:
                killpg(getpgid(AbstractSoundSynthesizer.soundPopen.pid), SIGTERM)
                logging.debug("Terminated current voice")
        finally:
            if locked:
                AbstractSoundSynthesizer.soundLock.release()
