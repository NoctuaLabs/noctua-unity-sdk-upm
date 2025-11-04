#!/bin/bash

git-cliff --latest > GithubRelease.md
[ "$(wc -l < GithubRelease.md)" -lt 2 ] && git-cliff | awk '/^## \[/{if (NR!=1) exit} {print}' > GithubRelease.md
cat GithubRelease.md
