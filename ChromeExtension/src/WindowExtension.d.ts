
interface Window {
    saveBookStorage(): { [key: string]: string };
    restoreBookStorage(stor: { [key: string]: string }, clear: boolean): void;
}
