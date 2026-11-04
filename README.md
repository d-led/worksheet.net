# Worksheet

Log your working day in one line, on projects and on the topics inside them.

![A day of eight hours over three projects with a forty-five minute break](docs/screenshots/day.png)

## Install

macOS and Linux:

```sh
curl -fsSL https://raw.githubusercontent.com/d-led/worksheet.net/main/install.sh | bash
```

Windows:

```powershell
irm https://raw.githubusercontent.com/d-led/worksheet.net/main/install.ps1 | iex
```

Both put one self-contained binary on your machine — nothing else to install. What they fetch is
the **newest build of `main`**, so there is no release to wait for; a tag makes a release with the
same files.

Prefer to take the file yourself? These links always give the latest build:

| Platform | Download |
|---|---|
| macOS arm64 | [worksheet-osx-arm64](https://nightly.link/d-led/worksheet.net/workflows/build/main/worksheet-osx-arm64.zip) |
| macOS x64 | [worksheet-osx-x64](https://nightly.link/d-led/worksheet.net/workflows/build/main/worksheet-osx-x64.zip) |
| Linux x64 | [worksheet-linux-x64](https://nightly.link/d-led/worksheet.net/workflows/build/main/worksheet-linux-x64.zip) |
| Linux arm64 | [worksheet-linux-arm64](https://nightly.link/d-led/worksheet.net/workflows/build/main/worksheet-linux-arm64.zip) |
| Windows x64 | [worksheet-win-x64](https://nightly.link/d-led/worksheet.net/workflows/build/main/worksheet-win-x64.zip) |

Unzip one and you have the application: on macOS and Linux `chmod +x worksheet-*` and run it, on
Windows double-click the `.exe`. macOS refuses a downloaded binary until the quarantine flag is off,
which is what the installer does for you and what `xattr -d com.apple.quarantine worksheet-osx-arm64`
does by hand.

## Log a day

One line records one time range. Type it, press Enter.

| you type | what it records |
|---|---|
| `Client portal (login) 08:30 2h30m` | a project, a topic, a start and a length |
| `Client portal (reports) 11:00-12:30` | a range instead of a length |
| `Break 12:30 45m` | no topic |
| `Reading 14:00 2h 30m # the paper` | spaced units and a note |
| `3.11. Reading 14:00 45m` | another day than the one on screen |

A project name may contain spaces. Names you have used before are offered as you type — the
projects first, then the topics of the project once its bracket is open:

![Typing the opening bracket offers the topics of that project](docs/screenshots/completion.png)

`←` `→` (or `↑` `↓`) walk the offers, `Tab` takes the one they are on, and the bracket that comes
next is written for you: after a project, `(`; after a topic, `)`. Whatever does not make sense is
answered in one sentence next to the line — nothing is swallowed.

## Fix a day

The table shows absolute times — when a block started and when it ended — one row per block, in
start order. Every cell is editable: the times, the project, the topic, the note. A correction is a
change like any other, so undo takes it back, and the history keeps what it was before.

The project and topic cells take a name you type, not only one from the list: the known names are
offered as you write, and a name nobody has used yet is opened when you leave the cell — so a
block can be moved onto a new project without going back to the entry line.

The holes between blocks are shown as rows of their own, with their length. Their `＋` button puts
those times into the entry line, so a forgotten half hour is two clicks away from being recorded;
their `Break` button records the hole at once — the `Break` project, a note you can change — and the
hole becomes an ordinary row:

![A hole between two blocks, and the same day after pressing Break](docs/screenshots/break.png)

Under the line, a `next:` hint names the part still missing, and anything already wrong — an
intersection with another block, for instance — is said while you type, not after Enter.

## Take a range with you

*Export month* in the heading writes one zip for the month the day on screen belongs to; the arrow
beside it offers the day itself, the quarter and the year. The zip holds two files:

- `worksheet-2026-11.json` — the range as data: minutes per project, per project and topic, and every
  logged day with its entries, for whatever you want to do with it next;
- `worksheet-2026-11.html` — a page of it: totals with shares, the same two tables, a pie of the
  projects, a gantt of the range as it was worked, and a foldable list of the days. It loads Bootstrap
  and mermaid from a CDN, so the two pictures need a connection; the tables read either way.

Days that hold nothing are left out, and the page says so at the bottom.

## Days that do not work

Above the table sits what the day is: `Working day`, `Flex day`, `Sick day`, `Holiday`, `Weekend`.
A Saturday and a Sunday start as a weekend; any of the five can be picked over them.

![A sick day: the status row, and one row of the timetable dimmed and frozen](docs/screenshots/frozen-day.png)

A day that is not a working day is frozen. Everything already recorded stays on screen — a sick
morning is not deleted by becoming a sick day — but nothing on the day can be changed: no new line,
no correction, no retraction, no break taken out of a hole. Pick `Working day` and the day is yours
again. Choosing a status is a change like any other, so undo takes it back too.

## Undo and redo

`⌘Z` / `⇧⌘Z`, `Ctrl+Z` / `Ctrl+Shift+Z`, or the two buttons at the bottom. Both work the way the
journal wants them to: an undo is the inverse change *appended* to the history — never a rewrite,
never a deletion — and anything recorded after an undo forgets what could have been redone.

## Let an agent log for you

While the application is running it answers MCP on `http://127.0.0.1:5317/mcp`, on the loopback
only. *Connect an agent…* in the tray menu shows the two snippets a client needs — VS Code reads
`servers` in `.vscode/mcp.json`, most other clients read `mcpServers`:

```json
{
  "servers": {
    "worksheet": {
      "type": "http",
      "url": "http://127.0.0.1:5317/mcp"
    }
  }
}
```

An agent can log a line, read a day back, ask for totals per project, list the catalogue, rename or
remove a project or a topic, and undo. Answers are sentences, refusals included, and every change it
makes is one undo away in the window's own undo. The port is chosen in *Settings…* in the tray menu;
`WORKSHEET_MCP_PORT` sets it too, and `0` leaves the server off.

## Rename or retire a project

*Projects and topics…* in the tray menu lists every project in use with how many entries each holds,
and the topics of the one you choose. Renaming keeps the entries where they are. Retiring a project
sends its hours to a project you pick and its topics go out of use with it; retiring a topic leaves
its hours on their days with no topic. Each of those is one change, so undo takes it back in one step.

## It lives in the tray

Closing the window hides it and the day keeps counting. A click on the tray icon brings the window
back — it never hides it, so a double click is just two raises — and the menu behind it (a right
click on Windows and Linux, the icon itself on macOS) holds *Show window*, *Connect an agent…*,
*Projects and topics…*, *Settings…*, *Open data folder* and the one way to quit. Undo and redo are
deliberately not there: they change the day, and a change made without the day on screen is a change
nobody can see. A second copy leaves quietly instead of fighting the first over the same data file.

## Where your data lives

One file:

- macOS — `~/Library/Application Support/Worksheet`
- Windows — `%APPDATA%\Worksheet`
- Linux — `~/.config/Worksheet`

Set `WORKSHEET_DATA_DIR` to keep it somewhere else, for a portable copy.

Beside the journal sits `settings.json`: the two knobs *Settings…* offers — the MCP port and how
often the fold is rewritten. What you choose there wins over the environment, and the environment
wins over the built-in value; *Revert to defaults* forgets the choice, so `WORKSHEET_MCP_PORT` and
`WORKSHEET_SNAPSHOT_EVERY` decide again. It is a file of its own on purpose: a preference is not
something to undo.

## Licence

Mozilla Public License 2.0, see [LICENSE](LICENSE).
