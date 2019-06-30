import { ChapterChangedEvent, CustomErrorEvent } from "./OfflinePlayerEvents";
import WebsiteControl from "./WebsiteControl";
import WebsiteControlRegistry, { WebsiteControlEntry } from "./WebsiteControlRegistry";

export default class OfflinePlayer implements WebsiteControl {
    private bgPort: chrome.runtime.Port | null = null;
    private isLoaded = false;

    constructor(private src: string, private file: string, private player: HTMLAudioElement, private win: Window) {
        // handler

        //this.player.addEventListener("abort", this.failed.bind(this));
        this.player.addEventListener("error", this.failed.bind(this));
        //this.player.addEventListener("loadstart", () => {this.isLoaded = false}); // this would trigger when jumping chapters
        this.player.addEventListener("ended", () => {
            console.log("Player finished");
            if (this.bgPort !== null)
                this.bgPort.postMessage("finished");
        });
        this.player.addEventListener("pause", () => {
            if (this.bgPort !== null)
                this.bgPort.postMessage("paused");
        });
        this.player.addEventListener("play", () => {
            if (this.bgPort !== null)
                this.bgPort.postMessage("playing");
        });
        this.player.addEventListener("canplay", this.loaded.bind(this));

        this.player.addEventListener("customError", (e) => {
            if (!(e instanceof CustomErrorEvent) || this.bgPort === null)
                return;
            this.bgPort.postMessage("log\nerror\n" + e.message);
        });
        this.player.addEventListener("chapterChanged", (e) => {
            if (!(e instanceof ChapterChangedEvent) || this.bgPort === null)
                return;
            this.bgPort.postMessage("log\ninfo\nChapter changed: " + e.chapter);
        });

        // start by loading file
        this.player.src = this.src;
        //this.player.load();

        // backup functions
        this.win.saveBookStorage = this.saveStorage.bind(this);
        this.win.restoreBookStorage = this.restoreStorage.bind(this);
    }

    private saveStorage(): { [key: string]: string } {
        return { ...localStorage };
    }

    private restoreStorage(stor: { [key: string]: string }, clear: boolean = true): void {
        if (clear)
            localStorage.clear();
        Object.keys(stor).forEach((key) => {
            localStorage.setItem(key, stor[key]);
        });
        console.log("LocalStorage restored");
    }

    private loaded(): void {
        if (this.isLoaded || this.bgPort === null)
            return;
        this.isLoaded = true;
        this.bgPort.postMessage("loaded\noffline://" + this.file);
        try {
            // Simulate audible player
            let chapter: Element | string = this.win.document.querySelector(".amplitude-active-song-container[data-chapter] .song-title");
            chapter = chapter && chapter.textContent;
            if (!chapter) {
                chapter = this.win.document.querySelector(".amplitude-active-song-container[data-chapter] .song-artist");
                chapter = chapter && chapter.textContent;
            }
            if (chapter)
                this.bgPort.postMessage("log\ninfo\nChapter changed: " + chapter);
        } catch (e) {}
    }

    private failed(): void {
        console.error("OfflinePlayer: Loading failed")
        //this.isLoaded = false; // abort is triggered on chapter jump
        if (this.bgPort !== null) {
            this.bgPort.postMessage("network");
            if (this.player.error !== null)
                this.bgPort.postMessage("log\nerror\nLoading failed. Reason: " + this.player.error.message + " (Code " + this.player.error.code + ")");
            else
                this.bgPort.postMessage("log\nerror\nLoading failed.");
        }
    }

    public play(): boolean {
        if (!this.isReady())
            return false;

        this.player.play();
        return true;
    }

    public pause(): boolean {
        if (!this.isReady())
            return false;

        this.player.pause();
        return true;
    }

    public isPlaying(): boolean {
        return this.isReady() && !this.player.paused;
    }

    public getMaxPlaytime(): number {
        return this.isReady() ? Math.round(this.player.duration) : 0;
    }

    public getCurrentPlaytime(): number {
        return this.isReady() ? Math.round(this.player.currentTime) : 0;
    }

    public getRemainingPlayTime(): number {
        return this.isReady() ? Math.round(this.player.duration - this.player.currentTime) : 0;
    }

    public forward(time?: number): void {
        if (!this.isReady())
            return;
        time = time || 30;
        this.player.currentTime += time;
    }

    public rewind(time?: number): void {
        if (!this.isReady())
            return;
        time = time || 30;
        this.player.currentTime -= time;
    }

    public setPort(port: chrome.runtime.Port): void {
        this.isLoaded = this.isLoaded && this.bgPort === port;
        this.bgPort = port || null;
        if (this.bgPort !== null && this.isReady() && !this.isLoaded) {
            this.isLoaded = true;
            this.bgPort.postMessage("loaded\noffline://" + this.file);
        }
    }

    public isReady(): boolean {
        return this.player.src !== "" && this.player.readyState >= HTMLMediaElement.HAVE_CURRENT_DATA; // enough to play current frame
    }
}

class OfflinePlayerEntry implements WebsiteControlEntry {
    private static hostRegExp = /^[a-z]{32}$/i;
    private static pageRegExp = /^chrome-extension:\/\/[a-z]{32}\/OfflinePlayer.html\?file=/i;
    private static hostPrefix = "http://127.0.0.1:8081/";

    public supportsHost(host: string, protocol: string): boolean {
        return OfflinePlayerEntry.hostRegExp.test(host) && protocol === "chrome-extension:";
    }

    public createControl(win: Window): WebsiteControl {
        if (!OfflinePlayerEntry.pageRegExp.test(win.location.href)) {
            console.error("OfflinePlayer: unsupported page");
            return null; //page unsupported
        }

        const query = location.search.substr(1).split("&").find((value: string, index: number, obj: string[]) => { return /^file=/i.test(value); });
        if (query === undefined) {
            console.error("OfflinePlayer: no file given");
            return null;
        }
        const file = query.substr(5);
        const src = OfflinePlayerEntry.hostPrefix + file;

        const player = win.document.getElementById("player") as HTMLAudioElement;

        if (!(player instanceof HTMLAudioElement)) {
            console.error("OfflinePlayer: missing audio element");
            return null;
        }

        return new OfflinePlayer(src, file, player, win);
    }
}

// finally add this control to the registry
WebsiteControlRegistry.getInstance().register(new OfflinePlayerEntry());
