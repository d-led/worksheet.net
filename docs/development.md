# Development

Everything that is about building this rather than using it.

## Toolchain

- .NET SDK 10, pinned in `global.json`. The pin rolls forward within the 10.0 feature band and
  will not jump to .NET 11 on its own. That is deliberate: the .NET 11 SDK requires Xcode 26.0 for
  Apple-workload projects that still target .NET 10, and downgrading Xcode breaks SkiaSharp's
  native copying. This application has no Apple workload and needs no Xcode — Avalonia's macOS
  backend is its own dylib — but the pin keeps that distinction from being discovered the hard way.
- No other prerequisites; Avalonia, SukiUI and LiteDB come from NuGet.

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

`tests/Worksheet.TestSupport` holds what both need (a temporary directory that tolerates Windows
releasing file handles late).

### Coverage and the CRAP metric

```sh
rm -rf coverage
dotnet test Worksheet.slnx --collect:"XPlat Code Coverage" --results-directory ./coverage
python3 scripts/crap.py
```

`scripts/crap.py` reads the cobertura reports and prints `complexity² × (1 − coverage)³ + complexity`
per method, worst first, and exits non-zero if any method is over 10. It merges both reports, skips
generated XAML members, and skips the composition roots (`App`, `Program`) — they only wire parts
together, and every part is measured where it lives.

Current state: 219 methods, no method over CRAP 10, method-average coverage 97.7%. The only
uncovered surfaces are the tray icon and the composition root, which need a window server session.

## Publishing

```sh
dotnet publish src/Worksheet.Desktop -c Release -r osx-arm64 \
  -p:PublishSingleFile=true -p:SelfContained=true \
  -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none \
  -o publish/osx-arm64
```

`IncludeNativeLibrariesForSelfExtract` is what makes it *one* file: without it Skia and HarfBuzz
stay beside the executable. Compression is deliberately off — it is slow to start and unvalidated
with Avalonia.

RIDs built: `osx-arm64`, `osx-x64`, `win-x64`, `linux-x64`, `linux-arm64`. Sizes are 96–103 MB.

## Packaging into a screenshot

`tools/Worksheet.Screenshot` renders the real window offscreen with headless Skia and writes a PNG:

```sh
# a working Monday, the same day declared sick so the timetable is frozen, and a half-typed line
dotnet run --project tools/Worksheet.Screenshot -- docs/screenshots/day.png
dotnet run --project tools/Worksheet.Screenshot -- docs/screenshots/frozen-day.png sick
dotnet run --project tools/Worksheet.Screenshot -- docs/screenshots/completion.png working "Client portal ("
```

The example day is eight hours over three projects plus a 45 minute break (525 minutes in total),
on a fixed Monday so that the picture never depends on the day it is rendered — a weekend would
freeze the very timetable it is meant to show. Regenerate all three after any layout change — it is
how the clipped start column was found.

## Continuous integration

`.github/workflows/build.yml`:

- **test** — `ubuntu-latest`, `windows-latest`, `macos-latest`, `dotnet test Worksheet.slnx`
- **package** — one job per RID, each published on its own operating system, then asserted to
  contain exactly one file, renamed to `worksheet-<rid>[.exe]` so all five can share a release
- **release** — on a `v*` tag, downloads every artifact and attaches it with `gh release create`

Not there yet: dmg/msi/deb installers and notarisation, which need signing credentials. On macOS an
unsigned download carries the quarantine flag; `install.sh` clears it after copying.

## Layout

| path | responsibility |
|---|---|
| `src/Worksheet.Core` | model, journal changes, the fold, undo/redo planning, the entry language |
| `src/Worksheet.Storage` | LiteDB journal and snapshots, the JSON codec, the data directory |
| `src/Worksheet.Desktop` | SukiUI theme, `SukiWindow`, five components, view models, tray |
| `tools/Worksheet.Screenshot` | headless render of the window to a PNG |

`src/Worksheet.Desktop/Styles/Tokens.axaml` is the only place a font size, weight or surface colour
is decided. No view sets one inline.

## Decisions and their reasons

- `docs/adr/0001-event-sourced-ledger.md` — why the journal is the only state, why one stream, why
  undo is a compensating change, why snapshots are a cache, and why only one process may write.
- `docs/design-review.md` — what was wrong with the first window, and the shape that replaced it.
- `docs/entry-syntax.md` — the grammar of one line, with examples.
- `docs/behaviour.md` — what the application does, and what it deliberately does not do.

## Licence and provenance

MPL-2.0, file-level; every source file carries an SPDX header.

This is an implementation of the behaviour written down in `docs/behaviour.md`, and that document is
the specification it is held to. Nothing here was copied from another program — no source, no
interface text, no artwork, no templates — and the terminology and the storage are its own.

## One line to change when the repository moves

`install.sh`, `install.ps1` and the README all name `d-led/worksheet.net`. If the repository lives
elsewhere, that slug is the only thing to update — the scripts also accept `WORKSHEET_REPO` and
`WORKSHEET_BASE_URL` for testing against a local build.
