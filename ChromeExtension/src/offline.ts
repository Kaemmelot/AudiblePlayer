import "foundation";
//import * as Amplitude from "Amplitude";
import * as jsmediatags from "jsmediatags";
import * as jQuery from "jquery";
import { CustomErrorEvent, ChapterChangedEvent } from "./OfflinePlayerEvents";

const volumeStorageKey = "OfflinePlayerVolume";
const fileStorageKeyPrefix = "OfflinePlayerProgress-";
const skipBackTime = 1.5 * 60;
let minSkipTime = 0;

if (document.readyState === "complete")
    loaded();
else
    window.addEventListener("load", loaded);

function loaded() {
    jQuery(document).foundation();

    // Example content:
    adjustPlayerHeights();
    window.addEventListener("resize", adjustPlayerHeights);

    /*
        Show and hide the play button container on the song when the song is clicked.
    */
    document.addEventListener("click", (e) => {
        if (e.target instanceof HTMLElement && e.target.classList.contains("song"))
            e.target.querySelector<HTMLElement>(".play-button-container").style.display = "none";
    });

    initializePlayer();
}

/*
    Adjusts the height of the left and right side of the players to be the same.
*/
function adjustPlayerHeights() {
    if (Foundation.MediaQuery.atLeast('medium')) {
        const left = document.getElementById("amplitude-left").clientWidth; // square :)
        document.getElementById("amplitude-right").style.height = left + "px";
    } else {
        document.getElementById("amplitude-right").style.height = "";
    }
}

let player: HTMLAudioElement;
let currentHours: HTMLSpanElement;
let currentMinutes: HTMLSpanElement;
let currentSeconds: HTMLSpanElement;
let durationHours: HTMLSpanElement;
let durationMinutes: HTMLSpanElement;
let durationSeconds: HTMLSpanElement;
let progressRange: HTMLInputElement;
let progressBg: HTMLProgressElement;
let playPause: HTMLDivElement;
let prev: HTMLDivElement;
let next: HTMLDivElement;
let volume: HTMLInputElement;
let muted: HTMLDivElement;
let cover: HTMLImageElement;
let fileName: HTMLSpanElement;
let fileDesc: HTMLDivElement;
let chapterList: HTMLDivElement;

let file: string | null = null;
let updateRunning = false;
let progressUserMovement = false;
let chapters: Array<{name: string, detailedName?: string,
        start: number, duration: number}> = [];
let playerReadyState = 0;
let currentChapter = -1;

const INIT_STATE = 1;
const PLAYER_READY_STATE = 2;
const METADATA_READY_STATE = 4;
const CHAPTERS_READY_STATE = 8;


function getElements(): boolean {
    player = document.getElementById("player") as HTMLAudioElement;
    const currentTime = document.getElementById("current-time") as HTMLSpanElement;
    currentHours = currentTime.querySelector<HTMLSpanElement>("span.amplitude-current-hours");
    currentMinutes = currentTime.querySelector<HTMLSpanElement>("span.amplitude-current-minutes");
    currentSeconds = currentTime.querySelector<HTMLSpanElement>("span.amplitude-current-seconds");
    const duration = document.getElementById("duration") as HTMLSpanElement;
    durationHours = duration.querySelector<HTMLSpanElement>("span.amplitude-duration-hours");
    durationMinutes = duration.querySelector<HTMLSpanElement>("span.amplitude-duration-minutes");
    durationSeconds = duration.querySelector<HTMLSpanElement>("span.amplitude-duration-seconds");
    const progress = document.getElementById("progress-container") as HTMLDivElement;
    progressRange = progress.querySelector<HTMLInputElement>("input.amplitude-song-slider");
    progressBg = document.getElementById("song-played-progress") as HTMLProgressElement;
    playPause = document.getElementById("play-pause") as HTMLDivElement;
    prev = document.getElementById("previous") as HTMLDivElement;
    next = document.getElementById("next") as HTMLDivElement;
    const volumeCont = document.getElementById("volume-container") as HTMLDivElement;
    volume = volumeCont.querySelector<HTMLInputElement>("input.amplitude-volume-slider");
    muted = volumeCont.querySelector<HTMLDivElement>("div.amplitude-mute");
    cover = document.getElementById("cover-art") as HTMLImageElement;
    const metaCont = document.getElementById("meta-container") as HTMLDivElement;
    fileName = metaCont.querySelector<HTMLSpanElement>("span.file-name");
    fileDesc = metaCont.querySelector<HTMLDivElement>("div.file-desc");
    chapterList = document.getElementById("amplitude-right") as HTMLDivElement;

    return (player instanceof HTMLAudioElement) && (currentHours instanceof HTMLSpanElement) && (currentMinutes instanceof HTMLSpanElement)
    && (currentSeconds instanceof HTMLSpanElement) && (durationHours instanceof HTMLSpanElement) && (durationMinutes instanceof HTMLSpanElement)
    && (durationSeconds instanceof HTMLSpanElement) && (progressRange instanceof HTMLInputElement) && (progressBg instanceof HTMLProgressElement)
    && (playPause instanceof HTMLDivElement) && (prev instanceof HTMLDivElement) && (next instanceof HTMLDivElement)
    && (volume instanceof HTMLInputElement) && (muted instanceof HTMLDivElement) && (cover instanceof HTMLImageElement)
    && (fileName instanceof HTMLSpanElement) && (fileDesc instanceof HTMLDivElement) && (chapterList instanceof HTMLDivElement)
}

async function initializePlayer(): Promise<void> {
    playerReadyState = 0;
    if (!getElements()) {
        console.error("Some elements could not be found in DOM for the OfflinePlayer");
        const err = new CustomErrorEvent("customError", {bubbles: false, cancelable: true});
        err.message = "Some elements could not be found in DOM for the OfflinePlayer";
        player.dispatchEvent(err);
        return;
    }
    console.info("Elements loaded, preparing player and file");

    // Defaults
    player.autoplay = false;
    player.loop = false;
    player.playbackRate = 1.0;
    player.defaultMuted = false;
    progressRange.min = "0";
    progressRange.value = "0";
    progressBg.value = 0;
    volume.min = "0";
    volume.max = "1";
    volume.step = "0.05";
    volume.value = "1";
    player.muted = false;
    // Storage or default
    player.volume = parseFloat(localStorage.getItem(volumeStorageKey) || "1");

    if (player.src !== "") {
        file = player.src;
        // Load metadata
        initMetadata();
    }
    updateUi();

    // Link audio and elements
    // UI updates
    player.addEventListener("abort", loadFailed);
    player.addEventListener("error", loadFailed);
    player.addEventListener("loadeddata", updateUi);
    player.addEventListener("loadstart", () => {
        file = player.src;
        // Load metadata
        initMetadata();
        updateUi();
    });
    player.addEventListener("emptied", () => {
        file = null;
        // TODO remove metadata
        updateUi();
    });
    player.addEventListener("durationchange", updateUi);
    player.addEventListener("pause", updateUiPlaying);
    player.addEventListener("ended", endedHandler);
    player.addEventListener("play", updateUiPlaying);
    player.addEventListener("timeupdate", updateUiTime);
    player.addEventListener("volumechange", updateUiVolume);
    // audio updates
    playPause.addEventListener("click", playPauseHandler);
    prev.addEventListener("click", prevChapterHandler);
    next.addEventListener("click", nextChapterHandler);
    let progressMouseDown = false;
    progressRange.addEventListener("mousedown", () => progressMouseDown = true);
    progressRange.addEventListener("mouseup", () => progressUserMovement = (progressMouseDown = false));
    progressRange.addEventListener("mousemove", () => progressUserMovement = progressMouseDown);
    progressRange.addEventListener("change", progressHandler);
    volume.addEventListener("change", volumeHandler);
    muted.addEventListener("click", mutedHandler);

    playerReadyState |= INIT_STATE;
    console.info("OfflinePlayer initialized");
}

function endedHandler(): void {
    // reset
    minSkipTime = 0;
    player.currentTime = 0;
    updateUiTime();
}

function isPlayerReady(): boolean {
    return player.src !== "" && player.readyState >= HTMLMediaElement.HAVE_METADATA;
}

function updateUi(e: Event = null, updateChapters = false): void {
    if (updateRunning)
        return;
    updateRunning = true;

    if (isPlayerReady()) {
        // source ready enough
        // load progress
        player.currentTime = (minSkipTime = parseFloat(localStorage.getItem(fileStorageKeyPrefix + file) || "0"));
        timeHelper(player.currentTime, currentHours, currentMinutes, currentSeconds);
        timeHelper(player.duration, durationHours, durationMinutes, durationSeconds);
        progressRange.max = (progressBg.max = player.duration) + "";
        progressRange.step = Math.ceil(player.duration / 1500) + ""; // 1500 steps
        progressRange.value = (progressBg.value = player.currentTime) + "";
        playPause.classList.remove(player.paused ? "amplitude-playing" : "amplitude-paused");
        playPause.classList.add(player.paused ? "amplitude-paused" : "amplitude-playing");
        volume.value = player.volume + "";
        muted.classList.remove(player.muted ? "amplitude-not-muted" : "amplitude-muted");
        muted.classList.add(player.muted ? "amplitude-muted" : "amplitude-not-muted");
        playerReadyState |= PLAYER_READY_STATE;
    } else {
        // no source loaded yet
        timeHelper(0, currentHours, currentMinutes, currentSeconds);
        timeHelper(0, durationHours, durationMinutes, durationSeconds);
        progressRange.max = (progressBg.max = 1) + "";
        progressRange.step = "1";
        progressRange.value = (progressBg.value = 0) + "";
        playPause.classList.remove("amplitude-playing");
        playPause.classList.add("amplitude-paused");
        volume.value = player.volume + "";
        muted.classList.remove(player.muted ? "amplitude-not-muted" : "amplitude-muted");
        muted.classList.add(player.muted ? "amplitude-muted" : "amplitude-not-muted");
    }

    if ((playerReadyState & METADATA_READY_STATE) && updateChapters) {
        // get a clear list
        let oldList = chapterList;
        chapterList = chapterList.cloneNode(false) as HTMLDivElement;

        // add chapters
        for (let c = 0; c < chapters.length; c++) {
            let container = document.createElement("DIV");
            container.classList.add("song", "amplitude-song-container");
            container.setAttribute("data-chapter", c + "");
            let nowPlayingContainer = document.createElement("DIV");
            nowPlayingContainer.classList.add("song-now-playing-icon-container");
            let playButton = document.createElement("DIV");
            playButton.classList.add("play-button-container");
            nowPlayingContainer.appendChild(playButton);
            let playing = document.createElement("DIV");
            playing.classList.add("now-playing");
            nowPlayingContainer.appendChild(playing);
            container.appendChild(nowPlayingContainer);
            let metaData = document.createElement("DIV");
            metaData.classList.add("song-meta-data");
            let detailedName = document.createElement("SPAN");
            detailedName.classList.add("song-title");
            detailedName.textContent = chapters[c].detailedName || "";
            metaData.appendChild(detailedName);
            let name = document.createElement("SPAN");
            name.classList.add("song-artist");
            name.textContent = chapters[c].name || "";
            metaData.appendChild(name);
            container.appendChild(metaData);
            let duration = document.createElement("SPAN");
            duration.classList.add("song-duration");
            duration.textContent = typeof chapters[c].duration === "number" ? getTimeString(chapters[c].duration) : "";
            container.appendChild(duration);
            let pos = document.createElement("SPAN");
            pos.classList.add("song-position");
            pos.textContent = typeof chapters[c].start === "number" ? getTimeString(chapters[c].start) : "";
            container.appendChild(pos);
            chapterList.appendChild(container);
        }

        // replace old with new
        oldList.parentNode.replaceChild(chapterList, oldList);
        // chapter handler
        chapterList.addEventListener("click", chapterClick);
        currentChapter = -1;

        playerReadyState |= CHAPTERS_READY_STATE;
    }

    if (isPlayerReady() && (playerReadyState & CHAPTERS_READY_STATE)) {
        let chapterDiv: HTMLDivElement;
        if (currentChapter !== -1) {
            chapterDiv = chapterList.querySelector("[data-chapter='" + currentChapter + "'") as HTMLDivElement;
            if (chapterDiv)
                chapterDiv.classList.remove("amplitude-active-song-container");
        }

        // show current chapter
        currentChapter = 0;
        let c = 0;
        while (c < chapters.length && chapters[c].start <= player.currentTime)
            currentChapter = c++;

        chapterDiv = chapterList.querySelector("[data-chapter='" + currentChapter + "'") as HTMLDivElement;
        if (chapterDiv)
            chapterDiv.classList.add("amplitude-active-song-container")
    }

    updateRunning = false;
}

function loadFailed(): void {
    if (player.error !== null)
        console.error("File loading failed", player.error);
    else
        console.error("File loading failed");
    if (!player.paused)
        player.pause();
}

function getTimeString(time: number): string {
    let result = Math.floor(time % 60) + ""; // seconds
    if (result.length < 2)
        result = "0" + result;
    time = Math.floor(time / 60);
    result = (time % 60) + ":" + result; // minutes
    if (result.length < 5)
        result = "0" + result;
    time = Math.floor(time / 60);
    if (time === 0)
        return result;
    result = time + ":" + result; // hours
    return result;
}

function timeHelper(time: number, hours: HTMLElement, minutes: HTMLElement, seconds: HTMLElement): void {
    hours.textContent = (time / 3600 < 10 ? "0" : "") + Math.floor(time / 3600) + "";
    time %= 3600;
    minutes.textContent = (time / 60 < 10 ? "0" : "") + Math.floor(time / 60) + "";
    time %= 60;
    seconds.textContent = (time < 10 ? "0" : "") + Math.floor(time) + "";
}

function updateUiPlaying(): void {
    if (updateRunning)
        return;
    updateRunning = true;

    playPause.classList.remove(player.paused ? "amplitude-playing" : "amplitude-paused");
    playPause.classList.add(player.paused ? "amplitude-paused" : "amplitude-playing");

    updateRunning = false;
}

function updateUiTime(): void {
    if (updateRunning)
        return;
    updateRunning = true;

    timeHelper(player.currentTime, currentHours, currentMinutes, currentSeconds);
    updateProgressBar();
    
    if (playerReadyState & CHAPTERS_READY_STATE) {
        let c = currentChapter === -1 || chapters[currentChapter].start > player.currentTime ? 0 : currentChapter; // if we didn't skip back: search from currentChapter
        while (++c < chapters.length && chapters[c].start <= player.currentTime)
            ;
        
        if (c - 1 !== currentChapter)
            changeChapter(c - 1);
    }
    
    localStorage.setItem(fileStorageKeyPrefix + file, Math.max(minSkipTime, player.currentTime - skipBackTime) + ""); // skip some time back

    updateRunning = false;
}

function updateUiVolume(): void {
    if (updateRunning)
        return;
    updateRunning = true;

    volume.value = player.volume + "";
    muted.classList.remove(player.muted ? "amplitude-not-muted" : "amplitude-muted");
    muted.classList.add(player.muted ? "amplitude-muted" : "amplitude-not-muted");
    localStorage.setItem(volumeStorageKey, player.volume + "");

    updateRunning = false;
}

function playPauseHandler(): void {
    if (updateRunning || !isPlayerReady())
        return;
    updateRunning = true;

    if (player.paused)
        player.play();
    else
        player.pause();
    playPause.classList.remove(player.paused ? "amplitude-playing" : "amplitude-paused");
    playPause.classList.add(player.paused ? "amplitude-paused" : "amplitude-playing");

    updateRunning = false;
}

function changeChapter(ch: number): void {
    let chapterDiv: HTMLDivElement;
    if (currentChapter !== -1) {
        chapterDiv = chapterList.querySelector("[data-chapter='" + currentChapter + "'") as HTMLDivElement;
        if (chapterDiv)
            chapterDiv.classList.remove("amplitude-active-song-container");
    }

    currentChapter = ch;

    chapterDiv = chapterList.querySelector("[data-chapter='" + currentChapter + "'") as HTMLDivElement;
    if (chapterDiv)
        chapterDiv.classList.add("amplitude-active-song-container")

    const event = new ChapterChangedEvent("chapterChanged", {bubbles: false, cancelable: false});
    event.chapter = chapters[currentChapter].detailedName || chapters[currentChapter].name;
    player.dispatchEvent(event);
}

function moveToChapter(ch: number): void {
    player.currentTime = (minSkipTime = chapters[ch].start);
    updateProgressBar();
    timeHelper(player.currentTime, currentHours, currentMinutes, currentSeconds);
    localStorage.setItem(fileStorageKeyPrefix + file, minSkipTime + "");
    changeChapter(ch);
}

function prevChapterHandler(): void {
    if (updateRunning || !isPlayerReady() || (playerReadyState & CHAPTERS_READY_STATE) === 0)
        return;
    updateRunning = true;

    moveToChapter(Math.max(currentChapter - 1, 0));

    updateRunning = false;
}

function nextChapterHandler(): void {
    if (updateRunning || !isPlayerReady() || (playerReadyState & CHAPTERS_READY_STATE) === 0)
        return;
    updateRunning = true;

    if (currentChapter < chapters.length - 1)
        moveToChapter(currentChapter + 1);

    updateRunning = false;
}

function chapterClick(e: MouseEvent): void {
    if (updateRunning || !isPlayerReady() || (playerReadyState & CHAPTERS_READY_STATE) === 0
            || !(e.target instanceof HTMLElement) || !e.target.classList.contains("song") || isNaN(parseInt(e.target.getAttribute("data-chapter"))))
        return;
    updateRunning = true;

    moveToChapter(parseInt(e.target.getAttribute("data-chapter")));

    updateRunning = false;
}

function updateProgressBar(): void {
    if (!progressUserMovement) // prevent this during user input
        progressRange.value = (progressBg.value = player.currentTime) + "";
}

function progressHandler(): void {
    if (updateRunning || !isPlayerReady())
        return;
    updateRunning = true;

    minSkipTime = 0;

    player.currentTime = (progressBg.value = Math.max(0, Math.min(player.duration, parseInt(progressRange.value))));
    if (playerReadyState & CHAPTERS_READY_STATE) {
        let c = currentChapter === -1 || chapters[currentChapter].start > player.currentTime ? 0 : currentChapter; // if we didn't skip back: search from currentChapter
        while (++c < chapters.length && chapters[c].start <= player.currentTime)
            ;
        
        if (c - 1 !== currentChapter)
            changeChapter(c - 1);
    }
    localStorage.setItem(fileStorageKeyPrefix + file, Math.max(minSkipTime, player.currentTime - skipBackTime) + ""); // skip some time back

    updateRunning = false;
}

function volumeHandler(): void {
    if (updateRunning)
        return;
    updateRunning = true;

    player.volume = Math.max(0, Math.min(1, parseFloat(volume.value)));
    localStorage.setItem(volumeStorageKey, player.volume + "");

    updateRunning = false;
}

function mutedHandler(): void {
    if (updateRunning)
        return;
    updateRunning = true;

    player.muted = !player.muted;
    muted.classList.remove(player.muted ? "amplitude-not-muted" : "amplitude-muted");
    muted.classList.add(player.muted ? "amplitude-muted" : "amplitude-not-muted");

    updateRunning = false;
}

function getDataUrl(format: string, binaryData: Uint8Array): string {
    return "data:" + format + ";base64," +
        btoa(binaryData.reduce((str, charIndex) => {
            return str += String.fromCharCode(charIndex);
        }, ''));
}

function decodeUTF8(str: string): string {
    return decodeURIComponent(escape(str));
}

async function initMetadata(): Promise<void> {
    new jsmediatags.Reader(file)
        .setTagsToRead(["CHAP", "TXXX", "APIC"]) // chapter, json, cover 
        .read({
            onSuccess: (id3) => {
                console.info("ID3 Tags loaded");
                //console.log(id3);

                cover.src = getDataUrl(id3.tags.picture.format, id3.tags.picture.data);
                cover.style.height = "1px"; // TODO this doesn't work when window is minimized
                cover.style.height = "";

                let json = null;
                // search for json entry
                for (let i = 0; i < id3.tags.TXXX.length; i++) {
                    if (id3.tags.TXXX[i].data.user_description === "json64")
                        json = JSON.parse(atob(id3.tags.TXXX[i].data.data));
                }
                if (json === null) {
                    console.error("No json found");
                    const err = new CustomErrorEvent("customError", {bubbles: false, cancelable: true});
                    err.message = "No json found";
                    player.dispatchEvent(err);
                    // goon
                }
                for (let key in json) {
                    // decode
                    if (json.hasOwnProperty(key) && typeof json[key] === "string")
                        json[key] = decodeUTF8(json[key]);
                }

                fileName.textContent = json.title;
                fileDesc.innerHTML = json.summary;

                chapters = id3.tags.CHAP.map((value) => {
                    return {name: decodeUTF8(value.data.subFrames.TIT2.data), start: value.data.startTime / 1000, duration: Math.round(value.data.endTime / 1000 - value.data.startTime / 1000)}
                });
                // TODO load additional chapter information from file


                // TODO Amplitude is nice, but cannot handle large files

                // update
                playerReadyState |= METADATA_READY_STATE;
                adjustPlayerHeights();
                updateUi(null, true);
            },
            onError: (error) => {
                console.error("Failed to load file tags", error.type, error.info);
                const err = new CustomErrorEvent("customError", {bubbles: false, cancelable: true});
                err.message = "Failed to load file tags";
                player.dispatchEvent(err);
            }
        });
}
