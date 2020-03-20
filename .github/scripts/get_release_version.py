# ------------------------------------------------------------
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
# ------------------------------------------------------------

# This script parses release version from Git tag and set the parsed version to
# environment variable, REL_VERSION.

import os
import sys

gitRef = os.getenv("GITHUB_REF")
tagRefPrefix = "refs/tags/v"

if gitRef is None or not gitRef.startswith(tagRefPrefix):
    print ("##[set-env name=REL_VERSION;]edge")
    print ("This is daily build from {}...".format(gitRef))
    sys.exit(0)

releaseVersion = gitRef[len(tagRefPrefix):]
releaseNotePath="docs/release_notes/v{}.md".format(releaseVersion)

if gitRef.find("-rc.") > 0:
    print ("Release Candidate build from {}...".format(gitRef))
else:
    # Set LATEST_RELEASE to true
    print ("##[set-env name=LATEST_RELEASE;]true")
    print ("Release build from {}...".format(gitRef))

print ("##[set-env name=REL_VERSION;]{}".format(releaseVersion))