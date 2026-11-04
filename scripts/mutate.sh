#!/usr/bin/env bash
# Stryker with stryker-config.json, over one glob or all of Worksheet.Core.
#   scripts/mutate.sh  |  scripts/mutate.sh "**/UndoPlanner.cs"
# Local and occasional: a full run is minutes, and the score is a hint, not a gate.
set -euo pipefail
cd "$(dirname "$0")/.."

# A run compiles deliberately broken variants; warnings as errors would call half of them
# "cannot compile" rather than a finding (Directory.Build.props).
export WS_MUTATION=1

dotnet tool restore >/dev/null
dotnet stryker --mutate "${1:-**/*.cs}"
