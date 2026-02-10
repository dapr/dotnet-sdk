import semver from "semver";

export type ComputeInput = {
    tags: string[];         // raw tag names e.g., ["v1.16.8", "1.17.0-rc.3"]
    tagPrefix?: string;     // e.g., "v"
    stableCount?: number;   // default: 2
    rcCount?: number;       // default: 1
    rcIdent?: string;       // default: "rc"
}

export type ComputeOutput = {
    matrix_json: { version: string; channel: "rc" | "stable" }[];
}

export function stripPrefix(tag: string, prefix?: string): string {
    if (!prefix)
        return tag;
    
    return tag.startsWith(prefix) ? tag.slice(prefix.length) : tag;
}

function versionKey(major: number, minor: number) {
    return `${major}.${minor}`;
}

/** Compute outputs from a set of tag names */
export function computeFromTags(input: ComputeInput): ComputeOutput {
    const tagPrefix = input.tagPrefix ?? "";
    const stableCount = input.stableCount ?? 2;
    const rcCount = input.rcCount ?? 1;
    const rcIdent = input.rcIdent ?? "rc";

    // Normalize tags -> semver.valid versions
    const versions: string[] = [];
    for (const t of input.tags) {
        const raw = stripPrefix(t.replace(/^refs\/tags\//, ""), tagPrefix);
        const cleaned = semver.valid(raw);
        if (cleaned) versions.push(cleaned);
    }

    if (versions.length === 0) {
        return {
            matrix_json: [],
        };
    }

    const stable = versions.filter((v) => !semver.prerelease(v));
    const prerelease = versions.filter((v) => !!semver.prerelease(v));

    // Newest stable minor and its top N patch releases
    let stableMinor = "";
    let stablePatches: string[] = [];
    if (stable.length > 0) {
        const stableSorted = [...stable].sort(semver.rcompare);
        const newestStable = stableSorted[0];
        const sMajor = semver.major(newestStable);
        const sMinor = semver.minor(newestStable);
        stableMinor = `${sMajor}.${sMinor}`;
        stablePatches = stableSorted
            .filter((v) => semver.major(v) === sMajor && semver.minor(v) === sMinor)
            .slice(0, stableCount);
    }

    // Build minor map for RC-only minors
    const minorMap = new Map<
        string,
        { hasStable: boolean; rcVersions: string[]; major: number; minor: number }
    >();

    for (const v of stable) {
        const key = versionKey(semver.major(v), semver.minor(v));
        const entry =
            minorMap.get(key) || {
                hasStable: false,
                rcVersions: [],
                major: semver.major(v),
                minor: semver.minor(v),
            };
        entry.hasStable = true;
        minorMap.set(key, entry);
    }

    for (const v of prerelease) {
        const pr = semver.prerelease(v) || [];
        if (pr[0] === rcIdent) {
            const key = versionKey(semver.major(v), semver.minor(v));
            const entry =
                minorMap.get(key) || {
                    hasStable: false,
                    rcVersions: [],
                    major: semver.major(v),
                    minor: semver.minor(v),
                };
            entry.rcVersions.push(v);
            minorMap.set(key, entry);
        }
    }

    // Find newest (major.minor) with only RCs
    let latestRcs: string[] = [];
    const candidates = Array.from(minorMap.values()).filter(
        (m) => !m.hasStable && m.rcVersions.length > 0
    );
    if (rcCount > 0 && candidates.length > 0) {
        candidates.sort((a, b) =>
            a.major === b.major ? b.minor - a.minor : b.major - a.major
        );
        const newest = candidates[0];
        latestRcs = newest.rcVersions.sort(semver.rcompare).slice(0, rcCount);
    }

    const matrix = [
        ...latestRcs.map((v) => ({ version: v, channel: "rc" as const })),
        ...stablePatches.map((v) => ({ version: v, channel: "stable" as const })),
    ];

    return {
        matrix_json: matrix,
    };
}
