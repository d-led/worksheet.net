#!/usr/bin/env bash
# Builds what a person can actually install, for one platform at a time:
#
#   macOS    Worksheet.app, a disk image holding it, and the .app as a zip
#   Linux    a tarball with the binary, its icon and a desktop entry
#   Windows  the single executable (copy it anywhere; that is the installation)
#
# The plain single file is always built too, because that is what install.sh and install.ps1
# download from a release.
#
#   scripts/package.sh                 # the platform this machine is
#   scripts/package.sh osx-arm64       # one named runtime
#   scripts/package.sh all             # every runtime the project ships
#   scripts/package.sh --no-dmg        # skip the disk image (macOS only)
#
# Everything lands in artifacts/, which is not committed.

set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."

readonly all_rids=(osx-arm64 osx-x64 win-x64 linux-x64 linux-arm64)
readonly artifacts=artifacts
readonly staging=obj/package

make_dmg=true
rids=()

host_rid() {
  local machine
  machine=$(uname -m)

  case "$(uname -s)" in
    Darwin) echo "osx-$([ "$machine" = arm64 ] && echo arm64 || echo x64)" ;;
    Linux) echo "linux-$([ "$machine" = aarch64 ] && echo arm64 || echo x64)" ;;
    *) echo "win-x64" ;;
  esac
}

for argument in "$@"; do
  case "$argument" in
    --no-dmg) make_dmg=false ;;
    all) rids+=("${all_rids[@]}") ;;
    osx-* | win-* | linux-*) rids+=("$argument") ;;
    *)
      echo "usage: scripts/package.sh [all|osx-arm64|osx-x64|win-x64|linux-x64|linux-arm64] [--no-dmg]" >&2
      exit 2
      ;;
  esac
done

if [ ${#rids[@]} -eq 0 ]; then
  rids+=("$(host_rid)")
fi

version=$(dotnet msbuild src/Worksheet.Desktop/Worksheet.Desktop.csproj -getProperty:Version -nologo | tr -d '[:space:]')
version=${version:-0.1.0}

echo "Worksheet $version: ${rids[*]}"

mkdir -p "$artifacts"

# One self-contained file, exactly as a release ships it: the same flags the workflow uses.
publish() {
  local rid=$1 output="$staging/$1/publish"

  rm -rf "$output"

  dotnet publish src/Worksheet.Desktop \
    --configuration Release \
    --runtime "$rid" \
    --self-contained true \
    --output "$output" \
    -nologo \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:DebugType=none

  # Skia and HarfBuzz leave their symbols beside the executable; the promise is one file.
  rm -f "$output"/*.pdb
}

# The executable comes out as Worksheet.Desktop; every artefact calls it worksheet.
single_file() {
  local rid=$1 output="$staging/$1/publish" name="worksheet-$rid"
  local binary="$output/Worksheet.Desktop"

  [ -f "$binary.exe" ] && binary="$binary.exe" && name="$name.exe"

  local count
  count=$(find "$output" -maxdepth 1 -type f | wc -l)

  if [ "$count" -ne 1 ]; then
    echo "expected one file in $output, found $count:" >&2
    find "$output" -maxdepth 1 -type f >&2
    exit 1
  fi

  mv "$binary" "$artifacts/$name"
  echo "  $artifacts/$name"
}

# An .icns built from the same picture the window uses: the sizes Finder asks for, upscaled
# where the source is smaller than the slot.
app_icon() {
  local target=$1 source=src/Worksheet.Desktop/Assets/app.png
  local iconset="$staging/iconset/AppIcon.iconset"

  rm -rf "$staging/iconset"
  mkdir -p "$iconset"

  for size in 16 32 128 256 512; do
    sips -z "$size" "$size" "$source" --out "$iconset/icon_${size}x${size}.png" >/dev/null
    sips -z "$((size * 2))" "$((size * 2))" "$source" --out "$iconset/icon_${size}x${size}@2x.png" >/dev/null
  done

  iconutil -c icns "$iconset" -o "$target"
}

# A bundle that can be dragged into /Applications, and a disk image that shows where to drag it.
macos_app() {
  local rid=$1 app="$artifacts/worksheet-$rid.app"

  rm -rf "$app"
  mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"

  cp "$artifacts/worksheet-$rid" "$app/Contents/MacOS/worksheet"
  chmod +x "$app/Contents/MacOS/worksheet"
  sed "s/@VERSION@/$version/" packaging/macos/Info.plist.in > "$app/Contents/Info.plist"

  if command -v sips >/dev/null && command -v iconutil >/dev/null; then
    app_icon "$app/Contents/Resources/AppIcon.icns"
  else
    echo "  no sips/iconutil: the bundle gets the generic icon" >&2
  fi

  if command -v plutil >/dev/null; then
    plutil -lint "$app/Contents/Info.plist" >/dev/null
  fi

  rm -f "$artifacts/worksheet-$rid.app.zip"
  (cd "$artifacts" && zip -qry "worksheet-$rid.app.zip" "worksheet-$rid.app" -x '*/.DS_Store')
  echo "  $app"
  echo "  $artifacts/worksheet-$rid.app.zip"

  [ "$make_dmg" = true ] || return 0

  if ! command -v hdiutil >/dev/null; then
    echo "  no hdiutil: no disk image for $rid" >&2
    return 0
  fi

  local stage="$staging/dmg-$rid"

  rm -rf "$stage" "$artifacts/worksheet-$rid.dmg"
  mkdir -p "$stage"
  cp -R "$app" "$stage/"
  ln -s /Applications "$stage/Applications"

  hdiutil create -quiet -volname "Worksheet $version" -srcfolder "$stage" \
    -format UDZO -ov "$artifacts/worksheet-$rid.dmg"
  echo "  $artifacts/worksheet-$rid.dmg"
}

# A tarball that unpacks to /opt/worksheet and carries the entry that puts it in the app menu.
linux_tar() {
  local rid=$1 stage="$staging/linux-$rid/worksheet"

  rm -rf "$staging/linux-$rid" "$artifacts/worksheet-$rid.tar.gz"
  mkdir -p "$stage"

  cp "$artifacts/worksheet-$rid" "$stage/worksheet"
  chmod +x "$stage/worksheet"
  cp src/Worksheet.Desktop/Assets/app.png "$stage/worksheet.png"
  cp packaging/linux/worksheet.desktop "$stage/"

  tar -czf "$artifacts/worksheet-$rid.tar.gz" -C "$staging/linux-$rid" worksheet
  echo "  $artifacts/worksheet-$rid.tar.gz"
}

for rid in "${rids[@]}"; do
  echo "$rid"
  publish "$rid"
  single_file "$rid"

  case "$rid" in
    osx-*) macos_app "$rid" ;;
    linux-*) linux_tar "$rid" ;;
    win-*) : ;;  # the executable is the application; copying it is the installation
  esac
done

echo "done: $(ls "$artifacts" | wc -l | tr -d ' ') artefacts in $artifacts/"
