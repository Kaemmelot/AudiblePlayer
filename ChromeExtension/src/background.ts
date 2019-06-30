import { getUrlFromOfflineUrl, isOfflineUrl } from "./OfflineUrlHelper";

let cmdSocket: WebSocket | null = null;
let socketOpen = false; // socket will be set once it opens, but might be still closed
let socketCloseExpected = true; // socket closing from the other side expected
let reconnect = false;
let socketReconnectTries = 0; // current reconnect tries after unexpected close
let currentPort: chrome.runtime.Port | null = null;
let currentTab: chrome.tabs.Tab | null = null;
let terminate = false; // socket closing from this side
let lastLoad: string | null = null;
let loadErrorTimer: number | null = null; // wait for tab to load, but trigger an error of that wasn't possible

const host = "127.0.0.1";
const port = 1025;
const retryTimeMs = 2000;
const retriesTillTabClose = 5;
const waitForLoadMs = 15000;

function connectToSocket(): void {
    try {
        cmdSocket = new WebSocket("ws://" + host + ':' + port);
        terminate = false;
        socketCloseExpected = false;
    } catch (error) {
        if (reconnect) { // if socket was closed unexpected
            console.info(error);
            socketReconnectTries++; // try to reconnect for retriesTillTabClose times before closing the tab and possibly stopping any playback
            if (socketReconnectTries >= retriesTillTabClose) {
                console.info("Considering socket as closed. Pausing...");
                if (loadErrorTimer !== null) {
                    clearTimeout(loadErrorTimer);
                    loadErrorTimer = null;
                }
                if (currentTab) {
                    lastLoad = null;
                    chrome.tabs.create({});
                    chrome.tabs.remove(currentTab.id);
                }
                socketCloseExpected = true;
                reconnect = false;
            }
        }
        setTimeout(connectToSocket, retryTimeMs); // retry after some time
        return;
    }
    cmdSocket.addEventListener("open", function (ev) {
        socketOpen = true; // now the socket is really open
        socketReconnectTries = 0;
        console.info("Socket opened");
        if (reconnect)
            cmdSocket.send("status"); // send current status, so cmdSocket knows it
        reconnect = false;
    });
    cmdSocket.addEventListener("error", onSocketError);
    cmdSocket.addEventListener("close", onSocketClose);
    cmdSocket.addEventListener("message", onSocketReceive);
}

function onSocketClose(ev: CloseEvent) {
    if (socketOpen && !terminate) {
        if (socketCloseExpected)
            console.info("Socket closed as expected");
        else {
            console.error("Socket closed unexpected:", ev);
            reconnect = true;
        }
        cmdSocket = null; // set this to null only if socket was open (could be connecting to new socket)
        setTimeout(connectToSocket, retryTimeMs);
    }
    socketOpen = false;
}

function onSocketError(ev: Event): void {
    if (terminate || cmdSocket == null)
        return;
    if (socketOpen) {
        // existing connection had error
        socketOpen = false;
        console.error("Socket had error:", ev);
        try {
            cmdSocket.close(); // consider socket as closed
        } catch (error) { }
        cmdSocket = null;
        setTimeout(connectToSocket, retryTimeMs);
    } else {
        // could not establish connection on new socket
        cmdSocket = null;
        setTimeout(connectToSocket, retryTimeMs);
    }
}

function closeSocket() {
    if (cmdSocket != null && !terminate) {
        terminate = true; // set marker, that socket was closed on purpose
        cmdSocket.close();
    }
}

function onSocketReceive(ev: MessageEvent): void {
    if (socketCloseExpected) {
        console.error("Expected socket close, but received new command. Closing the tab and socket.", ev.data);
        if (currentTab) {
            lastLoad = null;
            chrome.tabs.create({});
            chrome.tabs.remove(currentTab.id);
            currentTab = null;
        }
        if (loadErrorTimer !== null) {
            clearTimeout(loadErrorTimer);
            loadErrorTimer = null;
        }
        closeSocket();
        setTimeout(connectToSocket, retryTimeMs);
        return;
    }

    if (!(ev.data instanceof String) && typeof ev.data !== "string") {
        console.error("Invalid message received on socket:", ev.data);
        return;
    }

    const cmd = (ev.data as string).split('\n', 3);
    switch (cmd[0]) {
        case "shutdown":
            socketCloseExpected = true;
        case "reset":
            if (loadErrorTimer !== null) {
                clearTimeout(loadErrorTimer);
                loadErrorTimer = null;
            }
            if (currentTab) {
                lastLoad = null;
                chrome.tabs.create({});
                chrome.tabs.remove(currentTab.id);
            }
            break;
        case "load":
            if (cmd.length != 2) {
                console.error("Invalid command for load:", ev.data);
                return;
            }
            if (!currentTab || !currentPort) {
                lastLoad = ev.data as string;
                const url = isOfflineUrl(cmd[1]) ? getUrlFromOfflineUrl(cmd[1]) : cmd[1];
                if (isOfflineUrl(cmd[1]))
                    console.info("Loading special offline url: " + url);
                chrome.tabs.create({ url: url }, function (tab: chrome.tabs.Tab): void {
                    currentTab = tab;
                    loadErrorTimer = setTimeout(onLoadTimeout, waitForLoadMs);
                });
                return;
            }
            // else send to tab to switch page
            if (!currentPort) {
                console.warn("could not send command, since there is no tab connected");
                return;
            }
            if ((ev.data as string) !== lastLoad) { // no need to do this after a reconnect
                console.debug("Forwarding message to page:", ev.data as string);
                currentPort.postMessage(ev.data as string);
            }
            break;
        case "playSwitch":
        case "play":
        case "pause":
        case "rewind":
        case "forward":
            if (!currentPort) {
                console.warn("could not send command, since there is no tab connected");
                return;
            }
            console.debug("Forwarding message to page:", ev.data as string);
            currentPort.postMessage(ev.data as string);
            break;
        case "status":
            if (currentPort) {
                console.debug("Forwarding message to page:", ev.data as string);
                currentPort.postMessage(ev.data as string);
            } else {
                cmdSocket.send("unloaded");
            }
            break;
        default:
            console.error("unknown command received:", ev.data);
    }
}

function onLoadTimeout(): void {
    // reset, send error and close tab
    loadErrorTimer = null;

    console.error("Page loading timed out after " + Math.round(waitForLoadMs / 1000) + " seconds.");
    if (socketOpen) {
        cmdSocket.send("log\nerror\nPage loading timed out after " + Math.round(waitForLoadMs / 1000) + " seconds.");
        cmdSocket.send("network");
    }
    if (currentTab) {
        lastLoad = null;
        chrome.tabs.create({});
        chrome.tabs.remove(currentTab.id);
    }
}

function onPortReceive(message: any, port: chrome.runtime.Port): void {
    if (port !== currentPort) {
        port.disconnect(); // why could this even be connected?
        return;
    }

    if (!socketOpen)
        return;

    if (!(message instanceof String) && typeof message !== "string") {
        console.error("Invalid message received on port:", message);
        return;
    }

    switch (message.split('\n', 1)[0]) {
        case "readout":
        case "log":
        case "paused":
        case "playing":
        case "finished":
        case "loaded":
            console.debug("Forwarding message to socket:", message);
            cmdSocket.send(message as string);
            break;
        case "network":
            console.error("Connection lost");
            cmdSocket.send(message as string);
            if (currentTab) {
                lastLoad = null;
                chrome.tabs.create({});
                chrome.tabs.remove(currentTab.id);
            }
            break;
        default:
            console.error("Invalid message received on port:", message);
            break;
    }
}

function onPortDisconnect(port: chrome.runtime.Port): void {
    if (port !== currentPort)
        return; // this would be strange

    currentPort = null;
    currentTab = null;
    if (socketOpen && !socketCloseExpected)
        cmdSocket.send("unloaded");
    // TODO check for new tab after timeout?
}

function onPortConnect(port: chrome.runtime.Port): void {
    if (!port.sender || !port.sender.id || port.sender.id !== chrome.runtime.id || currentPort !== null) {
        // only accept valid connections from this extension
        // also ignore opened tabs from the user
        port.disconnect();
        return;
    }

    if (loadErrorTimer !== null) { // cancel error timer
        clearTimeout(loadErrorTimer);
        loadErrorTimer = null;
    }

    currentPort = port;
    currentPort.onDisconnect.addListener(onPortDisconnect);
    currentPort.onMessage.addListener(onPortReceive);
    if (currentPort.sender.tab) {
        currentTab = currentPort.sender.tab;
        chrome.tabs.query({}, tabs => {
            // close all tabs but this current one
            tabs = tabs.filter(tab => tab.id !== currentTab.id);
            console.debug("Closing tabs without current tab id of " + currentTab.id, tabs);
            chrome.tabs.remove(tabs.map(tab => tab.id));
        });
    } else
        console.warn("Could not find the tab that just connected")
    if (lastLoad != null) // be sure correct page is loaded
        currentPort.postMessage(lastLoad);
}
chrome.runtime.onConnect.addListener(onPortConnect);

// Start
connectToSocket();
