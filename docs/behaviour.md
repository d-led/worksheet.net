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

1. Shows one day at a time: the entries of that day in start order, and the total
   time on them.
2. Accepts an entry as one line of text (see `entry-syntax.md`), completing the names
   it is made of: the known projects while a project is being typed, then the topics
   of that project once its bracket is open. The keyboard walks the offers and takes
   one, and the bracket that comes next is written in already.
3. Says what kind of day it is — working, flex, sick, holiday or weekend — as a row of
   radios above the table, and freezes the timetable on every day that is not a
   working day.
4. Undoes the most recent change by appending its inverse, and redoes it by appending it
   again. Any new change forgets what could have been redone.
5. Runs in the system tray. Closing the window hides it. Only the tray's *Quit*
   ends the process.
6. Follows the system's light or dark appearance, and its accent colour.
7. Keeps everything in one file in the per-user data directory.

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
- Only a working day takes entries. A day nobody has declared follows the
  calendar: Saturday and Sunday are weekends, the rest work. Declaring any other
  kind freezes the day — its entries stay, and nothing about them can be changed
  until the day is a working day again. Declaring the kind the calendar already
  gives is not stored at all.
- Undo restores the prior state exactly: it appends the inverse of the last
  journal change, so replaying the journal after undo produces the same state as
  before the change. On a ledger with no snapshot, this is verifiable by
  construction; the tests do exactly that.

## Deliberately not included

- Report templates and transformation files. Totals are computed from the ledger;
  anything richer is a query over the journal, exported as XML (a later slice).
- Plugin loading. The process is one binary.
- A second copy of the state in a relational schema. The journal is the truth.
- Editing settings through the UI beyond the day navigation and the entry line.
  Settings live in a small JSON file next to the journal.
