
export function getUrlFromOfflineUrl(offlineUrl: string): string {
    return chrome.runtime.getURL("OfflinePlayer.html?file=" + offlineUrl.substr("offline://".length));
}

export function isOfflineUrl(url: string): boolean {
    return url.toLowerCase().startsWith("offline://");
}
