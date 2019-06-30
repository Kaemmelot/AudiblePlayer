import logging
import subprocess
from threading import BoundedSemaphore, Timer

__version_info__ = (1, 0, 0)
__version__ = '.'.join(map(str, __version_info__))
__author__ = "Sebastian Hofmann (Kaemmelot)"
__all__ = ["repeatCmd", "stopAllCmds"]


shutdown = False
timersSema = BoundedSemaphore(1)
timers = []


def repeatCmd(cmd, interval, name, repeatOnError=False, t=-1):
    if shutdown:
        return
    err = False
    try:
        logging.info("Command %s returned: %s", name, subprocess.check_output(
            cmd, timeout=2000).decode("utf-8").rstrip("\r\n"))  # universal_newlines=True,
    except subprocess.CalledProcessError as error:
        logging.error("Error executing repeated command %s: %s",
                      name, str(error))
        err = True
    except subprocess.TimeoutExpired:
        logging.error("Command %s took to long to execute", name)
        err = True

    timersSema.acquire()
    stop = shutdown or (err and not repeatOnError)
    if t < 0 and not stop:
        t = len(timers)
        timers.append(None)  # add dummy
    if not stop:
        timers[t] = Timer(interval, repeatCmd, [
                          cmd, interval, name, repeatOnError, t])
        timers[t].start()
    else:
        timers[t] = None  # remove entry without starting a new timer
    timersSema.release()


def stopAllCmds():
    shutdown = True
    timersSema.acquire()
    for t in timers:
        if t != None:  # stop all timers
            t.cancel()
    timersSema.release()
