#!/usr/bin/env bash
# Analysers and code-style rules, warnings as errors. CI runs it; .editorconfig says what is off.
set -euo pipefail
cd "$(dirname "$0")/.."

# --no-incremental: an analyser that only sees changed files hides the rule it just broke.
dotnet build Worksheet.slnx --no-incremental
