#!/usr/bin/env bash
# The CRAP gate: fails when a method's complexity is out of proportion to its tests. CI runs it.
set -euo pipefail
cd "$(dirname "$0")/.."

rm -rf coverage
dotnet test Worksheet.slnx --collect:"XPlat Code Coverage" --results-directory ./coverage >/dev/null
python3 scripts/crap.py
