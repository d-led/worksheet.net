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

Or take the file for your machine from the [latest release](https://github.com/d-led/worksheet.net/releases/latest):
`worksheet-osx-arm64`, `worksheet-osx-x64`, `worksheet-win-x64.exe`, `worksheet-linux-x64`,
`worksheet-linux-arm64`. One file, nothing else to install.

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

The holes between blocks are shown as rows of their own, with their length. Their `＋` button puts
those times into the entry line, so a forgotten half hour is two clicks away from being recorded;
their `Break` button records the hole at once, in the `Break` project, with a note you can change.

Under the line, a `next:` hint names the part still missing, and anything already wrong — an
intersection with another block, for instance — is said while you type, not after Enter.

## Days that do not work

Above the table sits what the day is: `Working day`, `Flex day`, `Sick day`, `Holiday`, `Weekend`.
A Saturday and a Sunday start as a weekend; any of the five can be picked over them.

![A sick day: the recorded blocks stay, the timetable is frozen](docs/screenshots/frozen-day.png)

A day that is not a working day is frozen. Everything already recorded stays on screen — a sick
morning is not deleted by becoming a sick day — but nothing on the day can be changed: no new line,
no correction, no retraction, no break taken out of a hole. Pick `Working day` and the day is yours
again. Choosing a status is a change like any other, so undo takes it back too.

## Undo and redo

`⌘Z` / `⇧⌘Z`, or `Ctrl+Z` / `Ctrl+Shift+Z`, or the two buttons at the bottom, or the tray menu.

## It lives in the tray

Closing the window hides it and the day keeps counting. The tray icon brings it back; the tray menu
is also the only place that quits. A second copy leaves quietly instead of fighting the first over
the same data file.

## Where your data lives

One file:

- macOS — `~/Library/Application Support/Worksheet`
- Windows — `%APPDATA%\Worksheet`
- Linux — `~/.config/Worksheet`

Set `WORKSHEET_DATA_DIR` to keep it somewhere else, for a portable copy.

## Licence

Mozilla Public License 2.0, see [LICENSE](LICENSE).
