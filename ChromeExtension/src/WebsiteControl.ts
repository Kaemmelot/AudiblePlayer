
export default interface WebsiteControl {
    play(): boolean;
    pause(): boolean;
    isPlaying(): boolean;

    getMaxPlaytime(): number;
    getCurrentPlaytime(): number;
    getRemainingPlayTime(): number;
    forward(time?: number): void;
    rewind(time?: number): void;

    setPort(port: chrome.runtime.Port | null): void;
    isReady(): boolean;
}
