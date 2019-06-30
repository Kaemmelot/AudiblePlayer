// Version 3.9.0

declare module 'jsmediatags' {
    export function read(location: Object, callbacks: CallbackType): void;

    export class Reader {
        constructor(file: any);
        setTagsToRead(tagsToRead: Array<string>): Reader;
        setFileReader(fileReader: MediaFileReader): Reader;
        setTagReader(tagReader: MediaTagReader): Reader;
        read(callbacks: CallbackType);
    }

    export class Config {
        static addFileReader(fileReader: MediaFileReader): Config;
        static addTagReader(tagReader: MediaTagReader): Config;
        static removeTagReader(tagReader: MediaTagReader): Config;
        static EXPERIMENTAL_avoidHeadRequests(): void;
        static setDisallowedXhrHeaders(disallowedXhrHeaders: Array<string>);
        static setXhrTimeoutInSec(timeoutInSec: number);
    }

    ///
    /// Reader
    ///

    class MediaFileReader {
        constructor(path: any);
        static canReadFile(file: any): boolean;
        init(callbacks: LoadCallbackType): void;
        loadRange(range: [number, number], callbacks: LoadCallbackType): void;
        getSize(): number;
        getByteAt(offset: number): number;
        getBytesAt(offset: number, length: number): ByteArray;
        isBitSetAt(offset: number, bit: number): boolean;
        getSByteAt(offset: number): number;
        getShortAt(offset: number, isBigEndian: boolean): number;
        getSShortAt(offset: number, isBigEndian: boolean): number;
        getLongAt(offset: number, isBigEndian: boolean): number;
        getSLongAt(offset: number, isBigEndian: boolean): number;
        getInteger24At(offset: number, isBigEndian: boolean): number;
        getStringAt(offset: number, length: number): string;
        getStringWithCharsetAt(offset: number, length: number, charset?: CharsetType): DecodedString;
        getCharAt(offset: number): string;
        getSynchsafeInteger32At(offset: number): number;
    }

    class ArrayFileReader extends MediaFileReader {
        constructor(array: ByteArray);
        static canReadFile(file: any): boolean;
    }

    class BlobFileReader extends MediaFileReader {
        constructor(array: ByteArray);
        static canReadFile(file: any): boolean;
    }

    class XhrFileReader extends MediaFileReader {
        constructor(url: string);
        static canReadFile(file: any): boolean;
        static setConfig(config: Config): void;
    }

    class ReactNativeFileReader extends MediaFileReader {
        constructor(path: string);
        static canReadFile(file: any): boolean;
    }

    class NodeFileReader extends MediaFileReader {
        constructor(path: string);
        static canReadFile(file: any): boolean;
    }

    ///
    /// TagReader
    ///

    class MediaTagReader {
        constructor(mediaFileReader: MediaFileReader);
        static getTagIdentifierByteRange(): ByteRange;
        static canReadTagFormat(tagIdentifier: Array<number>): boolean;
        setTagsToRead(tags: Array<string>): MediaTagReader;
        read(callbacks: CallbackType);
        getShortcuts(): {[key: string]: (string|Array<string>)};
    }

    class FLACTagReader extends MediaTagReader {
        constructor();
        static getTagIdentifierByteRange(): ByteRange;
        static canReadTagFormat(tagIdentifier: Array<number>): boolean;
    }

    class ID3v1TagReader extends MediaTagReader {
        constructor();
        static getTagIdentifierByteRange(): ByteRange;
        static canReadTagFormat(tagIdentifier: Array<number>): boolean;
    }

    class ID3v2FrameReader {
        static getFrameReaderFunction(frameId: string): FrameReaderSignature | null;
        static readFrames(offset: number, end: number, data: MediaFileReader, id3header: TagHeader, tags: Array<string> | null): TagFrames;
        static getUnsyncFileReader(data: MediaFileReader, offset: number, size: number): MediaFileReader
    }

    class ID3v2TagReader extends MediaTagReader {
        static getTagIdentifierByteRange(): ByteRange;
        static canReadTagFormat(tagIdentifier: Array<number>): boolean;
        getShortcuts(): {[key: string]: string | Array<string>};
    }

    class MP4TagReader extends MediaTagReader {
        static getTagIdentifierByteRange(): ByteRange;
        static canReadTagFormat(tagIdentifier: Array<number>): boolean;
        getShortcuts(): {[key: string]: string | Array<string>};
    }

    ///
    /// needed types
    ///

    type ByteArray = Array<number>;

    type CharsetType =
        "utf-16" |
        "utf-16le" |
        "utf-16be" |
        "utf-8" |
        "iso-8859-1";
    
    type DataType = Array<number> | TypedArray | string;
    
    type TypedArray =
        Int8Array |
        Uint8Array |
        Uint8ClampedArray |
        Int16Array |
        Uint16Array |
        Int32Array |
        Uint32Array |
        Float32Array |
        Float64Array;
    
    type FrameReaderSignature = (
        offset: number,
        length: number,
        data: MediaFileReader,
        flags: Object | null,
        id3header?: TagHeader
        ) => any;

    type TagFrames = {[key: string]: TagFrame};

    class DecodedString {
        constructor(value: string, bytesReadCount: number);
        bytesReadCount: number;
        length: number;
        toString(): string;
    }

    class ChunkedFileData {
        constructor();
        addData(offset: number, data: DataType): void;
        hasDataRange(offsetStart: number, offsetEnd: number): boolean;
        getByteAt(offset: number): any;
    }

    interface TagFrame {
        id: string;
        size: number;
        description: string;
        data: any;
    }

    interface TagHeader {
        version: string;
        major: number;
        revision: number;
        flags: TagHeaderFlags;
        size: number;
    }

    interface TagHeaderFlags {
        unsynchronisation: boolean;
        extended_header: boolean;
        experimental_indicator: boolean;
        footer_present: boolean;
    }

    interface CallbackType {
        onSuccess: (data: any) => void;
        onError?: (error: ErrorObject) => void;
    }

    interface LoadCallbackType {
        onSuccess: () => void;
        onError?: (error: ErrorObject) => void;
    }

    interface ErrorObject {
        type: string;
        info: string;
        xhr?: XMLHttpRequest;
    }

    interface ByteRange {
        offset: number;
        length: number;
    }
}
