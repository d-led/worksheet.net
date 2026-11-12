#!/usr/bin/env bash
# Bumps the version, commits it, tags the commit and pushes both — which is what starts a release:
# build.yml builds and tests every platform, attaches the binaries to the tag, commits the Homebrew
# formula to the tap and pushes the Chocolatey package.
#
# Usage:
#   scripts/release.sh patch            # 0.1.0 -> 0.1.1
#   scripts/release.sh minor            # 0.1.0 -> 0.2.0
#   scripts/release.sh major            # 0.1.0 -> 1.0.0
#   scripts/release.sh set 0.2.3        # exactly that version
#   scripts/release.sh patch --dry-run  # say what would happen, change nothing
#   scripts/release.sh patch --yes      # skip the confirmation
#
# The tests run before anything is written. A tag cannot be taken back once the release exists and
# the package managers have it, so the one cheap place to stop a bad one is here.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

readonly props=Directory.Build.props

log_error() { printf '\033[0;31m[ERROR]\033[0m %s\n' "$1" >&2; }
log_info() { printf '\033[1;33m[INFO]\033[0m  %s\n' "$1"; }
log_ok() { printf '\033[0;32m[OK]\033[0m    %s\n' "$1"; }

dry_run=false
assume_yes=false
words=()
for argument in "$@"; do
  case "$argument" in
    --dry-run) dry_run=true ;;
    --yes | -y) assume_yes=true ;;
    *) words+=("$argument") ;;
  esac
done
set -- "${words[@]:-}"

command="${1:-}"
if [ -z "$command" ]; then
  log_error "Missing command."
  sed -n '8,14p' "$0" >&2
  exit 2
fi

current=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$props" | head -1)
[ -n "$current" ] || {
  log_error "Could not read <Version> from $props"
  exit 1
}

major=${current%%.*}
remainder=${current#*.}
minor=${remainder%%.*}
patch=${remainder#*.}
patch=${patch%%-*}

case "$command" in
  major) next="$((major + 1)).0.0" ;;
  minor) next="$major.$((minor + 1)).0" ;;
  patch) next="$major.$minor.$((patch + 1))" ;;
  set)
    next="${2:-}"
    if [[ ! "$next" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
      log_error "set needs a version like 1.2.3"
      exit 2
    fi
    ;;
  *)
    log_error "Unknown command: $command (patch, minor, major, set)"
    exit 2
    ;;
esac

tag="v$next"

if [ "$next" = "$current" ]; then
  log_error "$next is already the version in $props"
  exit 1
fi

git rev-parse --git-dir > /dev/null 2>&1 || {
  log_error "Not in a git repository"
  exit 1
}

git fetch --tags --quiet origin

if git rev-parse --quiet --verify "refs/tags/$tag" > /dev/null; then
  log_error "Tag $tag already exists: a released version is never rewound, so pick the next one"
  exit 1
fi

log_info "Current version: $current"
log_info "New version:     $next"
log_info "Tag:             $tag"
echo ""
echo "About to be released:"
git log --oneline -5
echo ""

if [ "$dry_run" = true ]; then
  log_info "[DRY RUN] Would bump $props, run the tests, commit, tag $tag and push both."
  exit 0
fi

if [ -n "$(git status --porcelain)" ]; then
  log_error "The working tree is not clean; commit or stash first:"
  git status --short >&2
  exit 1
fi

if [ "$assume_yes" = false ]; then
  read -r -p "Bump to $next, tag $tag and push? Type 'yes' to confirm: " reply
  [ "$reply" = "yes" ] || {
    log_error "Aborted; nothing was changed"
    exit 1
  }
fi

# The tests first: a failure here costs nothing, a failure after the tag costs a version.
log_info "Running the tests..."
test_log=$(mktemp)
if ! dotnet test Worksheet.slnx --configuration Release --nologo --verbosity quiet > "$test_log" 2>&1; then
  log_error "The tests failed, so nothing was bumped and nothing was tagged:"
  tail -20 "$test_log" >&2
  exit 1
fi
log_ok "Tests pass"

log_info "Writing $next into $props..."
bumped=$(mktemp)
sed "s:<Version>.*</Version>:<Version>$next</Version>:" "$props" > "$bumped"
mv "$bumped" "$props"
git add "$props"
git commit --quiet --message "chore(release): $next"
log_ok "Committed"

git tag --annotate "$tag" --message "Worksheet.net $next"
log_ok "Tagged $tag"

log_info "Pushing the commit and the tag..."
git push --quiet origin HEAD
git push --quiet origin "$tag"
log_ok "Pushed"

echo ""
echo "The release workflow now:"
echo "  1. builds and tests every platform, and attaches the binaries to $tag"
echo "  2. commits worksheet.rb ($current -> $next) to d-led/homebrew-d-led"
echo "  3. pushes worksheet.net.$next.nupkg to community.chocolatey.org, where a moderator has to"
echo "     approve it before 'choco install worksheet.net' can take it"
echo ""
echo "Watch it: https://github.com/d-led/worksheet.net/actions"
