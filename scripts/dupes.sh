#!/usr/bin/env bash
# jscpd with .jscpd.json. Local, occasional, never a gate: a duplicate is a judgement call.
set -euo pipefail
cd "$(dirname "$0")/.."

npx --yes jscpd@4 src tests tools
