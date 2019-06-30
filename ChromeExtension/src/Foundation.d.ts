// TODO foundation types outdated
declare namespace Foundation {
    interface FoundationStatic {
        MediaQuery: MediaQuery;
    }

    interface MediaQuery {
        queries: Array<{name: string, value: string}>;
        current: "small" | "medium" | "large";
        atLeast(size: "small" | "medium" | "large"): boolean;
        is(size: "small only" | "medium only" | "large only" | "small" | "medium" | "large"): boolean;
        get(size: "small" | "medium" | "large"): String | null;
    }
}
declare var Foundation : Foundation.FoundationStatic;
