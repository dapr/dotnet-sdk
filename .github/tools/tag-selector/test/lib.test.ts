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
    test("stable latest minor has two patches; latest RC for next minor", () => {
        const tags = [
            "v1.16.7",
            "v1.16.8",
            "v1.17.0-rc.1",
            "v1.17.0-rc.3",
            // some older noise
            "v1.15.9",
            "junk-tag",
            "refs/tags/v1.14.2" // ensure we handle refs/tags/ normalization
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
            { version: "1.16.7", channel: "stable" },
        ]);
    });

    test("rc_count returns latest N RCs from newest RC minor", () => {
        const tags = [
            "v1.16.7",
            "v1.16.8",
            "v1.17.0-rc.1",
            "v1.17.0-rc.2",
            "v1.17.0-rc.3",
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
            { version: "1.16.7", channel: "stable" },
        ]);
    });

    test("rc_count returns available RCs when fewer exist", () => {
        const tags = [
            "v1.16.8",
            "v1.17.0-rc.1",
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

    test("no RC-only newest minor -> only stable outputs", () => {
        const tags = ["1.16.7", "1.16.8", "1.17.0"]; // 1.17 has a stable, so not RC-only
        const out = computeFromTags({ tags, stableCount: 2 });
        expect(out.matrix_json).toEqual([
            { version: "1.17.0", channel: "stable" },
        ]);
    });

    test("fewer than requested stableCount returns available", () => {
        const tags = ["v2.5.1"]; // only one stable in newest stable minor
        const out = computeFromTags({ tags, tagPrefix: "v", stableCount: 3 });
        expect(out.matrix_json).toEqual([{ version: "2.5.1", channel: "stable" }]);
    });

    test("no versions at all", () => {
        const out = computeFromTags({ tags: [], stableCount: 2 });
        expect(out).toEqual({
            matrix_json: [],
        });
    });
});
