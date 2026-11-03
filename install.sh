#!/usr/bin/env bash
# Installs Worksheet: one self-contained binary, no runtime to install.
#
#   curl -fsSL https://raw.githubusercontent.com/d-led/worksheet.net/main/install.sh | bash
#
# What it installs is the newest build of `main` — there is no release to wait for.
#
# WORKSHEET_BIN_DIR  where to put the binary (default: ~/.local/bin)
# WORKSHEET_REPO     owner/repo to download from (default: d-led/worksheet.net)
# WORKSHEET_BRANCH   which branch's build to take (default: main)
# WORKSHEET_WORKFLOW the workflow that builds it (default: build)
# WORKSHEET_BASE_URL a directory holding worksheet-<os>-<arch> itself, which skips the unzip

set -euo pipefail

repo="${WORKSHEET_REPO:-d-led/worksheet.net}"
branch="${WORKSHEET_BRANCH:-main}"
workflow="${WORKSHEET_WORKFLOW:-build}"
bin_dir="${WORKSHEET_BIN_DIR:-$HOME/.local/bin}"

case "$(uname -s)" in
    Darwin) os=osx ;;
    Linux) os=linux ;;
    *)
        echo "Worksheet does not support $(uname -s) yet." >&2
        exit 1
        ;;
esac

case "$(uname -m)" in
    arm64 | aarch64) arch=arm64 ;;
    x86_64 | amd64) arch=x64 ;;
    *)
        echo "Worksheet does not support the $(uname -m) architecture yet." >&2
        exit 1
        ;;
esac

asset="worksheet-$os-$arch"
target="$bin_dir/worksheet"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

fetch() {
    if command -v curl >/dev/null 2>&1; then
        curl -fL --progress-bar "$1" -o "$2"
    elif command -v wget >/dev/null 2>&1; then
        wget -q --show-progress "$1" -O "$2"
    else
        echo "Install curl or wget first." >&2
        exit 1
    fi
}

unpack() {
    if command -v unzip >/dev/null 2>&1; then
        unzip -q "$1" -d "$2"
    elif command -v python3 >/dev/null 2>&1; then
        python3 -m zipfile -e "$1" "$2"
    else
        echo "Install unzip (or python3) first: the download arrives zipped." >&2
        exit 1
    fi
}

mkdir -p "$bin_dir"

if [ -n "${WORKSHEET_BASE_URL:-}" ]; then
    echo "Downloading $asset from $WORKSHEET_BASE_URL ..."
    fetch "$WORKSHEET_BASE_URL/$asset" "$work/$asset"
else
    latest="https://nightly.link/$repo/workflows/$workflow/$branch/$asset.zip"
    echo "Downloading the newest $branch build: $latest"
    fetch "$latest" "$work/$asset.zip"
    unpack "$work/$asset.zip" "$work"
fi

if [ ! -f "$work/$asset" ]; then
    echo "The download did not contain $asset. Nothing was installed." >&2
    exit 1
fi

install -m 0755 "$work/$asset" "$target"

# A downloaded binary can carry the quarantine flag, which stops it from starting. There is no
# recursive form of xattr any more, and there is no need for one: this is a single file.
xattr -d com.apple.quarantine "$target" 2>/dev/null || true

echo "Installed $target"

case ":$PATH:" in
    *":$bin_dir:"*) ;;
    *) echo "Add it to your PATH:  export PATH=\"$bin_dir:\$PATH\"" ;;
esac
