import WebsiteControl from "./WebsiteControl";
import WebsiteControlRegistry, { WebsiteControlEntry } from "./WebsiteControlRegistry";

export default class AudibleControl implements WebsiteControl {
    private bgPort: chrome.runtime.Port | null = null;
    private playing = false;
    private ready = false;

    public constructor(private playElem: HTMLImageElement, private pauseElem: HTMLImageElement,
            private rewindElem: HTMLImageElement, private forwardElem: HTMLImageElement,
            private timeSpent: HTMLElement, private timeLeft: HTMLElement,
            chapterElem: HTMLElement,
            private win: Window) {
        const chapterObserver = new MutationObserver(this.chapterChanged.bind(this));
        chapterObserver.observe(chapterElem, {
            childList: true,
            characterData: true
        });
        /*const playObserver = new MutationObserver(this.playChanged.bind(this));
        playObserver.observe(playElem, {
            attributes: true,
            attributeFilter: ["class"]
        });*/ // will be registered later
        const initialPlayObserver = new MutationObserver(this.initialPlayHandler.bind(this));
        initialPlayObserver.observe(timeSpent, {
            childList: true,
            characterData: true
        });
    }

    private isHidden(elem: HTMLElement): boolean {
        // https://stackoverflow.com/a/21696585
        return elem.offsetParent === null;
    }

    private triggerElementClick(elem: HTMLElement): void {
        setTimeout(() => {
            try {
                const playPos = elem.getBoundingClientRect() as DOMRect;
                elem.dispatchEvent(new MouseEvent("click", {
                    screenX: playPos.x + playPos.width / 2,
                    screenY: playPos.y + playPos.height / 2,
                    clientX: playPos.x + playPos.width / 2,
                    clientY: playPos.y + playPos.height / 2,
                    buttons: 1,
                    detail: 1,
                    view: this.win,
                    bubbles: true,
                    cancelable: true,
                    composed: true
                }));
            } catch (error) {
                console.error("Could not trigger element click. Reason:", error);
                if (this.bgPort !== null)
                    this.bgPort.postMessage("log\nerror\nCould not trigger element click. Reason: " + error);
            }
        }, 1);
    }

    public play(): boolean {
        const playVisible = !this.isHidden(this.playElem);
        if (playVisible) {
            this.playing = true;
            this.triggerElementClick(this.playElem);
        }
        return playVisible;
    }

    public pause(): boolean {
        const pauseVisible = !this.isHidden(this.pauseElem);
        if (pauseVisible) {
            this.playing = false;
            this.triggerElementClick(this.pauseElem);
        }
        return pauseVisible;
    }

    public isPlaying(): boolean {
        return this.isReady && this.playing;
    }

    public getMaxPlaytime(): number {
        const ts = this.timeSpent.textContent.split(':');
        const tl = this.timeLeft.textContent.substr(1).split(':');
        let secs = 0;
        let factor = 1;
        for (let i = Math.min(Math.max(ts.length, tl.length), 3) - 1; i >= 0; i--) {
            if (ts.length > i)
                secs += factor * parseInt(ts[i]);
            if (tl.length > i)
                secs += factor * parseInt(tl[i]);
            factor *= 60;
        }
        return secs;
    }

    public getCurrentPlaytime(): number {
        const ts = this.timeSpent.textContent.split(':');
        let secs = 0;
        let factor = 1;
        for (let i = Math.min(ts.length, 3) - 1; i >= 0; i--) {
            secs += factor * parseInt(ts[i]);
            factor *= 60;
        }
        return secs;
    }

    public getRemainingPlayTime(): number {
        const tl = this.timeLeft.textContent.substr(1).split(':');
        let secs = 0;
        let factor = 1;
        for (let i = Math.min(tl.length, 3) - 1; i >= 0; i--) {
            secs += factor * parseInt(tl[i]);
            factor *= 60;
        }
        return secs;

    }

    public forward(time?: number): void {
        if (this.getRemainingPlayTime() > 0) {
            const times = !!time ? Math.ceil(time / 30) : 1; // only 30 sec jumps are possible
            for (let i = 0; i < times; i++)
                this.triggerElementClick(this.forwardElem);
        }
    }

    public rewind(time?: number): void {
        if (this.getCurrentPlaytime() > 0) {
            const times = !!time ? Math.ceil(time / 30) : 1; // only 30 sec jumps are possible
            for (let i = 0; i < times; i++)
                this.triggerElementClick(this.rewindElem);
        }
    }

    public setPort(port: chrome.runtime.Port | null): void {
        this.bgPort = port || null;
        if (this.bgPort !== null && this.ready)
            this.bgPort.postMessage("loaded\n" + this.win.location.href);
    }

    public isReady(): boolean {
        return this.ready;
    }

    private chapterContent = "";
    private chapterChanged(mutations: MutationRecord[], observer: MutationObserver): void {
        if (this.bgPort === null)
            return; // logging not possible

        for (let mutation of mutations) {
            if ((mutation.type === "characterData" || mutation.type === "childList") && this.chapterContent !== mutation.target.textContent) {
                this.bgPort.postMessage("log\ninfo\nChapter changed: " + mutation.target.textContent);
                this.chapterContent = mutation.target.textContent;
            } else if (mutation.type !== "characterData" && mutation.type !== "childList")
                console.error("Unsupported mutation call", mutation);
        }
    }

    private playChanged(mutations: MutationRecord[], observer: MutationObserver): void {
        const hidden = this.isHidden(this.playElem);
        if (this.playing && !hidden) {
            this.playing = false;
            let remaining = this.getRemainingPlayTime();
            if (!isNaN(remaining) && remaining > 1) {
                console.warn("Player stopped by itself");
                if (this.bgPort !== null) {
                    this.bgPort.postMessage("paused");
                    this.bgPort.postMessage("log\nwarn\nAudible-player stopped by itself.");
                }
                // trigger click? maybe better not to...
                // could be the first step to connection lost; therefore:
                setTimeout(() => {
                    if (!this.isPlaying() && isNaN(this.getRemainingPlayTime())) {
                        console.error("Connection lost");
                        if (this.bgPort !== null)
                            this.bgPort.postMessage("network");
                    }
                }, 100);
            } else if (!isNaN(remaining)) {
                console.log("Player finished");
                if (this.bgPort !== null)
                    this.bgPort.postMessage("finished");
            } else {
                console.error("Connection lost");
                if (this.bgPort !== null)
                    this.bgPort.postMessage("network");
            }
        } else if (!this.playing && hidden) {
            console.warn("Player started by itself, stopping...");
            this.triggerElementClick(this.pauseElem);
        }
        // else everything is fine
    }

    private initialPlayHandler(mutations: MutationRecord[], observer: MutationObserver): void {
        observer.disconnect();
        const playObserver = new MutationObserver(this.playChanged.bind(this));
        playObserver.observe(this.playElem, {
            attributes: true,
            attributeFilter: ["class"]
        });
        this.ready = true;
        if (this.bgPort !== null)
            this.bgPort.postMessage("loaded\n" + this.win.location.href);
        if (!this.playing && this.isHidden(this.playElem)) {
            this.triggerElementClick(this.pauseElem);
            console.debug("Initial play stopped");
        }
        if (this.getRemainingPlayTime() <= 25) {
            const restartElem = this.win.document.querySelector("#adbl-cp-chapters-display-row-0 .adblCpChaptersDisplay") as HTMLElement; // only available after book load
            const closeOverlayElem = this.win.document.getElementById("adbl-cp-chapters-close-icon") as HTMLElement;
            if (restartElem instanceof HTMLElement && closeOverlayElem instanceof HTMLElement) {
                this.triggerElementClick(restartElem); // restart if book finished
                setTimeout(() => this.triggerElementClick(closeOverlayElem), 75); // close the popup overlay
                if (this.bgPort !== null)
                    this.bgPort.postMessage("log\ninfo\nBook was automatically reset to the beginning");
            } else {
                console.error("Could not reset book");
                if (this.bgPort !== null)
                    this.bgPort.postMessage("log\nerror\nBook could not be automatically reset to the beginning");
            }
        }
    }
}

class AudibleControlEntry implements WebsiteControlEntry {
    private hostRegExp: RegExp = /^www\.audible\.[a-z]{2,3}(\.[a-z]{2,3})?$/i;
    private pageRegExp: RegExp = /^https:\/\/www\.audible\.[a-z]{2,3}(\.[a-z]{2,3})?\/cloudplayer\?asin=/i;

    public supportsHost(host: string, protocol: string): boolean {
        return this.hostRegExp.test(host) && protocol === "https:";
    }

    public createControl(win: Window): WebsiteControl {
        if (!this.pageRegExp.test(win.location.href)) {
            console.error("AudibleControl: unsupported page");
            return null; //page unsupported
        }
        const playButton = win.document.querySelector("img.adblPlayButton") as HTMLImageElement;
        const pauseButton = win.document.querySelector("img.adblPauseButton") as HTMLImageElement;
        const rewindButton = win.document.querySelector("img.adblFastRewind") as HTMLImageElement;
        const forwardButton = win.document.querySelector("img.adblFastForward") as HTMLImageElement;
        const timeSpent = win.document.getElementById("adblMediaBarTimeSpent");
        const timeLeft = win.document.getElementById("adblMediaBarTimeLeft");
        const chapterElem = win.document.getElementById("cp-Top-chapter-display");

        if (!(playButton instanceof HTMLImageElement) || !(pauseButton instanceof HTMLImageElement) ||
                !(rewindButton instanceof HTMLImageElement) || !(forwardButton instanceof HTMLImageElement) ||
                !(timeSpent instanceof HTMLElement) || !(timeLeft instanceof HTMLElement) ||
                !(chapterElem instanceof HTMLElement)) {
            console.error("AudibleControl: missing element");
            return null;
        }

        return new AudibleControl(playButton, pauseButton, rewindButton, forwardButton, timeSpent, timeLeft, chapterElem, win);
    }
}

// finally add this control to the registry
WebsiteControlRegistry.getInstance().register(new AudibleControlEntry());
