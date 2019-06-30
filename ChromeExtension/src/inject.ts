import WebsiteControlRegistry from "./WebsiteControlRegistry";
import { getUrlFromOfflineUrl, isOfflineUrl } from "./OfflineUrlHelper";

let bgPort: chrome.runtime.Port | null = null;
const bgConnectRetryTimeout = 500;
const control = WebsiteControlRegistry.getInstance().createControl(window.location.host, window.location.protocol, window);

if (control === null)
    console.error("Unsupported site");

function backgroundDisconnected(port: chrome.runtime.Port): void {
    bgPort = null;
    control.setPort(bgPort);
    console.error("Background script disconnected");
    setTimeout(connectToBackground, bgConnectRetryTimeout);
}

let statusTimeout: number | null = null;
function resendStatusAfterTimeout(timeout = 750) {
    if (statusTimeout != null)
        clearTimeout(statusTimeout); // restart in case multiple actions happen
    statusTimeout = setTimeout(() => {
        statusTimeout = null;
        if (control.isPlaying())
            bgPort.postMessage("playing");
        else
            bgPort.postMessage("paused");
    }, timeout);
}


function backgroundMessage(msg: any, port: chrome.runtime.Port): void {
    if (!(msg instanceof String) && typeof msg !== "string") {
        console.error("Invalid message received from background script", msg);
        return;
    }

    const cmd = msg.split('\n', 3);
    switch (cmd[0]) {
        case "status":
            if (!control.isReady())
                bgPort.postMessage("log\ndebug\nAsked for status but site is not ready yet.")
            else if (control.isPlaying())
                bgPort.postMessage("playing");
            else
                bgPort.postMessage("paused");
            break;
        case "load":
            if (cmd.length != 2) {
                console.error("Invalid command for load", msg);
                return;
            }
            const url = isOfflineUrl(cmd[1]) ? getUrlFromOfflineUrl(cmd[1]) : null;
            if (url !== null)
                console.info("Loading special offline url: " + url);
            if (url === null && !window.location.href.startsWith(cmd[1]))
                window.location.href = cmd[1];
            else if (url !== null && !window.location.href.startsWith(url)) // offline
                window.location.href = url;
            else
                console.info("Ignoring load, since the url is the same:", cmd[1]);
            break;
        case "play":
            if (!control.isPlaying() && control.play()) {
                console.info("playing");
                bgPort.postMessage("playing");
            }
            resendStatusAfterTimeout();
            break;
        case "pause":
            if (control.isPlaying() && control.pause()) {
                console.info("paused");
                bgPort.postMessage("paused");
            }
            resendStatusAfterTimeout();
            break;
        case "playSwitch":
            console.log("Switching play/pause state");
            if (control.isPlaying()) {
                if (control.pause()) {
                    console.info("paused");
                    bgPort.postMessage("paused");
                }
            }
            else if (control.play()) {
                console.info("playing");
                bgPort.postMessage("playing");
            }
            resendStatusAfterTimeout();
            break;
        case "rewind":
            if (cmd.length > 2) {
                console.error("Invalid command for rewind", msg);
                return;
            }
            const rsecs = cmd.length == 2 ? parseInt(cmd[1]) : undefined;
            console.info("rewind", rsecs);
            control.rewind(rsecs);
            resendStatusAfterTimeout();
            break;
        case "forward":
            if (cmd.length > 2) {
                console.error("Invalid command for forward", msg);
                return;
            }
            const fsecs = cmd.length == 2 ? parseInt(cmd[1]) : undefined;
            console.info("forward", fsecs);
            control.forward(fsecs);
            resendStatusAfterTimeout();
            break;
        default:
            console.error("Unknown command received:", msg);
            break;
    }
}

function connectToBackground(): void {
    if (bgPort !== null)
        return;
    try {
        bgPort = chrome.runtime.connect();
    } catch (error) {
        console.warn("Connecting to background failed. Retrying...", error);
        setTimeout(connectToBackground, bgConnectRetryTimeout);
        return;
    }
    console.info("Connected to background script");
    bgPort.onDisconnect.addListener(backgroundDisconnected);
    bgPort.onMessage.addListener(backgroundMessage);
    if (control === null)
        bgPort.postMessage("log\nerror\nSite not supported.");
    else
        control.setPort(bgPort);
}

// Start
connectToBackground();

console.log("Custom script inject complete.");
