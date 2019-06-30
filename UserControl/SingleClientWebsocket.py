import asyncio
import logging
from threading import Thread, currentThread

import websockets

__version_info__ = (1, 0, 0)
__version__ = '.'.join(map(str, __version_info__))
__author__ = "Sebastian Hofmann (Kaemmelot)"
__all__ = ['SingleClientWebsocket']


class SingleClientWebsocket:
    def __init__(self, ip, port, msgCallback, connCallback=None):
        self.__terminated = False
        self._clientWs = None
        self._server = None
        self._loopThread = None
        self._eventLoop = asyncio.new_event_loop()
        self._startServer = websockets.serve(
            self._socketHandler, ip, port, loop=self._eventLoop)
        self._msgCallback = msgCallback
        self._connCallback = connCallback
        self._loopThread = Thread(
            target=self._eventLoop.run_forever, name="SingleClientWebsocket._eventLoop")

    async def _socketHandler(self, websocket, path):
        if self._clientWs != None:
            try:
                await self._clientWs.ping()
                logging.warning(
                    "Someone tried to connect even though the socket is already in use")
                return
            except:
                logging.warning(
                    "Closing current socket forcefully, because it doesn't react anymore and a new connection was opened")

        if path != "" and path != "/":
            return  # invalid path

        self._clientWs = websocket
        logging.debug("Websocket connected")
        if self._connCallback != None:
            self._connCallback(True)
        try:
            while not self.__terminated:  # this is just to pass on everything we receive
                msg = await websocket.recv()
                if self._clientWs != websocket:
                    websocket.close()
                    return  # we have been closed forcefully
                self._msgCallback(msg)
        except websockets.ConnectionClosed:
            # this is called when the connection was closed, so we stop
            logging.debug("Websocket closed")
        except asyncio.CancelledError:
            logging.warning("Got asyncio.CancelledError")  # should not happen
        finally:
            if self._clientWs == websocket:
                self._clientWs = None
                if self._connCallback != None:
                    self._connCallback(False)

    def start(self):
        if not self.__terminated and not self._eventLoop.is_running():
            self._server = self._eventLoop.run_until_complete(
                self._startServer)
            self._loopThread.start()

    def stop(self):
        if not self.__terminated and self._server != None:
            logging.debug("Stopping SingleClientWebsocket")
            self.__terminated = True
            try:
                if self._clientWs != None:
                    asyncio.run_coroutine_threadsafe(
                        self._clientWs.close(), self._eventLoop).result()  # close open socket
            except asyncio.CancelledError:
                pass
            self._server.close()  # close server
            self._server.wait_closed()
            self._server = None
            self._eventLoop.call_soon_threadsafe(self._eventLoop.stop)
            self._loopThread.join()  # wait until loop stopped
            # needed for next run_until_complete call
            asyncio.set_event_loop(self._eventLoop)
            try:
                self._eventLoop.run_until_complete(asyncio.gather(
                    *asyncio.Task.all_tasks(self._eventLoop)))  # finish all pending tasks
            except asyncio.CancelledError:
                pass
            self._eventLoop.close()  # now we can safely close the loop

    def send(self, msg):
        try:
            if self._clientWs != None:
                res = asyncio.run_coroutine_threadsafe(
                    self._clientWs.send(msg), self._eventLoop)  # send
                if self._loopThread != currentThread():
                    res.result()  # and wait until complete (only if other thread)
                #logging.debug("Sending message via websocket:\n%s", msg)
                return True
            logging.debug("Could not send message via websocket:\n%s", msg)
        except Exception as error:
            logging.error(
                "Exception prevented message to be send via websocket:\n%s", str(error))
        return False

    @property
    def connected(self):
        return self._clientWs != None
