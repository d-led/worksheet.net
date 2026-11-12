#!/usr/bin/env bash
# Packs one release as a Chocolatey package (.nupkg) from the templates in packaging/chocolatey/.
#
# Expects the following environment variables:
#   VERSION   the released version without the v prefix, e.g. 0.1.0
#   URL       direct download URL of the released Windows executable
#   SHA256    sha256 of exactly that file
#   OUTDIR    where the package is staged (default: dist/choco)
#
# Requires chocolatey (`choco`) on PATH, so in practice a Windows machine — which is why the
# workflow's publish-choco job runs on windows-latest. Output: $OUTDIR/worksheet.net.<version>.nupkg
#
# Usage:
#   VERSION=0.1.0 URL=... SHA256=... scripts/generate-chocolatey-package.sh
set -euo pipefail
cd "$(dirname "$0")/.."

version="${VERSION:?VERSION is required}"
url="${URL:?URL is required}"
sha256="${SHA256:?SHA256 is required}"
outdir="${OUTDIR:-dist/choco}"
release_url="https://github.com/d-led/worksheet.net/releases/tag/v${version}"
# community.chocolatey.org rejects raw.githubusercontent.com, so the icon is served by jsDelivr,
# pinned to the tag so the file it returns cannot change under the package.
icon_url="https://cdn.jsdelivr.net/gh/d-led/worksheet.net@v${version}/src/Worksheet.Desktop/Assets/app.png"

rm -rf "$outdir"
mkdir -p "$outdir"

# choco pack takes everything beside the nuspec as the package's files, which is why the templates
# are rendered here rather than packed where they live.
cp -R packaging/chocolatey/tools "$outdir/tools"
cp LICENSE "$outdir/LICENSE.txt"

sed \
  -e "s|__VERSION__|${version}|g" \
  -e "s|__URL__|${url}|g" \
  -e "s|__SHA256__|${sha256}|g" \
  -e "s|__RELEASE_URL__|${release_url}|g" \
  -e "s|__ICON_URL__|${icon_url}|g" \
  packaging/chocolatey/worksheet.net.nuspec > "$outdir/worksheet.net.nuspec"

sed -e "s|__URL__|${url}|g; s|__SHA256__|${sha256}|g" \
  packaging/chocolatey/tools/chocolateyinstall.ps1 > "$outdir/tools/chocolateyinstall.ps1"

choco pack "$outdir/worksheet.net.nuspec" --outputdirectory "$outdir"
