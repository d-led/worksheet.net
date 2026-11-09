# Worksheet

Log your working day in one line, on projects and on the topics inside them.

![A day of eight hours over three projects with an hour's break](docs/screenshots/day.png)

## Install

macOS and Linux:

```sh
curl -fsSL https://raw.githubusercontent.com/d-led/worksheet.net/main/install.sh | bash
```

Windows:

```powershell
irm https://raw.githubusercontent.com/d-led/worksheet.net/main/install.ps1 | iex
```

Each installs one self-contained binary. They fetch the newest build of `main`, so there is no
release to wait for. On macOS and Linux it lands in `~/.local/bin` (`WORKSHEET_BIN_DIR` moves it, and
the installer says so if that folder is not on your `PATH`); on Windows it goes to
`%LOCALAPPDATA%\Programs\Worksheet` and is added to your `PATH`. How those builds are made:
`docs/development.md`.

Or take the file yourself. These links always give the newest build:

| Platform | Download |
|---|---|
| macOS arm64 | [worksheet-osx-arm64](https://nightly.link/d-led/worksheet.net/workflows/build/main/worksheet-osx-arm64.zip) |
| macOS x64 | [worksheet-osx-x64](https://nightly.link/d-led/worksheet.net/workflows/build/main/worksheet-osx-x64.zip) |
| Linux x64 | [worksheet-linux-x64](https://nightly.link/d-led/worksheet.net/workflows/build/main/worksheet-linux-x64.zip) |
| Linux arm64 | [worksheet-linux-arm64](https://nightly.link/d-led/worksheet.net/workflows/build/main/worksheet-linux-arm64.zip) |
| Windows x64 | [worksheet-win-x64](https://nightly.link/d-led/worksheet.net/workflows/build/main/worksheet-win-x64.zip) |

Unzip and run it: `chmod +x worksheet-*` on macOS and Linux, a double-click on Windows. macOS refuses
a downloaded binary until the quarantine flag is off; the installer clears it, and
`xattr -d com.apple.quarantine worksheet-osx-arm64` does the same by hand.

## Log a day

One line records one time range. Type it, press Enter.

| you type | what it records |
|---|---|
| `Client portal (login) 08:30 2h30m` | a project, a topic, a start and a length |
| `Client portal (reports) 11:00-12:30` | a range instead of a length |
| `Break 12:30 45m` | no topic |
| `Break 12:30 45m lunch with Anna` | a break, with its comment |
| `Reading 14:00 2h 30m # the paper` | spaced units and a note |
| `Reading 14:00 2h the paper` | the same note without the mark |
| `# the paper` | a note on its own, on the block the day ended with |
| `3.11. Reading 14:00 45m` | another day than the one on screen |
| `Reading till 16:00` | an end only: it begins where the last block ended |
| `Reading for 1h30m` | a length only, from the same start |

A project name may contain spaces. Names you have used before are offered as you type: the projects
first, then the topics of that project once its bracket is open. A name that would otherwise end in
a time, or in `till`/`for`, is refused rather than swallowed: close it with `-`, as in
`Work for- 09:00 1h`.

![Typing the opening bracket offers the topics of that project](docs/screenshots/completion.png)

`←` `→` (or `↑` `↓`) walk the offers, `Tab` takes the one they are on, and the next bracket is
written for you: `(` after a project, `)` after a topic. A line that cannot be recorded is refused
in one sentence beside it.

A note is the last thing a line carries: `Reading 14:00 2h # the paper`, and the mark is optional —
the words after the time are the note too, since nothing else may follow a span. The `#` is still
worth typing when the note holds numbers: after the mark anything is the note, while a word a time
could be made of is refused rather than taken. A note can also be a line of its own — type
`# the paper` and press Enter, and it lands on the block the day ended with, the same block `till`
and `for` continue from. A line is only read as such a note when it holds no name and no time of its
own, so a note never competes with an entry for the same keystrokes: comments thought of after the
work is logged are written without leaving the entry line.

## Fix a day

The table shows absolute times — when a block started and when it ended — one row per block, in
start order. Every cell is editable: the times, the project, the topic, the note. A cell is written
into the journal when you leave it, and the row stays where it is, so a note is typed a key at a
time and kept whole. A correction is a change, so undo takes it back.

A row is washed in the tint of its project, and its left edge carries the same colour at full
strength: a pastel worked out from the project's place in the catalogue, so the same project is the
same colour in every window, on every run, and on the exported page, where its bars and its day rows
are drawn in it too. The colours are spread as far apart as the number of projects allows — two
projects land most of the wheel apart, a dozen are never closer than twenty degrees — so two names
never turn out to be the same colour by accident. A break is the one row with no colour at all — a
pause is not a project — so it sits on the window's own surface, its edge a quiet grey mark where a
project has its stripe, and the exported page leaves its row unpainted too.

The project and topic cells take a name you type, not only one from the list. A name nobody has used
yet is created when you leave the cell, so a block can be moved onto a new project without returning
to the entry line.

The holes between blocks are rows of their own, with their length. `＋` puts that range into the
entry line; `Break` records it directly as a `Break` entry with a note you can change:

![A hole between two blocks, and the same day after pressing Break](docs/screenshots/break.png)

Under the line, a `next:` hint names the part still missing, and an error — an intersection with
another block, for instance — is shown while you type.

The interface is English, and so are the dates: a weekday or a month is worded in English and read
day first (`Thursday 5 November`), whatever language the machine is set to, so a report made on one
machine says what a report made on another says.

## Move between days

`←` and `→` walk a day back and forward, as long as the caret is not in one of the fields. `❮` `❯`,
`Today` and the calendar button do the same with the mouse: the calendar opens on the day on screen,
closes when a day is picked or the focus leaves it, and its icon is an outline with no number on it —
next to a day on screen, a number of its own would read as another date. A window showing today keeps
up with the clock: brought back after midnight it is about the new day, not the one the application
started on. A day you picked stays where you put it — today is the one day that follows the clock,
and walking back onto today hands the window to it again.

## Export a range

*Export month* in the heading writes one zip for the month the day on screen belongs to; the arrow
beside it offers the day, the week — Monday to Sunday — the quarter and the year. The file is named
for when it was taken and what it covers — `2026-11-04-0930-worksheet-2026-11.zip` — and holds two
files:

- `worksheet-2026-11.json` — the range as data: minutes per project and per project and topic, the
  total with the time without breaks beside it, and every logged day with its entries. The first
  field, `version`, is the format version: it changes only when a reader of the older file would read
  the newer one wrongly, so a script can check it and stop.
- `worksheet-2026-11.html` — totals with shares, a bar per project and a bar per project and topic, a
  gantt of the range as it was worked, and a foldable list of the days. A break counts in the total,
  so the time without breaks stands beside it. Before the days, a table of every day against every
  project says what a project took on a given day. A heading sorts its table by
  what its cells hold — a name, a time or a share — and pressing it again turns the order round. The
  gantt draws each project's bars in the same pastel the window stripes its rows in, and it zooms:
  `+`, `−` and *Reset* on the chart, ⌘ or Ctrl with the wheel, and a drag to pan a range too long to
  read at once. Bootstrap and mermaid come from a CDN; without a connection the page still reads,
  unstyled, and only the chart is missing.

Once the file is written, a banner under the heading names it and the folder it went to, and *Show in
folder* selects it in Finder, Explorer or the file manager the platform has.

Days that hold nothing are left out; the page says so at the bottom.

## Days that do not work

Above the table sits the day's status: `Working day`, `Flex day`, `Sick day`, `Holiday`, `Weekend`.
Saturday and Sunday start as weekend; any of the five can be chosen.

![A sick day: the status row, and one row of the timetable dimmed and frozen](docs/screenshots/frozen-day.png)

A day that is not a working day is frozen: what is recorded stays on screen, and nothing on the day
can be changed — no new line, no correction, no retraction, no break from a hole. Choose `Working
day` to edit it again. A status is a change, so undo takes it back.

## Undo and redo

`⌘Z` / `⇧⌘Z`, `Ctrl+Z` / `Ctrl+Shift+Z`, or the two buttons at the bottom. Anything recorded after an
undo discards the redo history; how undo is stored is in `docs/adr/0001-event-sourced-ledger.md`.

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
remove a project or a topic, and undo. Answers are sentences, refusals included. Each change it makes
is one step in the window's undo. The port comes from *Settings…* or `WORKSHEET_MCP_PORT`; `0` turns
the server off.

## Rename or retire a project

*Projects and topics…* in the heading's `⋯` (or in the tray menu) lists every project in use with
what each holds, and the topics of the one you choose. A name is edited where it stands: `Enter` or
leaving the field keeps the new name, `Esc` puts the old one back and then leaves the window.

![Renaming in place, with the merge the typed name would make stated before it is made](docs/screenshots/projects.png)

A name another row already answers to is not refused — it merges the two. The line above the message
says so while the name is still being typed, with what would move; `Enter` then makes it one name,
with the entries of both, in one change. So one undo takes a merge back as readily as a rename.

Retiring a project or a topic leaves every entry exactly as it was, under the name it was logged
with — the history is not rewritten — and simply stops offering that name; typing it again brings
the project back with its topics.

Moving a few hours from one project or topic to another, without renaming anything, is a separate
decision and is not offered yet.

## Read the journal

*The journal…* in the heading's `⋯`, and in the tray menu, is every change the file holds, newest
first: its version, its kind, who wrote it and when, beside the body it was written with.

![The journal, filtered to the changes that hold one word](docs/screenshots/journal.png)

The filter is free text, and every word typed has to appear somewhere in a change — its kind, its
origin, its version, its time or its body. An empty filter holds everything. The words are looked
for as text, so a project's name finds the change that opened it and a date finds that day's.

Double click a change to read it whole: the body is written out with an indent, coloured by what
each piece is, and can be selected and copied.

![One change, opened: the stored body, indented and coloured](docs/screenshots/change.png)

Nothing here is a second copy of the data — the audit reads the file as it was written, which is
the same thing the day is folded from.

## The tray

Closing the window puts the day aside; it keeps counting, and the tray icon is what brings it
back. The first close says so once — that the day keeps counting, and where the icon is — and
offers leaving instead; tick *Don't tell me again* to make it the last time it says it.

A click on the tray icon raises the window, and a right click opens the menu. On macOS the icon
is in the menu bar and the application has no Dock icon at all: the platform gives an application
no click of its own for an icon that carries a menu, so a Dock icon could only take a place and
answer nothing. Starting the application again raises the window of the copy already running, on
any platform, instead of starting a second one.

The menu holds *Show window*, *Connect an agent…*, *Projects and topics…*, *Settings…*, *Open data
folder*, *The journal…* and *Quit*.

The window offers the same screens without the tray: the *⋯* at the right of the heading opens
*Settings…*, *Projects and topics…*, *Connect an agent…*, *Open data folder* and *The journal…*.
Only *Quit* lives in the tray alone, because it is not a screen. Each of those windows closes with
`Esc`.

## Where your data lives

One file:

- macOS — `~/Library/Application Support/Worksheet`
- Windows — `%APPDATA%\Worksheet`
- Linux — `~/.config/Worksheet`

Set `WORKSHEET_DATA_DIR` to keep it elsewhere.

Beside the journal sit its weekly copies — `ledger-2026-W44.db` and three more. One is written when
a week closes, the application checks once an hour and at start-up, and the fifth week deletes the
oldest. Putting one back under the name `ledger.db` is the whole of a restore.

Beside the journal sits `settings.json`, with the two values *Settings…* writes: the MCP port and how
often the fold is rewritten. A stored value wins over the environment, the environment over the
built-in value; *Revert to defaults* removes the stored choice, so `WORKSHEET_MCP_PORT` and
`WORKSHEET_SNAPSHOT_EVERY` decide again.

## Licence

Mozilla Public License 2.0, see [LICENSE](LICENSE).
