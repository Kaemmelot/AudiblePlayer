__version_info__ = (1, 0, 0)
__version__ = '.'.join(map(str, __version_info__))
__author__ = "Sebastian Hofmann (Kaemmelot)"
__all__ = ['Card']


class Card(object):
    """The RFID card class. Contains the application relevant (read-only) contents of an RFID card"""

    def __init__(self, uid, content):
        self._uid = uid
        self._content = content

    @property
    def uid(self):
        return self._uid

    @property
    def content(self):
        return self._content
