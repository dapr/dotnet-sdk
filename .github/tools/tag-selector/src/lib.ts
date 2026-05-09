import semver from "semver";

export type ComputeInput = {
    tags: string[];         // raw tag names e.g., ["v1.16.8", "1.17.0-rc.3"]
    tagPrefix?: string;     // e.g., "v"
    stableCount?: number;   // number of distinct stable minor versions to include; default: 2
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

/**
 * Compute outputs from a set of tag names using an N-2 support policy:
 *
 * 1. Find the highest minor version across all tags.
 * 2. If that minor is RC-only (no stable release exists for it), include up to
 *    rcCount of its latest RC versions and shift the stable window down by one.
 * 3. Collect the latest stable patch for each of stableCount distinct minor
 *    versions, starting from the minor just below any RC-only newest minor (or
 *    from the highest stable minor when there is no RC phase), and scanning
 *    downwards — skipping gaps where a minor has no stable release.
 */
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
        return { matrix_json: [] };
    }

    const stable = versions.filter((v) => !semver.prerelease(v));
    const prerelease = versions.filter((v) => !!semver.prerelease(v));

    // Highest overall version determines whether we're in an RC phase
    const allSorted = [...versions].sort(semver.rcompare);
    const highestOverall = allSorted[0];
    const highestMajor = semver.major(highestOverall);
    const highestMinor = semver.minor(highestOverall);

    const highestMinorHasStable = stable.some(
        (v) => semver.major(v) === highestMajor && semver.minor(v) === highestMinor
    );

    // If the highest minor is RC-only, collect up to rcCount RCs from it
    let latestRcs: string[] = [];
    let stableBaseMinor = highestMinor;

    if (!highestMinorHasStable && rcCount > 0) {
        const stablePatchSet = new Set(
            stable.map((v) => `${semver.major(v)}.${semver.minor(v)}.${semver.patch(v)}`)
        );
        const rcCandidates = prerelease.filter((v) => {
            const pr = semver.prerelease(v) || [];
            return (
                semver.major(v) === highestMajor &&
                semver.minor(v) === highestMinor &&
                pr[0] === rcIdent &&
                !stablePatchSet.has(`${semver.major(v)}.${semver.minor(v)}.${semver.patch(v)}`)
            );
        });
        latestRcs = [...rcCandidates].sort(semver.rcompare).slice(0, rcCount);
        // Stable window starts one minor below the RC-only minor
        stableBaseMinor = highestMinor - 1;
    }

    // Collect the latest stable patch for each of stableCount distinct minor
    // versions, scanning downwards from stableBaseMinor
    const stablePatches: string[] = [];
    if (stable.length > 0) {
        const stableSorted = [...stable].sort(semver.rcompare);
        const stableMajor = semver.major(stableSorted[0]);
        let minor = stableBaseMinor;
        while (stablePatches.length < stableCount && minor >= 0) {
            const best = stableSorted.find(
                (v) => semver.major(v) === stableMajor && semver.minor(v) === minor
            );
            if (best) stablePatches.push(best);
            minor--;
        }
    }

    const matrix = [
        ...latestRcs.map((v) => ({ version: v, channel: "rc" as const })),
        ...stablePatches.map((v) => ({ version: v, channel: "stable" as const })),
    ];

    return { matrix_json: matrix };
}
