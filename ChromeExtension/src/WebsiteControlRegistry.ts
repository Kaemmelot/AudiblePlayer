import WebsiteControl from "./WebsiteControl";

export interface WebsiteControlEntry {
    supportsHost(host: string, protocol: string): boolean;
    createControl(win: Window): WebsiteControl;
}

export default class WebsiteControlRegistry {
    private constructor() { }

    private static instance: WebsiteControlRegistry | null = null;

    public static getInstance(): WebsiteControlRegistry {
        if (this.instance === null)
            this.instance = new WebsiteControlRegistry();
        return this.instance;
    }

    private controls: WebsiteControlEntry[] = [];

    public register(entry: WebsiteControlEntry): void {
        this.controls.push(entry);
    }

    public createControl(host: string, protocol: string, win: Window): WebsiteControl | null {
        for (const k in this.controls) {
            if (this.controls.hasOwnProperty(k) && this.controls[k].supportsHost(host, protocol))
                return this.controls[k].createControl(win);
        }
        return null;
    }
}
