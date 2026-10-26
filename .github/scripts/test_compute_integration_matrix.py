# Copyright 2026 The Dapr Authors
# Licensed under the Apache License, Version 2.0.

import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

from compute_integration_matrix import FRAMEWORKS, compute_matrices


class IntegrationMatrixTests(unittest.TestCase):
    def setUp(self):
        self.projects = [
            {
                "project": f"test/Dapr.IntegrationTest.Project{i}/Dapr.IntegrationTest.Project{i}.csproj",
                "projectName": f"Project{i}",
            }
            for i in range(19)
        ]
        self.versions = [
            {"version": "edge", "channel": "edge"},
            {"version": "1.19.0-rc.7", "channel": "rc"},
            {"version": "1.18.9", "channel": "stable"},
            {"version": "1.17.18", "channel": "stable"},
            {"version": "1.16.21", "channel": "stable"},
        ]

    def test_release_matrix_is_split_without_losing_combinations(self):
        # Run 39701042612 attempted 19 x 3 x 5 = 285 jobs in one stable matrix.
        matrices = compute_matrices(self.projects, self.versions)
        self.assertEqual(set(matrices), {"net8", "net9", "net10", "net11"})
        for framework in FRAMEWORKS:
            prefix = framework.split(".")[0]
            rows = matrices[prefix]["include"]
            self.assertEqual(len(rows), 95)
            self.assertEqual(
                {(row["project"], row["dapr-runtime-versions"]["version"]) for row in rows},
                {(p["project"], v["version"]) for p in self.projects for v in self.versions},
            )
            for row in rows:
                self.assertEqual(row["framework"], framework)
                self.assertEqual(row["prefix"], prefix)
                self.assertEqual(row["install-version"], framework.removeprefix("net") + ".x")
                self.assertEqual(row["display-name"], ".NET " + framework.removeprefix("net"))
                self.assertEqual(row["os"], "ubuntu-latest")
                self.assertEqual(row["upload_coverage"], framework == "net10.0")
                self.assertIn(row["dapr-runtime-versions"], self.versions)

    def test_ga_and_rc_windows(self):
        for versions in (self.versions, [v for v in self.versions if v["channel"] != "rc"]):
            with self.subTest(versions=versions):
                matrices = compute_matrices(self.projects, versions)
                for matrix in matrices.values():
                    self.assertEqual(len(matrix["include"]), len(self.projects) * len(versions))

    def test_matrix_limit_boundary(self):
        projects = [self.projects[0]] * 256
        matrices = compute_matrices(projects, self.versions[:1])
        self.assertTrue(all(len(matrix["include"]) == 256 for matrix in matrices.values()))
        with self.assertRaisesRegex(ValueError, "257.*256-job matrix limit"):
            compute_matrices(projects + [self.projects[0]], self.versions[:1])

    def test_project_growth_fails_before_github_expands_the_matrix(self):
        with self.assertRaisesRegex(ValueError, "260.*256-job matrix limit"):
            compute_matrices([self.projects[0]] * 52, self.versions)

    def test_invalid_inputs_fail_instead_of_silently_skipping_tests(self):
        for invalid in ([], None, {}, "[]", [{}], [{"project": "", "projectName": "Example"}]):
            with self.subTest(projects=invalid), self.assertRaises(ValueError):
                compute_matrices(invalid, self.versions)
        for invalid in ([], None, {}, "[]", [{}], [{"version": "", "channel": "stable"}]):
            with self.subTest(versions=invalid), self.assertRaises(ValueError):
                compute_matrices(self.projects, invalid)

    def test_cli_writes_all_four_github_outputs(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "outputs"
            output.write_text("existing=value\n", encoding="utf-8")
            subprocess.run(
                [sys.executable, str(Path(__file__).with_name("compute_integration_matrix.py"))],
                env={
                    **os.environ,
                    "PROJECTS_JSON": json.dumps(self.projects),
                    "DAPR_VERSIONS_JSON": json.dumps(self.versions),
                    "GITHUB_OUTPUT": str(output),
                },
                check=True,
                capture_output=True,
                text=True,
            )
            lines = output.read_text(encoding="utf-8").splitlines()
            self.assertEqual(lines[0], "existing=value")
            actual = dict(line.split("=", 1) for line in lines[1:])
            self.assertEqual(
                {key: json.loads(value) for key, value in actual.items()},
                compute_matrices(self.projects, self.versions),
            )


if __name__ == "__main__":
    unittest.main()
