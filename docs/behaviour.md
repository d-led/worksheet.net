# Behaviour

This document describes what Worksheet does. It is written for this application
and records decisions; it is not a description of any other program's source.

## Vocabulary

- **Project** — the thing being worked on. Has a name.
- **Topic** — an optional part of a project. Names are unique within a project.
- **Entry** — one time range: a date, a start, a duration, a project, an optional
  topic, an optional note. The unit of everything.
- **Journal** — the append-only list of changes. The only thing a store keeps.

## What the application does

1. Shows one day at a time: the entries of that day in start order, and the time
   worked on it — a break is on the day without being work, so the heading leaves
   it out, while the exported page and the agent's answers give the total the
   journal holds with the time without breaks beside it. The left and right arrows
   move to the day before and after,
   unless the caret is in a text field, which then keeps them for itself. A field
   takes what was typed when it is left, and Enter takes it the same way — a cell
   of the day is settled, a number in the settings is saved. A clock is then shown
   back the way the day keeps it, to the minute: `9:5` left in a start cell reads
   `09:05`, and `7:30pm` reads `19:30` — the same spelling the journal stores.
   The window opens on
   the day it is now, and it keeps no copy of that day: while it shows today it
   reads the clock again every time it is brought back or acted in, so an
   application left running overnight is about the new day rather than the one it
   started on. A day walked to or picked from the calendar stays where it was put,
   and *Today* hands the window back to the clock.
2. Accepts an entry as one line of text (see `entry-syntax.md`), completing the names
   it is made of: the known projects while a project is being typed, then the topics
   of that project once its bracket is open. The keyboard walks the offers and takes
   one, and the bracket that comes next is written in already. Once the day holds an
   entry, the line may leave its start out — `till 16:00`, `for 1h30m` — and then
   begins where the last entry of that day ended. What a line carries after its span
   is its note, with or without a `#` — nothing else may follow a span — while the
   mark is still needed when the note holds a number or a time, since such words may
   be a span typed wrong. A note may be a line of its own — `# the paper` — which
   lands on the entry the day ended with, so work logged in a hurry is commented
   from the same line a moment later.
3. Says what kind of day it is — working, flex, sick, holiday or weekend — as a row of
   radios above the table, and freezes the timetable on every day that is not a
   working day.
4. Undoes the most recent change by appending its inverse, and redoes it by appending it
   again. Any new change forgets what could have been redone.
5. Runs in the system tray. Closing the window puts it aside rather than ending
   the process; the tray icon raises it again. The first time a window is closed
   the application says so in a notice of its own — the day keeps counting, the
   icon in the menu bar brings the window back — and the notice can be silenced
   there, and is then not shown again. Nothing in that notice leaves: the
   system's own Quit — Cmd+Q, and the Quit in the application menu — is answered
   the way closing the window is, because only *Quit* ends the process, the one
   in the menu-bar icon. A shutdown the machine itself is carrying out is not
   stood in the way of. On macOS the application has no Dock icon at all: it is a
   menu-bar application, and a Dock icon it could never hear a click on would
   only take a place and answer nothing (see `development.md` for the measured
   reasons). The tray and the window's heading both open the
   other five screens — settings, projects and topics, the journal, the agent
   connection, the data folder — so a platform without a tray icon loses only
   *Quit*. Each of those windows leaves on Escape; in the catalogue, a name that
   differs from the journal is put back first, and the next Escape leaves.
   Starting the application again raises the window of the copy that is running,
   rather than starting a second copy beside it.
6. Follows the system's light or dark appearance, and its accent colour.
7. Keeps everything in one file in the per-user data directory.
8. Reads the journal as an audit when asked: every stored change newest first,
   narrowed by free text — each word typed has to appear somewhere in a change,
   its kind and its origin and its body included — with a change opened whole,
   its body written out with an indent and coloured. The audit reads the file as
   it was written; it is not a second copy of anything.
9. Exports a range as one zip — the data and a page — through the platform's save
   dialog. The range is the day on screen, its week from Monday to Sunday, or its
   month, quarter or year; a range of more than one day ends its page with a table
   of every day against every project. What was written is announced under the
   heading: the file, the folder it went to, and one action that selects it in the
   platform's file manager. A dismissed dialog writes nothing and says so; the
   notice is put away without touching the file, and the next export replaces it.

## Rules the application enforces

- An entry is entirely within one day. A range that crosses midnight is refused.
- A day is a sequence: no two entries may share a minute. An entry that would
  intersect another is refused, and the refusal names the entry it would run
  into, so the person can see what is already there. The same project may be
  used as often as the day needs, as long as the ranges do not intersect.
- The day is always shown in start order, whatever order the entries were
  logged in.
- Time that no entry covers is shown as a gap between two entries, with its
  length, and can be filled: the entry line is pre-filled with that range so
  that only a project is missing. A gap can also be taken as a break, which
  records it in the `Break` project with a note to be changed.
- A break is a project without topics. What came before or after it is not a
  topic, so a line may not give `Break` one, a correction may not add one later,
  and the topic picker of a break row is closed.
- A break is time on the day but not time worked: the heading of the day leaves
  it out, the content of the day keeps it, and a total is given with the time
  without breaks beside it rather than on its own.
- Only a working day takes entries. A day nobody has declared follows the
  calendar: Saturday and Sunday are weekends, the rest work. Declaring any other
  kind freezes the day — its entries stay, and nothing about them can be changed
  until the day is a working day again. Declaring the kind the calendar already
  gives is not stored at all.
- Undo restores the prior state exactly: it appends the inverse of the last
  journal change, so replaying the journal after undo produces the same state as
  before the change. On a ledger with no snapshot, this is verifiable by
  construction; the tests do exactly that.
- Retiring a project or a topic takes the name out of what is offered and leaves
  every entry exactly as it was: the journal is append-only, so hours keep the
  name they were logged under. Typing the name again brings it back, with the
  topics that went with the project. Moving hours to another name is a remap —
  a decision of its own, not a side effect of retiring.
- Renaming a project or a topic onto a name that is already in use merges the
  two instead of refusing. The entries of the name that goes away keep their
  hours and take the name that stays; a topic the survivor does not have moves
  under it, and a topic it already has absorbs the entries of the one that came.
  A name retired earlier comes back when something is merged into it. The whole
  merge is one journal change, so one undo takes it back — a merge is a decision,
  not a sequence of side effects. The panel says in one sentence what pressing
  Enter would do, with what would move, before it is pressed.

## Deliberately not included

- Report templates and transformation files. The export is one fixed pair of files for a range — a
  JSON report and a page built from the same materialized object — rather than a template language.
- Plugin loading. The process is one binary.
- A second copy of the state in a relational schema. The journal is the truth.
- Choosing more than the knobs the settings view offers. The data folder is shown there rather than
  changed, the day-kind labels are code, and everything else is settled by the day's own behaviour.
