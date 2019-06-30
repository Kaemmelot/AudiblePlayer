from configparser import ConfigParser

__version_info__ = (1, 0, 0)
__version__ = '.'.join(map(str, __version_info__))
__author__ = "Sebastian Hofmann (Kaemmelot)"
__all__ = ['loadConfigFromFile', 'getAdditionalSections']

defaultValues = {
    "UserControl": {
        "language": "en-US",
        "key1": "FF",
        "key2": "FF",
        "key3": "FF",
        "key4": "FF",
        "key5": "FF",
        "key6": "FF",
        "selectedKey": "A",
        "volumePercent": "5",
        "readyRepeat": "0.0",
        "loggingDebug": "false",
        "useOffline": "true",
        "offlineDir": "/automnt/offlineBooks"
    },
    "InputPins": {
        "shutdown": "40",
        "shutdownOffset": "1000",
        "shutdownEdgeFalling": "true",
        "playPause": "16",
        "playPauseBouncetime": "500",
        "playPauseEdgeFalling": "true",
        "playPausePullup": "true",
        "rewind": "7",
        "rewindBouncetime": "500",
        "rewindEdgeFalling": "true",
        "rewindPullup": "true",
        "forward": "22",
        "forwardBouncetime": "500",
        "forwardEdgeFalling": "true",
        "forwardPullup": "true",
        "volumeClk": "11",
        "volumeDt": "12",
        "irq": "13"
    },
    "OutputPins": {
        "statusLed": "18",
        "rst": "15",
        "ce": "24"
    },
    "Chromium": {
        "checktime": "20.0",
        "display": "1",
        "recheckBrowser": "150.0"
    },
    "Extra": {
        "socketHost": "127.0.0.1",
        "socketPort": "1025",
        "readRepeatSecs": "1.0",
        "readRetries": "5"
    }
}


def loadConfigFromFile(file):
    config = ConfigParser()
    config.read_dict(defaultValues)
    # now override with real values
    config.read(file)
    return config


def getAdditionalSections(config):
    sections = config.sections()
    return [s for s in sections if not s in defaultValues]
