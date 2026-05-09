import { computeFromTags, stripPrefix } from "../src/lib";

describe("stripPrefix", () => {
    it("removes prefix when present", () => {
        expect(stripPrefix("v1.2.3", "v")).toBe("1.2.3");
    });
    it("keeps tag when prefix not present", () => {
        expect(stripPrefix("1.2.3", "v")).toBe("1.2.3");
    });
    it("noop when no prefix provided", () => {
        expect(stripPrefix("v1.2.3")).toBe("v1.2.3");
    });
});

describe("computeFromTags - core scenarios", () => {
    test("N-2 policy: RC-only newest minor + three stable minors (today's Dapr scenario)", () => {
        // 1.18 is RC-only; 1.17, 1.16, 1.15 each have stable patches
        const tags = [
            "v1.18.0-rc.1",
            "v1.18.0-rc.2",
            "v1.17.6",
            "v1.17.5",
            "v1.16.14",
            "v1.16.13",
            "v1.15.14",
            "v1.15.13",
        ];
        const out = computeFromTags({
            tags,
            tagPrefix: "v",
            stableCount: 3,
            rcCount: 1,
            rcIdent: "rc",
        });

        expect(out.matrix_json).toEqual([
            { version: "1.18.0-rc.2", channel: "rc" },
            { version: "1.17.6", channel: "stable" },
            { version: "1.16.14", channel: "stable" },
            { version: "1.15.14", channel: "stable" },
        ]);
    });

    test("highest minor has stable: no RC output, return stableCount distinct minors", () => {
        // 1.17 has a stable release, so its RC is ignored; return latest patch per minor
        const tags = [
            "v1.16.7",
            "v1.16.8",
            "v1.17.0",
            "v1.17.0-rc.1",
        ];
        const out = computeFromTags({
            tags,
            tagPrefix: "v",
            stableCount: 2,
            rcIdent: "rc",
        });

        expect(out.matrix_json).toEqual([
            { version: "1.17.0", channel: "stable" },
            { version: "1.16.8", channel: "stable" },
        ]);
    });

    test("RC-only newest minor: latest RC included, stable window shifts down", () => {
        const tags = [
            "v1.16.7",
            "v1.16.8",
            "v1.17.0-rc.1",
            "v1.17.0-rc.3",
            "v1.15.9",
            "junk-tag",
            "refs/tags/v1.14.2",
        ];
        const out = computeFromTags({
            tags,
            tagPrefix: "v",
            stableCount: 2,
            rcIdent: "rc",
        });

        expect(out.matrix_json).toEqual([
            { version: "1.17.0-rc.3", channel: "rc" },
            { version: "1.16.8", channel: "stable" },
            { version: "1.15.9", channel: "stable" },
        ]);
    });

    test("rc_count=2 returns latest 2 RCs from RC-only newest minor", () => {
        const tags = [
            "v1.17.0-rc.1",
            "v1.17.0-rc.2",
            "v1.17.0-rc.3",
            "v1.16.8",
            "v1.15.5",
        ];
        const out = computeFromTags({
            tags,
            tagPrefix: "v",
            stableCount: 2,
            rcCount: 2,
            rcIdent: "rc",
        });

        expect(out.matrix_json).toEqual([
            { version: "1.17.0-rc.3", channel: "rc" },
            { version: "1.17.0-rc.2", channel: "rc" },
            { version: "1.16.8", channel: "stable" },
            { version: "1.15.5", channel: "stable" },
        ]);
    });

    test("rc_count returns available RCs when fewer than rcCount exist in RC-only minor", () => {
        const tags = [
            "v1.17.0-rc.1",
            "v1.16.8",
        ];
        const out = computeFromTags({
            tags,
            tagPrefix: "v",
            stableCount: 1,
            rcCount: 2,
            rcIdent: "rc",
        });

        expect(out.matrix_json).toEqual([
            { version: "1.17.0-rc.1", channel: "rc" },
            { version: "1.16.8", channel: "stable" },
        ]);
    });

    test("highest minor has stable: no RC output even when prerelease versions exist for that minor", () => {
        // 1.18.0 is stable, so 1.18.0-rc.1 and 1.18.1-rc.1 are irrelevant; 1.17 RCs are older minor, also ignored
        const tags = [
            "1.18.0",
            "1.18.0-rc.1",
            "1.18.1-rc.1",
            "1.17.0-rc.2",
            "1.17.0-rc.1",
        ];
        const out = computeFromTags({
            tags,
            stableCount: 1,
            rcCount: 2,
            rcIdent: "rc",
        });

        expect(out.matrix_json).toEqual([
            { version: "1.18.0", channel: "stable" },
        ]);
    });

    test("each stable minor is represented by its single latest patch, not multiple patches", () => {
        // 1.17 has stable patches; 1.17.2-rc.1 and 1.16.9-rc.1 are patch RCs within stable minors — ignored
        const tags = [
            "v1.17.0",
            "v1.17.1",
            "v1.17.2-rc.1",
            "v1.16.8",
            "v1.16.9-rc.1",
        ];
        const out = computeFromTags({
            tags,
            tagPrefix: "v",
            stableCount: 2,
            rcCount: 1,
            rcIdent: "rc",
        });

        expect(out.matrix_json).toEqual([
            { version: "1.17.1", channel: "stable" },
            { version: "1.16.8", channel: "stable" },
        ]);
    });

    test("stable gap: skips minors with no stable release when scanning backwards", () => {
        // 1.16 has no stable releases; window skips it and picks 1.15
        const tags = [
            "v1.17.0",
            "v1.16.0-rc.1",
            "v1.15.3",
            "v1.14.9",
        ];
        const out = computeFromTags({
            tags,
            tagPrefix: "v",
            stableCount: 3,
            rcIdent: "rc",
        });

        expect(out.matrix_json).toEqual([
            { version: "1.17.0", channel: "stable" },
            { version: "1.15.3", channel: "stable" },
            { version: "1.14.9", channel: "stable" },
        ]);
    });

    test("fewer stables than stableCount returns only what is available", () => {
        const tags = ["v2.5.1"];
        const out = computeFromTags({ tags, tagPrefix: "v", stableCount: 3 });
        expect(out.matrix_json).toEqual([{ version: "2.5.1", channel: "stable" }]);
    });

    test("no versions at all", () => {
        const out = computeFromTags({ tags: [], stableCount: 2 });
        expect(out).toEqual({ matrix_json: [] });
    });
});
