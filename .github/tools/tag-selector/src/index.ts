import * as core from "@actions/core";
import * as github from "@actions/github";
import { computeFromTags } from "./lib";

async function run() {
    try {
        const token = core.getInput("github_token", { required: true});
        const tagPrefix = core.getInput("tag_prefix") || "";
        const stableCount = parseInt(core.getInput("stable_count") || "1", 10);
        const rcCount = parseInt(core.getInput("rc_count") || "1", 10);
        const rcIdent = core.getInput("rc_identifier") || "rc";

        const owner = core.getInput("owner") || "dapr";
        const repo = core.getInput("repo") || "dapr";

        const octokit = github.getOctokit(token);

        // Paginate releases so prerelease releases are excluded from stable selection
        // even when their tag name does not include a prerelease suffix.
        const releases = await octokit.paginate(octokit.rest.repos.listReleases, {
            owner,
            repo,
            per_page: 100
        });

        const releaseTags = releases
            .filter((release) => !release.draft)
            .map((release) => ({
                name: release.tag_name,
                prerelease: release.prerelease,
            }));

        const result = computeFromTags({
            tags: releaseTags.map((tag) => tag.name),
            prereleaseTags: releaseTags.filter((tag) => tag.prerelease).map((tag) => tag.name),
            tagPrefix,
            stableCount,
            rcCount,
            rcIdent,
        });

        core.setOutput("matrix_json", JSON.stringify(result.matrix_json));
    } catch (err: any) {
        core.setFailed(err?.message ?? String(err));
    }
}

run();
