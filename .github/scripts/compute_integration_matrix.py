# Copyright 2026 The Dapr Authors
# Licensed under the Apache License, Version 2.0.

import json
import os


FRAMEWORKS = ("net8.0", "net9.0", "net10.0", "net11.0")
MAX_MATRIX_JOBS = 256


def compute_matrices(projects, dapr_versions):
    for name, entries, fields in (
        ("projects", projects, ("project", "projectName")),
        ("Dapr versions", dapr_versions, ("version", "channel")),
    ):
        if not isinstance(entries, list) or not entries:
            raise ValueError(f"{name} must be a non-empty array")
        for entry in entries:
            if not isinstance(entry, dict) or any(
                not isinstance(entry.get(field), str) or not entry[field].strip()
                for field in fields
            ):
                raise ValueError(f"Invalid {name} entry: {entry!r}")

    job_count = len(projects) * len(dapr_versions)
    if job_count > MAX_MATRIX_JOBS:
        raise ValueError(
            f"Each framework would generate {job_count} jobs "
            f"({len(projects)} projects x {len(dapr_versions)} Dapr versions), "
            f"exceeding GitHub Actions' {MAX_MATRIX_JOBS}-job matrix limit. "
            "Split the projects into additional jobs before expanding the matrix."
        )

    matrices = {}
    for framework in FRAMEWORKS:
        version = framework.removeprefix("net")
        prefix = framework.split(".")[0]
        matrices[prefix] = {
            "include": [
                {
                    "project": project["project"],
                    "projectName": project["projectName"],
                    "dotnet-version": version,
                    "display-name": f".NET {version}",
                    "framework": framework,
                    "prefix": prefix,
                    "install-version": f"{version}.x",
                    "upload_coverage": framework == "net10.0",
                    "os": "ubuntu-latest",
                    "dapr-runtime-versions": dapr_version,
                }
                for project in projects
                for dapr_version in dapr_versions
            ]
        }
    return matrices


def main():
    matrices = compute_matrices(
        json.loads(os.environ["PROJECTS_JSON"]),
        json.loads(os.environ["DAPR_VERSIONS_JSON"]),
    )
    with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as output:
        for prefix, matrix in matrices.items():
            print(f"{prefix}: {len(matrix['include'])} integration jobs")
            output.write(f"{prefix}={json.dumps(matrix, separators=(',', ':'))}\n")


if __name__ == "__main__":
    main()
