import * as core from "@actions/core";
import * as github from "@actions/github";
import { computeFromTags } from "./lib";

async function run() {
    try {
        const token = core.getInput("github_token", { required: true});
        const tagPrefix = core.getInput("tag_prefix") || "";
        const stableCount = parseInt(core.getInput("stable_count") || "1", 10);
        const rcIdent = core.getInput("rc_identifier") || "rc";
        
        const ctx = github.context;
        const owner = core.getInput("owner") || cx.repo.owner;
        const repo = core.getInput("repo") || ctx.repo.repo;
        
        const octokit = github.getOctokit(token);
        
        // Paginate all tags
        const tags: Tag[] = await octokit.paginate(octokit.rest.repos.listTags, {
            owner,
            repo,
            per_page: 100
        });
        
        const tagNames = tags.map((t: { name: string }) => t.name);
        const result = computeFromTags({
            tags: tagNames,
            tagPrefix,
            stableCount,
            rcIdent,
        });

        core.setOutput("matrix_json", JSON.stringify(result.matrix_json));
    } catch (err: any) {
        core.setFailed(err?.message ?? String(err));
    }
}

run();