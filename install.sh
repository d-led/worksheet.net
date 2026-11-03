#!/usr/bin/env bash
# Installs Worksheet: one self-contained binary, no runtime to install.
#
#   curl -fsSL https://raw.githubusercontent.com/d-led/worksheet.net/main/install.sh | bash
#
# WORKSHEET_BIN_DIR  where to put the binary (default: ~/.local/bin)
# WORKSHEET_REPO     owner/repo to download from
# WORKSHEET_BASE_URL download root, instead of the latest GitHub release

set -euo pipefail

repo="${WORKSHEET_REPO:-d-led/worksheet.net}"
base="${WORKSHEET_BASE_URL:-https://github.com/$repo/releases/latest/download}"
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

echo "Downloading $asset ..."
mkdir -p "$bin_dir"

if command -v curl >/dev/null 2>&1; then
    curl -fL --progress-bar "$base/$asset" -o "$target.part"
elif command -v wget >/dev/null 2>&1; then
    wget -q --show-progress "$base/$asset" -O "$target.part"
else
    echo "Install curl or wget first." >&2
    exit 1
fi

chmod +x "$target.part"
mv "$target.part" "$target"

# A downloaded binary can carry the quarantine flag, which stops it from starting. There is no
# recursive form of xattr any more, and there is no need for one: this is a single file.
xattr -d com.apple.quarantine "$target" 2>/dev/null || true

echo "Installed $target"

case ":$PATH:" in
    *":$bin_dir:"*) ;;
    *) echo "Add it to your PATH:  export PATH=\"$bin_dir:\$PATH\"" ;;
esac
