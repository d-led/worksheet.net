# Development

Everything that is about building this rather than using it.

## Toolchain

- .NET SDK 10, pinned in `global.json`. The pin rolls forward within the 10.0 feature band and
  will not jump to .NET 11 on its own. That is deliberate: the .NET 11 SDK requires Xcode 26.0 for
  Apple-workload projects that still target .NET 10, and downgrading Xcode breaks SkiaSharp's
  native copying. This application has no Apple workload and needs no Xcode — Avalonia's macOS
  backend is its own dylib — but the pin keeps that distinction from being discovered the hard way.
- No other prerequisites; Avalonia, SukiUI and LiteDB come from NuGet.
- Node is needed for one optional check (`scripts/dupes.sh`); nothing else does.
- `dotnet tool restore` installs Stryker for `scripts/mutate.sh` from
  `.config/dotnet-tools.json`. No other tool is installed this way.

## Build, test, run

```sh
dotnet build Worksheet.slnx
dotnet test  Worksheet.slnx
dotnet run   --project src/Worksheet.Desktop
```

Run the built binary rather than `dotnet run` when you want to watch start-up behaviour:
a `dotnet run` started from a detached subshell can fail with
`Avalonia.Native … RenderTimer -6661`, because the process has no window server session.
The binary itself is fine.

The same message appears when the screen is asleep, and that one is not about subshells: macOS
times frames with a display link, and will not create one without an active display. Start-up
fails before a window exists, so the application now says so in one line instead of printing a
stack trace. Wake the screen and start again. The headless screenshot tool does not care.

## Tests

Two projects, on purpose:

| project | what it holds | framework |
|---|---|---|
| `tests/Worksheet.Tests` | domain, storage, view models: parsing, folding, undo/redo, revisions, LiteDB round trips | xunit v2 |
| `tests/Worksheet.UiTests` | the real controls in a headless Avalonia window: typing a line, pressing Enter, clicking retract, editing a clock, closing the window | xunit v3 (required by `Avalonia.Headless.XUnit`) |

The UI tests never start the application: no tray, no single-instance guard, no shared store. Each
test opens one `Window` with one component and a throw-away ledger, which is why they run in
seconds and cannot interfere with each other.

### What the window really draws

The headless window is drawn by Skia, not by a stand-in: the same text layout and the same
painting the application ships. A test that captures a frame is therefore a test about the glass —
`Frame.Of(window, path)` keeps the picture and reads its pixels, so `RenderedDayTests` can say
that a row really carries its project's colour down its left edge, that a typed note reaches across
its cell, and that a frozen row is still written out readably.

Each captured frame is written to `artifacts/ui-tests/<test class>/<name>.png` (git-ignored), and
the build keeps them as the `ui-frames-<os>` artefact — one per platform, which is also how a
font or layout difference between the platforms is noticed. `WORKSHEET_UI_SHOTS` puts them
somewhere else.

`tests/Worksheet.TestSupport` holds what both need (a temporary directory that tolerates Windows
releasing file handles late).

### Coverage and the CRAP metric

```sh
scripts/crap.sh
```

`scripts/crap.py` reads the cobertura reports and prints `complexity² × (1 − coverage)³ + complexity`
per method, worst first, and exits non-zero if any method is over 10. It merges both reports, skips
generated XAML members, and skips the composition roots (`App`, `Program`) — they only wire parts
together, and every part is measured where it lives.

Current state: 869 methods, no method over CRAP 10, method-average coverage 95.2%. What stays
uncovered needs a window server session — the tray, the composition root, the save dialog's file
picker — or is a guard against a state a test cannot reach on purpose.

### The four checks

| command | what it answers | when |
|---|---|---|
| `scripts/analyse.sh` | do the analysers and code-style rules agree with the code? | every commit; CI |
| `scripts/crap.sh` | is a method too tangled for how little of it is tested? | every commit; CI |
| `scripts/dupes.sh` | is the same block of lines written twice? | by hand, occasionally |
| `scripts/mutate.sh` | do the tests notice a broken statement? | by hand, occasionally |

The first two gate: non-zero exit, and CI's `Quality` job runs them, so packaging waits for them.
The last two read, and stay off CI. `dupes.sh` needs Node (jscpd through `npx`); 0.5% duplicated
today, fails over 5%. `mutate.sh` needs `dotnet tool restore` and runs Stryker over
`Worksheet.Core`; a full run is minutes, so it is not a commit hook. It sets `WS_MUTATION=1`
because a run compiles deliberately broken variants, and warnings-as-errors would report half of
them as "cannot compile" instead of as findings.

crap4dotnet was tried and dropped: it wants the .NET 8 runtime, cannot read `.slnx`, and mis-joins
coverage (74 methods flagged that `crap.py` measures under 10, `EventCodec.Decode` among them).



## Building what a person installs

```sh
scripts/package.sh                  # this machine's platform
scripts/package.sh osx-arm64        # one named runtime
scripts/package.sh all              # every runtime, or --no-dmg to skip the disk image
```

It publishes with the same flags the workflow uses and then shapes the artefact. Each platform gets
its own folder, so the platform is said once instead of in every file name:

| folder | what is in it |
|---|---|
| `artifacts/osx-arm64/` (and `osx-x64`) | `worksheet` (the bare binary, as the installer downloads it), `Worksheet.net.app` to drag into `/Applications`, `Worksheet.net.zip`, `Worksheet.net.dmg` (the app beside a link to `/Applications`) |
| `artifacts/linux-x64/` (and `linux-arm64`) | `worksheet`, and `worksheet.tar.gz` with the binary, its icon and a desktop entry that expects `/opt/worksheet` |
| `artifacts/win-x64/` | `worksheet.exe` — copying it somewhere is the installation |

`IncludeNativeLibrariesForSelfExtract` is what makes it *one* file: without it Skia and HarfBuzz
stay beside the executable. The script drops the `.pdb`s those libraries leave behind and refuses to
ship a directory holding more than one file. Compression stays off — slow to start, unvalidated with
Avalonia. An unsigned `.app` runs on the machine that built it; a downloaded one needs
`xattr -d com.apple.quarantine` or a signature.

Two macOS details look like bugs the first time and are not:

- the bundle sets `LSUIElement`, so the application has **no Dock icon** by design — it lives in the
  menu bar, and the menu-bar item is the only icon it is meant to show;
- Finder draws a bundle's icon from the *LaunchServices record*, and a bundle assembled by a script
  has none until something reads its `Info.plist`. That is why a freshly built `.app` shows a blank
  icon until it has been launched once. `scripts/package.sh` now registers the bundle with
  `lsregister -f` after assembling it, so the icon is there before the first launch. Moving the
  bundle afterwards leaves the old record behind, so a copy in `/Applications` can still look
  iconless until it is launched once or registered with `lsregister -f`.

RIDs: `osx-arm64`, `osx-x64`, `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`. Binaries are
101–113 MB. The Windows ARM64 build is published from the x64 runner — a RID-specific build needs
the matching runtime pack, not a machine of that architecture, and nothing here is AOT compiled.

The macOS pair stays two files rather than one universal one: a self-contained single file carries
the runtime and the native libraries *of its architecture* (Skia, HarfBuzz, the CLR itself), and
those cannot be lipo-ed into one bundle. Both files are on the release, and the Homebrew formula
picks the one the machine needs.

### The newest build, without a release

The package jobs upload each binary as its own workflow artefact, and the README links them through
nightly.link —
`https://nightly.link/d-led/worksheet.net/workflows/build/main/worksheet-osx-arm64.zip` — so the
newest build of `main` is always downloadable without tagging anything. `install.sh` and
`install.ps1` fetch from there too. Two consequences: artefacts are kept for 90 days, and renaming
`build.yml` breaks every published link.

## Publishing to package managers

A tag is what makes a version. `build.yml` builds every platform, the `release` job attaches the
six binaries to the tag, and two more jobs turn those immutable assets into packages — a URL that
never changes, beside the sha256 of exactly the file behind it, is all a package manager asks for.
The tag is also the version the binaries carry: the package job passes it as `-p:Version`, so what
Chocolatey installs and what the application reports over MCP cannot disagree.

The whole of it starts with one command — `scripts/release.sh patch` (or `minor`, `major`,
`set 1.2.3`), which refuses a dirty tree, runs the tests, writes the version into
`Directory.Build.props`, commits it, tags `v<version>` and pushes both. `--dry-run` says what it
would do; `--yes` skips the confirmation. Pushing the tag is the release.

- **Homebrew**: `scripts/generate-homebrew-formula.sh` writes `worksheet.rb` from the release's
  checksums, and the job commits it to the `d-led/homebrew-d-led` tap, where
  `brew install d-led/d-led/worksheet` finds it. It needs `PUBLISHER_TOKEN`: a token allowed to
  push to that repository. The formula installs the bare executable under the name `worksheet`,
  which is also why its `test` only checks that the file arrived and is executable — starting the
  application would open a window and never return.
- **Chocolatey**: `scripts/generate-chocolatey-package.sh` fills in `packaging/chocolatey/` and
  packs `worksheet.net.<version>.nupkg`; the job pushes it to community.chocolatey.org with
  `CHOCOLATEY_API_KEY`. Without the key the job is skipped, so a fork, or a checkout with no
  secret, still releases. `choco` exists only on Windows, which is why that job runs on
  `windows-latest`. The install script downloads the single executable into the package's tools
  folder and lets Chocolatey shim it; the `worksheet.exe.gui` file beside it says the shim must not
  wait for a window. The iconURL comes from jsDelivr rather than raw.githubusercontent.com, which
  the community repository rejects. A new package is moderated before anyone can install it.

Re-submitting the same version with a packaging fix — moderation asking for a different icon, say
— is `.github/workflows/publish-choco.yml`, run by hand with the released version: it takes the
already-released executable, so the tag and its checksums stay untouched.

## Packaging into a screenshot

`tools/Worksheet.Screenshot` renders the real window offscreen with headless Skia and writes a PNG:

```sh
# the whole window: a working Monday
dotnet run --project tools/Worksheet.Screenshot -- docs/screenshots/day.png

# crops of it, on the same surface: the status with one frozen row, the entry line and its offers,
# and a hole before and after taking it as a break
dotnet run --project tools/Worksheet.Screenshot -- docs/screenshots/frozen-day.png --kind sick --only status
dotnet run --project tools/Worksheet.Screenshot -- docs/screenshots/completion.png --typing "Client portal (" --only entry
dotnet run --project tools/Worksheet.Screenshot -- docs/screenshots/break.png --only break

# the panel that renames and merges, with a name half typed so the sentence is in the picture
dotnet run --project tools/Worksheet.Screenshot -- docs/screenshots/projects.png --only projects

# the audit of the journal, filtered, and one change of it opened for its body
dotnet run --project tools/Worksheet.Screenshot -- docs/screenshots/journal.png --only journal
dotnet run --project tools/Worksheet.Screenshot -- docs/screenshots/change.png --only change
```

The example day is eight hours over three projects plus an hour's break (nine hours on the day,
of which the heading counts the eight that were worked),
on a fixed Monday so that the picture never depends on the day it is rendered — a weekend would
freeze the very timetable it is meant to show. Regenerate all seven after any layout change — it is
how the clipped start column was found. The journal and the change pictures carry the moment the
journal was written, so those two differ on every run; the other five are byte-identical unless the
layout moved.

## Continuous integration

`.github/workflows/build.yml`:

- **quality** — `ubuntu-latest`, `scripts/analyse.sh` then `scripts/crap.sh`: the analysers with
  warnings as errors, and the CRAP gate over both test projects
- **test** — `ubuntu-latest`, `windows-latest`, `macos-latest`, `dotnet test Worksheet.slnx`,
  and the frames the UI tests drew, kept as an artefact per platform
- **package** — one job per RID, each published on its own operating system, then asserted to
  contain exactly one file, renamed to `worksheet-<rid>[.exe]` so all five can share a release
- **release** — on a `v*` tag, downloads every artifact and attaches it with `gh release create`

Packaging waits for both `test` and `quality`, so nothing is published from a tree that fails
either. The duplicate and mutation checks are not in CI on purpose — see the four checks above.

Not there yet: dmg/msi/deb installers and notarisation, which need signing credentials. On macOS an
unsigned download carries the quarantine flag; `install.sh` clears it after copying.

## Layout

| path | responsibility |
|---|---|
| `src/Worksheet.Core` | model, journal changes, the fold, undo/redo planning, the catalogue, the entry language, the project palette |
| `src/Worksheet.Storage` | LiteDB journal and snapshots, the JSON codec, the data directory, the settings file, the weekly copies |
| `src/Worksheet.Export` | the range as a materialized object, its JSON file, its HTML page, and the zip |
| `src/Worksheet.Mcp` | JSON-RPC over the MCP shape, the ten tools, the loopback transport |
| `src/Worksheet.Desktop` | SukiUI theme, `SukiWindow`, the components, view models, tray |
| `tools/Worksheet.Screenshot` | headless render of the window to a PNG |

`src/Worksheet.Desktop/Styles/Tokens.axaml` is the only place a font size, weight or surface colour
is decided. No view sets one inline.

## Decisions and their reasons

- `docs/adr/0001-event-sourced-ledger.md` — why the journal is the only state, why one stream, why
  undo is a compensating change, why snapshots are a cache, and why only one process may write.
- `docs/design-review.md` — what was wrong with the first window, and the shape that replaced it.
- `docs/entry-syntax.md` — the grammar of one line, with examples.
- `docs/behaviour.md` — what the application does, and what it deliberately does not do.
- `docs/state-machines.md` — the states each part has, what may follow what, what must stay true,
  and the states the code can reach but should not.

Some choices in the window look like omissions and are not:

- **Undo and redo stay out of the tray menu.** They change the day, and a change made with the window
  hidden is one nobody can see. The footer carries both, with their key combinations.
- **A second copy of the process exits at start-up** instead of opening the same data file twice,
  because one process is the only writer (`docs/adr/0001-event-sourced-ledger.md`).
- **The ledger is the stream.** `Ledger` implements `IObservable<LedgerState>`: subscribing hands
  over the state as it stands, then every version after it, published on the thread that committed.
  Windows subscribe instead of being told to reload — that tells are what left the catalogue window
  stale when an agent wrote through MCP. The fold stays a live single-writer object, so an observer
  reads it on the thread that changed it and nothing needs a lock.
- **`settings.json` is a file of its own**, apart from the journal, so that a preference is never part
  of the undo history.
- **The weekly copy is noticed by an hourly poll**, not by an alarm at midnight: the application may
  run for days without being restarted, and may not be running at midnight at all. The copy is taken
  on the UI thread, with the store's own gate held and after a checkpoint, because the ledger has one
  writer and the file it has open is the only consistent thing there is to copy.
- **Shapes other people read are types, not anonymous objects.** The export's data file
  (`ReportShape.cs`), the tool listing a client reads (`ToolSchema`, `ToolProperty`, `ToolListing`) and
  the day's rows (`IDayRow`) are records and interfaces with the wire names spelled out, so a change
  to one is a change to a named thing rather than to a shape that exists nowhere. `ExportTests`
  pins version 1 of the data file member by member and order by order, which is what makes the
  format safe to refactor behind. The journal's own row stays a `BsonDocument` on purpose: its field
  names are the file's contract, and mapping it through a class would rename them in every file
  already written.
- **The second copy asks the first for its window.** The store is single-writer, so a second copy is
  refused the mutex; it used to leave silently, which made starting the application again do
  nothing visible. It now connects to a named pipe the running copy holds and leaves, and the running
  copy raises its window. A named pipe rather than a named event, because .NET implements named
  synchronization primitives only on Windows. The pipe's path carries the temporary folder and macOS
  caps a socket path at 104 characters, so the name is short and a pipe that cannot be had simply
  means no summons rather than a failure to start.
- **On macOS there is no Dock icon, and closing says so once.** Avalonia's own documentation says
  `TrayIcon.Clicked` is never raised on macOS, and an icon that carries a menu opens the menu
  instead — so the menu-bar icon cannot raise the window. It cannot report a reopen either:
  `ClassicDesktopStyleApplicationLifetime` is not an `IActivatableLifetime` in Avalonia 12.1.5
  (measured, not assumed), so `Activated` never fires and a listener for it is dead code. An
  earlier attempt kept a Dock icon and made closing minimise, relying on AppKit restoring minimised
  windows when the application is activated; but the same measurement showed the Dock icon itself
  answers no click, so it took a place and returned nothing. The application is now a menu-bar
  application: `MacOSPlatformOptions.ShowInDock = false` in `Program.cs` and `LSUIElement` in the
  bundle, and closing hides.
- **The close notice is our own window, not a system notification.** Avalonia has no API for
  system notifications; `Avalonia.Controls.Notifications` is the in-window card set, and macOS
  Notification Center would need a signed bundle and entitlements. So the first close opens a small
  window beside it that says the day keeps counting and where the icon is, with one answer —
  *Close the window*, the default, so Escape and Return both take it. Closing is not leaving, so
  leaving is not offered there; `Quit` is the tray menu's. The same notice answers the system's
  Quit, because that request arrives at the window as a close: `WindowClosingEventArgs.CloseReason`
  says which kind — `WindowClosing` for the close button and Cmd+W, `ApplicationShutdown` for
  Cmd+Q and the application menu's Quit (both put the window aside), and `OSShutdown`, the one kind
  let through, because a machine going down should not be argued with. It is
  shown once: `settings.json` holds `closeNoticeShown`, `SettingsResolver.HasShownCloseNotice` reads
  it and `RememberCloseNotice()` writes it, and `Store` keeps the flag while writing the knobs. A
  notice that repeats itself is one nobody reads.

- **A merge is one journal change.** Renaming a project or a topic onto a name already in use
  rewrites entries and re-homes or retires topics, which is several changes; they travel as one
  `Batch`, so the journal records one decision and one undo takes the whole of it back. The
  alternative — one event per rewritten entry — would have made undo step through a merge an entry
  at a time, which is not what a person means by taking it back.
- **The projects and topics panel has no second step.** A name is a field in its own row: `Enter`
  or leaving it settles the name, `Esc` puts the old one back, and a sentence above the message line
  says what `Enter` would do — including what a merge would move — while the name is still being
  typed. A confirmation dialog would have been a second question after the answer was already given.

Two export details that come from the same kind of reasoning: the JSON opens with a `version`
(`ReportJson.FormatVersion`) that counts up only when a reader of the older file would read the newer
one wrongly — a field merely added does not move it — and the page's pictures are not drawn by this
code: Bootstrap comes from a CDN and gives it its typography and its bars, and mermaid comes from
another and draws the gantt, one `section` per project. That was tried the other way round — bars
and a lane table drawn by hand — and the hand-drawn timeline was a lot of code for what mermaid's
sections do better; what stays ours is the tables, which sort themselves from an inline script and
so work from a file with no connection.

## Licence and provenance

MPL-2.0, file-level; every source file carries an SPDX header.

This is an implementation of the behaviour written down in `docs/behaviour.md`, and that document is
the specification it is held to. Nothing here was copied from another program — no source, no
interface text, no artwork, no templates — and the terminology and the storage are its own.

## One line to change when the repository moves

`install.sh`, `install.ps1` and the README all name `d-led/worksheet.net`. If the repository lives
elsewhere, that slug is the only thing to update — the scripts also accept `WORKSHEET_REPO` and
`WORKSHEET_BASE_URL` for testing against a local build.
