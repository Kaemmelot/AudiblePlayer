#!/usr/bin/env python3
# encoding: utf-8

import logging
import re
import subprocess
import urllib.parse
from os import WIFEXITED, _exit, fork, path, popen, setsid, sys, system
from posixpath import normpath
from signal import (SIG_IGN, SIGHUP, SIGINT, SIGKILL, SIGSTOP, SIGTERM,
                    Signals, default_int_handler, signal)
from sys import exit
from threading import Semaphore, Thread, Timer, enumerate

import RPi.GPIO as GPIO
from Configuration import *
from GpioInput import *
from GpioOutput import *
from RepeatCmd import *
from RfidReader import *
from SingleClientWebsocket import *
from SoundSynthesizer import DefaultSoundSynthesizer
from ThreadingRangeHTTPServer import get_threaded_server, run_server

__version_info__ = (1, 0, 0)
__version__ = '.'.join(map(str, __version_info__))
__author__ = "Sebastian Hofmann (Kaemmelot)"
__all__ = []


runSema = Semaphore(0)
# load config
config = loadConfigFromFile("config.ini")
# initialize logging
# standalone format:
#"%(asctime)-15s (%(module)s->%(funcName)s) [%(levelname)s] %(message)s"
# syslog format:
logging.basicConfig(
    format="(%(module)s->%(funcName)s) [%(levelname)s] %(message)s",
    level=logging.DEBUG if config.getboolean("UserControl", "loggingDebug") else logging.INFO)
logging.debug("Configuration and logging ready")


def sigHandler(sigNum, stackFrame):
    logging.debug("Received signal: %d" % sigNum)
    runSema.release()


def ignoreHandler(sigNum, stackFrame):
    logging.debug("Ignoring signal: %d" % sigNum)


class UserControl:
    _sysShutdown = False
    pausedStatus = [0.75, 1.5]
    errorStatus = [0.25, 0.5, 0.25, 0.5, 1.5, 0.5]

    def __init__(self, config):
        self.config = config
        if self.config.getint("InputPins", "shutdown") > 0:
            self._shutdownButton = GpioInputButton(self.config.getint("InputPins", "shutdown"), holdCallback=self._shutdown,
                                                   singleHoldCall=True, holdTimeOffset=self.config.getint("InputPins", "shutdownOffset"),
                                                   pull=GPIO.PUD_OFF, edge=GPIO.FALLING if self.config.getboolean("InputPins", "shutdownEdgeFalling") else GPIO.RISING)
        else:
            self._shutdownButton = None
        self.__running = True
        self._statusLed = GpioOutputLed(self.config.getint(
            "OutputPins", "statusLed"))  # led is off
        logging.debug("Initializing user control")

        self._reader = None
        self._websocket = None
        self._offlineServer = None
        self._sound = None
        self.__browserTimer = None
        self.__readyTimer = None
        self.__recheckStatusTimer = None
        if self._shutdownButton != None and self._shutdownButton.isPressed:
            self._shutdown(1, True)  # shutdown request after start
            return
        self.__paused = True
        self.__networkError = False
        self.__currentUrl = None
        self._sound = DefaultSoundSynthesizer()
        self._sound.playSound(self.config.get(
            "UserControl", "language") + "/startup.wav", 0)

        rfidKey = [
            int(self.config.get("UserControl", "key1"), 16),
            int(self.config.get("UserControl", "key2"), 16),
            int(self.config.get("UserControl", "key3"), 16),
            int(self.config.get("UserControl", "key4"), 16),
            int(self.config.get("UserControl", "key5"), 16),
            int(self.config.get("UserControl", "key6"), 16)
        ]
        rfidKeyType = self.config.get("UserControl", "selectedKey")
        self._reader = RfidReader(
            self._readerDetect, self.config.getint(
                "OutputPins", "rst"), self.config.getint("OutputPins", "ce"),
            self.config.getint("InputPins", "irq"), GPIO.BOARD, rfidKey, rfidKeyType)

        wsHost = self.config.get("Extra", "socketHost")
        wsPort = self.config.getint("Extra", "socketPort")
        self._websocket = SingleClientWebsocket(
            wsHost, wsPort, self._websocketMsg, self._websocketConn)
        self._websocket.start()

        self._playButton = GpioInputButton(
            self.config.getint("InputPins", "playPause"), self._playPause,
            pull=GPIO.PUD_UP if self.config.getboolean(
                "InputPins", "playPausePullup") else GPIO.PULL_DOWN,
            edge=GPIO.FALLING if self.config.getboolean(
                "InputPins", "playPauseEdgeFalling") else GPIO.RISING,
            bouncetime=self.config.getint("InputPins", "playPauseBouncetime"))

        if self.config.getint("InputPins", "rewind") > 0:
            logging.debug("Rewind enabled on pin %d" %
                          self.config.getint("InputPins", "rewind"))
            self._rewindButton = GpioInputButton(
                self.config.getint(
                    "InputPins", "rewind"), self._rewind, self._rewind,
                pull=GPIO.PUD_UP if self.config.getboolean(
                    "InputPins", "rewindPullup") else GPIO.PULL_DOWN,
                edge=GPIO.FALLING if self.config.getboolean(
                    "InputPins", "rewindEdgeFalling") else GPIO.RISING,
                bouncetime=self.config.getint("InputPins", "rewindBouncetime"))
        else:
            logging.debug("Rewind disabled")
            self._rewindButton = None

        if self.config.getint("InputPins", "forward") > 0:
            logging.debug("Forward enabled on pin %d" %
                          self.config.getint("InputPins", "forward"))
            self._forwardButton = GpioInputButton(
                self.config.getint(
                    "InputPins", "forward"), self._forward, self._forward,
                pull=GPIO.PUD_UP if self.config.getboolean(
                    "InputPins", "forwardPullup") else GPIO.PULL_DOWN,
                edge=GPIO.FALLING if self.config.getboolean(
                    "InputPins", "forwardEdgeFalling") else GPIO.RISING,
                bouncetime=self.config.getint("InputPins", "forwardBouncetime"))
        else:
            logging.debug("Forward disabled")
            self._forwardButton = None

        if self.config.getint("InputPins", "volumeClk") > 0 and self.config.getint("InputPins", "volumeDt") > 0:
            logging.debug("Volume control enabled on pins %d and %d" % (self.config.getint(
                "InputPins", "volumeClk"), self.config.getint("InputPins", "volumeDt")))
            self._volRotaryEncoder = GpioInputRotaryEncoder(
                self.config.getint("InputPins", "volumeClk"), self.config.getint("InputPins", "volumeDt"), self._volume)
        else:
            logging.debug("Volume control disabled")
            self._volRotaryEncoder = None

        self._checkAndStartBrowser()

        if self.config.getboolean("UserControl", "useOffline"):
            serverDir = self.config.get("UserControl", "offlineDir")
            self._offlineServer = get_threaded_server(
                port=8081, serve_path=serverDir)
            Thread(target=run_server, name="offlineServer.run",
                   kwargs={"server": self._offlineServer}).start()
            logging.debug(
                "Server for offline player launched with directory '%s'" % serverDir)
        logging.info("User control ready")

    def _readyRepeat(self):
        self.__readyTimer = None
        if self.__running and self._reader.currentCard == None:
            self._sound.playSound(self.config.get(
                "UserControl", "language") + "/readyRepeat.wav")
            self.__readyTimer = Timer(self.config.getfloat(
                "UserControl", "readyRepeat"), self._readyRepeat)
            self.__readyTimer.start()

    def __recheckStatus(self):
        self.__recheckStatusTimer = None
        if self.__running and self.config.getfloat("Chromium", "recheckBrowser") > 0 and self._websocket.connected:
            if self._websocket.send("status"):
                logging.info("Rechecking current browser status")
            else:
                self._internalError("Status check via websocket failed", False)
            self.__recheckStatusTimer = Timer(
                self.config.getfloat("Chromium", "recheckBrowser"), self.__recheckStatus)
            self.__recheckStatusTimer.start()

    def __restartStatusCheck(self):
        if self.__recheckStatusTimer != None:
            self.__recheckStatusTimer.cancel()
            self.__recheckStatusTimer = None
        if self._websocket.connected and self.config.getfloat("Chromium", "recheckBrowser") > 0:
            self.__recheckStatusTimer = Timer(
                self.config.getfloat("Chromium", "recheckBrowser"), self.__recheckStatus)
            self.__recheckStatusTimer.start()

    def _internalError(self, error, showUser=True, soundFile="error"):
        logging.error(error)
        if showUser and self.__running:
            self._sound.playSound(self.config.get(
                "UserControl", "language") + "/" + soundFile + ".wav")
            self._statusLed.setPattern(self.errorStatus)

    def _readerDetect(self, card):
        if not self.__running:
            return
        if card != None:
            self.__restartStatusCheck()  # this shouldn't trigger while loading
            self._statusLed.off()  # turn off while loading the page
            logging.info("RFID-card detected: %s; Content:\n%s",
                         arrayToHexString(card.uid), card.content)
            urls = card.content.split(";")
            offlineUrl = None
            i = 0
            while i < len(urls):
                # TODO compile
                if re.match("^https?:\/\/(?:[a-z0-9_\-]+)+(?:\.[a-z0-9_\-]+)+(?:\/(?:[a-z0-9_\-\.]|%[\da-f]{2})+)+\/?(?:\?.*|#.*)?$", urls[i], re.IGNORECASE) != None:
                    i += 1  # url match
                elif re.match("^offline:\/\/(?:(?:[a-z0-9_\-\.]|%[\da-f]{2})+\/?)+$", urls[i], re.IGNORECASE) != None:
                    offlineUrl = urls[i]  # offline url match
                    urls.remove(urls[i])  # remove url
                    logging.debug("Found offline url")
                else:
                    logging.warn("Invalid URL on card: %s" % urls[i])
                    urls.remove(urls[i])  # remove url
            if len(urls) == 0 and offlineUrl == None:
                self._internalError("No valid URL on card",
                                    soundFile="invalidCard")
                return
            # TODO we could now try every url on this card but for now we check the offline url and use the first online url as fallback
            load = urls[0]
            if offlineUrl != None:
                offFile = normpath(path.join(self.config.get(
                    "UserControl", "offlineDir"), urllib.parse.unquote(offlineUrl[10:])))
                if path.exists(offFile):
                    load = offlineUrl
            self.__currentUrl = load
            logging.debug("Selected url '%s'" % self.__currentUrl)
            if self._websocket.connected:
                if self._websocket.send("load\n" + load):
                    self._sound.playSound(self.config.get(
                        "UserControl", "language") + "/loading.wav")
                    self.__paused = True
                    self.__networkError = False
                else:
                    self._internalError("Load via websocket failed")
        else:
            logging.info("RFID-card removed")
            if self._websocket.connected:
                self.__currentUrl = None
                if self._websocket.send("reset"):
                    self._statusLed.on()  # on equals ready for input (book/rfid)
                    self.__paused = True
                    self.__networkError = False
                else:
                    self._internalError("Reset via websocket failed")
            if self.config.getfloat("UserControl", "readyRepeat") > 0 and self.__readyTimer == None:
                self.__readyTimer = Timer(
                    self.config.getfloat("UserControl", "readyRepeat"), self._readyRepeat)  # start delayed
                self.__readyTimer.start()

    def _websocketConn(self, connected):
        if not self.__running:
            return
        if connected and self.__currentUrl != None:
            logging.info("Browser connected, loading book")
            if self._websocket.send("load\n" + self.__currentUrl):
                self._sound.stopPlayback() # abort sound
                self._sound.playSound(self.config.get(
                    "UserControl", "language") + "/loading.wav")
                self._statusLed.off()  # while loading the page
                self.__paused = True
                self.__networkError = False
            else:
                self._internalError("Load via websocket failed")
        elif connected:  # no card
            logging.info("Browser connected")
            self._statusLed.on()  # on equals ready for input (book/rfid)
            if self.config.getfloat("UserControl", "readyRepeat") > 0 and self.__readyTimer == None:
                self._readyRepeat()  # tell the user we are ready to go
            elif self.config.getfloat("UserControl", "readyRepeat") <= 0:
                self._sound.stopPlayback() # abort sound
                self._sound.playSound(self.config.get(
                    "UserControl", "language") + "/ready.wav")
        elif not connected:
            logging.info("Browser disconnected")
            self._statusLed.setPattern(self.errorStatus)
            if self.__browserTimer == None:
                self._checkAndStartBrowser()

        # run status checks while the browser is connected
        self.__restartStatusCheck()

    def _websocketMsg(self, msg, retry=0):
        lines = msg.split('\n', 3)

        if lines[0] == "log" and len(lines) == 3:
            if lines[1] == "debug":
                logging.debug("BROWSER: " + lines[2])
            elif lines[1] == "info":
                logging.info("BROWSER: " + lines[2])
            elif lines[1] == "warn":
                logging.warning("BROWSER: " + lines[2])
            elif lines[1] == "error":
                logging.error("BROWSER: " + lines[2])
            else:
                logging.warning(
                    "Invalid browser log command: %s\n%s" % (lines[1], lines[2]))
            return

        if not self.__running:
            return  # only log commands are allowed while stopping

        if lines[0] == "readout" and len(lines) == 2:
            # replace all spaces/newlines
            text = re.sub("\s", " ", lines[1], flags=re.MULTILINE)
            # make sure only allowed chars will be passed to the shell
            text = re.sub("[^\w ]", "", text)
            self._sound.stopPlayback() # abort sound
            success = self._sound.playTextOnce(
                text, self.config.get("UserControl", "language"))
            if not success and retry < self.config.getint("Extras", "readRetries"):
                Timer(self.config.getfloat("Extras", "readRepeatSecs"), self._websocketMsg, kwargs={
                      "msg": msg, "retry": retry + 1}).start()  # try again later
            elif not success:
                self._internalError(
                    "Could not read message, because device was still busy", False)
            else:
                logging.debug("Read text: %s", text)
        elif lines[0] == "playing":
            if self.__paused:
                self.__paused = False
                self._statusLed.on()
                logging.info("Now playing")
            else:
                logging.debug("Still playing")
        elif lines[0] == "paused":
            if not self.__paused:
                self.__paused = True
                self._statusLed.setPattern(self.pausedStatus)
                logging.info("Now paused")
                self._sound.playSound(self.config.get(
                    "UserControl", "language") + "/paused.wav")
            else:
                logging.debug("Still paused")
        elif lines[0] == "finished":
            self.__paused = True
            self._statusLed.setPattern(self.pausedStatus)
            logging.info("Book finished")
        elif lines[0] == "loaded" and len(lines) == 2:
            self.__paused = True
            self.__networkError = False
            if self.__currentUrl == None:
                self._internalError(
                    "Browser opened %s but there is no RFID-card" % lines[1])
            elif not lines[1].startswith(self.__currentUrl):
                self._internalError(
                    "Browser opened %s, but %s is to be opened" % (lines[1], self.__currentUrl))
            else:
                self._sound.stopPlayback() # abort sound
                self._sound.playSound(self.config.get(
                    "UserControl", "language") + "/ready.wav")
                self._statusLed.setPattern(self.pausedStatus)
                logging.info("Browser loading finished")
        elif lines[0] == "unloaded":
            if self.__networkError:
                return
            self.__paused = True
            if self.__currentUrl != None:
                self._internalError(
                    "Browser unloaded page, but %s should be open now" % self.__currentUrl)
            else:
                self._statusLed.on()  # on equals ready for input (book/rfid)
            # else everything is fine
        elif lines[0] == "network":
            self.__paused = True
            self.__networkError = True
            self._internalError("Network problem", soundFile="network")
        else:
            self._internalError("Unknown command received: %s" % msg, False)

    def _playPause(self, pin=0):
        if self.__running and self._websocket.connected:
            logging.debug("play/pause pressed")
            if not self._websocket.send("playSwitch"):
                self._internalError("Play/Pause via websocket failed")
            elif self.__paused:
                self._sound.stopPlayback()  # stop any sounds if we now start reading

    def _rewind(self, pin=0):
        if self.__running and self._websocket.connected:
            logging.debug("rewinding")
            if not self._websocket.send("rewind"):
                self._internalError("Rewind via websocket failed", False)

    def _forward(self, pin=0):
        if self.__running and self._websocket.connected:
            logging.debug("forwarding")
            if not self._websocket.send("forward"):
                self._internalError("Forward via websocket failed", False)

    def _volume(self, direction):
        logging.debug("Volume changing: %s%d%%", "+" if direction >
                      0 else "", self.config.getint("UserControl", "volumePercent") * direction)
        # change the volume by percentage up or down
        subprocess.Popen("amixer set PCM %d%%%s" % (self.config.getint("UserControl", "volumePercent"), "+" if direction > 0 else "-"),
                         stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                         close_fds=True, shell=True, start_new_session=True)

    def _shutdown(self, pin=0, skipSound=False):
        if self._sysShutdown:
            return
        self._sysShutdown = True
        logging.info("Shutting down...")
        if not skipSound:
            self._sound.stopPlayback() # abort sound
            self._sound.playSound(self.config.get(
                "UserControl", "language") + "/shutdown.wav", 0, True)
        system("sudo shutdown -h now &")
        runSema.release()  # trigger own shutdown

    def _checkAndStartBrowser(self):
        if not self.__running or self.config.getfloat("Chromium", "checktime") <= 0:
            return
        try:
            # check if chromium is running and start it if it's not
            runningProcesses = popen(
                "ps x | grep -P \"[0-9] [/a-zA-Z0-9_-]+/chromium-browser\"").read()
            if len(runningProcesses) == 0:
                logging.info("Starting chromium")
                if fork() == 0:
                    setsid()  # make me process group leader
                    signal(SIGHUP, SIG_IGN)  # ignore sighup
                    subprocess.Popen(["DISPLAY=:%d chromium-browser --start-maximized & disown" % self.config.getint("Chromium", "display")],
                                     stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                                     close_fds=True, shell=True, start_new_session=True, restore_signals=False)
                    _exit(0)  # child is done, daemonize chromium
        except Exception as error:
            logging.error(
                "Checking and starting browser gave exception:\n%s", str(error))
        if self.__running:  # check again since the command above needs time
            self.__browserTimer = Timer(
                self.config.getfloat("Chromium", "checktime"), self._checkAndStartBrowser)
            self.__browserTimer.start()

    def stop(self):
        self.__running = False
        if self._statusLed != None:
            self._statusLed.off()
        logging.info("Stopping user control")
        if self.__browserTimer != None:
            self.__browserTimer.cancel()
        if self.__readyTimer != None:
            self.__readyTimer.cancel()
        if self.__recheckStatusTimer != None:
            self.__recheckStatusTimer.cancel()
        if self._reader != None:  # this would be None on an immediate shutdown
            self._reader.stop(False)
        if self._websocket != None:  # this would be None on an immediate shutdown
            self._websocket.send("shutdown")
            self._websocket.stop()
        if self._offlineServer != None:
            self._offlineServer.server_close()
        gpioInputStop(False)
        gpioOutputStop(False)
        if self._sound != None:  # this would be None on an immediate shutdown
            self._sound.stopPlayback()


control = None
try:
    # initialize signal handler
    logging.info("======= INIT =======")
    # sigHandler or default_int_handler: https://stackoverflow.com/a/40785230
    signal(SIGINT, sigHandler)  # TODO did timeout for runSema fix this?
    signal(SIGTERM, sigHandler)
    # https://stackoverflow.com/a/34568177/5516047
    #catchableSigs = set(Signals) - {SIGINT, SIGTERM, SIGKILL, SIGSTOP}
    # for sig in catchableSigs:
    # just so, that we can be sure no other handler is used
    #signal(sig, ignoreHandler)
    control = UserControl(config)
    sections = getAdditionalSections(config)
    for s in sections:  # look for commands
        if s.endswith("Command") and "command" in config[s] and "interval" in config[s] and "name" in config[s]:
            logging.debug("Found command %s" % config.get(s, "name"))
            repeatCmd(config.get(s, "command").split(' '), config.getint(s, "interval"), config.get(
                s, "name"), config.getboolean(s, "repeatOnError", fallback=False))
    # this will return true once SIGTERM was received
    while not runSema.acquire(True, 0.25):
        pass
# except KeyboardInterrupt:
#    pass
finally:
    stopAllCmds()
    if control != None:
        control.stop()
        GPIO.cleanup()

runningThreads = enumerate()
if len(runningThreads) > 1:
    threads = runningThreads.pop(0).name
    if threads == "MainThread":  # ignore main
        threads = runningThreads.pop(0).name
    for t in runningThreads:
        if t.name != "MainThread":
            threads = threads + ", " + t.name
    logging.warning("There are still threads running:\n%s", threads)

logging.info("FINAL STOP")
exit()
