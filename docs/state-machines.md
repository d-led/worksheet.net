# State machines

What this application has states, what may follow what, what must stay true whatever happens, and
which of the states it can reach are wrong. Every claim here names the code that makes it true, so
that changing the code and not this file is a thing a reader can notice.

The last section lists the states the code can reach but should not, with the impact and what was
done about each.

## The journal and its fold

`Ledger` (`src/Worksheet.Core/Journal.cs`), `IEventJournal` / `ISnapshotStore`, `LiteDbStore`
(`src/Worksheet.Storage/LiteDbStore.cs`).

**States:** `Empty` (version 0), `Open` (a fold at the journal's version), `Closed` (the store
disposed). A commit is not a state of its own: it either happens whole or not at all.

| From | What happens | To | What is written |
| --- | --- | --- | --- |
| Empty | `Open` | Open at 0 | nothing |
| Empty, Open | `Commit(changes, origin)`, `changes` empty | unchanged | nothing, and nobody is told |
| Empty, Open | `Commit(changes, origin)` | Open at `n + count` | every change, in one transaction, then folded |
| Open | `TryUndo` | Open at `n + 1` | the inverse of the last change |
| Open | `TryRedo` | Open at `n + 1` | the change the last undo took away |
| Open | `Dispose` | Closed | nothing |

**Invariants.**

- **The fold's version is the journal's version.** `EnsureConsistent` refuses to open when they
  disagree; `Commit` appends before it folds, so a refused append leaves both where they were.
- **Versions are `1..n`, with no gaps and none reused.** The store inserts `_id = version`, sized
  from the current maximum inside its lock, and refuses an append whose `expectedVersion` is not
  that maximum (`JournalConcurrencyException`).
- **A commit is atomic.** The inserts of one commit are one `BeginTrans`/`Commit`, with a
  `Rollback` on the way out, and the fold happens only after the append returns.
- **There is one writer.** The file is opened `ConnectionType.Direct`, so a second process cannot
  have it; `SingleInstance` keeps the process to one; MCP calls are marshalled onto the UI thread
  by `SerialToolkit` (`src/Worksheet.Mcp/Tooling.cs`), which is the thread that commits.
- **The fold is published after it is applied**, to everyone subscribed at that moment, on the
  thread that committed. A subscriber hears the state it finds when it subscribes.
- **The journal is the whole story.** Nothing is overwritten or removed; the audit
  (`Worksheet.Storage.LiteDbStore.Blobs`) reads the rows as written, not the fold.

`Commit` runs its steps in this order: append, fold, forget the redo stack, write a snapshot if one
is due, publish.

## Snapshots

`SnapshotPolicy`, `Ledger.TakeSnapshotIfDue`, `LiteDbStore.SnapshotCollection`.

**States:** no snapshot; a snapshot at version `k` (only one row ever exists).

| From | What happens | To |
| --- | --- | --- |
| any | `Commit` reaches `version - snapshotted >= Every` | snapshot at `version` |
| any | `Commit` or `Every == 0` | unchanged |
| snapshot at `k` | `LoadLatest` | a fresh `LedgerState` restored from it; the caller folds the tail |

**Invariants:** a snapshot never carries a version the journal cannot reach (it is written after
the fold); every read hands out a fresh instance; a snapshot ahead of the journal is refused
(`JournalIntegrityException`); the journal alone is enough to rebuild everything, so a snapshot is
a cache and deleting it loses nothing.

## Undo and redo

`Ledger.TryUndo`, `Ledger.TryRedo`, `UndoPlanner` (`src/Worksheet.Core/UndoPlanner.cs`).

**States:** `(CanUndo, redo depth)`, where `CanUndo` is `Version > 0`.

| From | What happens | To | Journal |
| --- | --- | --- | --- |
| any | a change of origin `User` or `Import` | `CanUndo` true, depth 0 | one row |
| depth `d` | `TryUndo` | depth `d + 1` | the inverse of the last row |
| depth `d` | `TryRedo` | depth `d - 1` | the change remembered at the top |
| any | a change of origin `Undo` or `Redo` | depth unchanged | one row |

**Invariants:** an undo is an ordinary appended change, so replaying the journal reproduces what
the state was before the change and nothing is ever dropped; the inverse of an inverse is the
original, which is what makes a redo ordinary.

**Reachable and surprising, but intended:** pressing Undo twice does not walk back two changes.
The second undo inverts the first undo, so the day returns to where it was while the journal gains
two rows. `LedgerTests.Undo_is_itself_a_change_so_it_can_be_undone` pins it and
`behaviour.md` states it ("undoes the most recent change by appending its inverse"). The redo stack
lives in memory only: after a restart nothing can be redone, while the journal still holds the
inverses.

## What kind of day it is

`DayKind`, `DayKinds`, `DayCalendar.Declare`, `LedgerState.KindOf`.

**States per day:** `Undeclared` or `Declared(kind)`. The effective kind is
`declared ?? DayKinds.DefaultOf(day)`, and only `Working` takes entries.

| Effective now | `Declare(kind)` | Stored |
| --- | --- | --- |
| `kind` | any `kind` | nothing — no row, no undo step |
| `other` | `kind == DefaultOf(day)` | `DayKindChanged(date, before, null)`: undeclared again |
| `other` | `kind != DefaultOf(day)` | `DayKindChanged(date, before, kind)` |

**Invariants:** the journal never holds a declaration that says what the calendar already says;
`Declare` is idempotent; the effective kind alone decides whether the day is editable
(`TakesEntries`, and `DayViewModel.IsFrozen` with it).

**Reachable and intended:** declaring a non-working kind on a day that already holds entries
freezes those entries. `EntryComposer` then refuses every correction with "…is marked Weekend; set
the day to Working day first", undo still works, and the rows stay read-only until the day is a
working day again (`behaviour.md`, "Only a working day takes entries").

## The day on screen

`DayViewModel` (`src/Worksheet.Desktop/ViewModels/DayViewModel.cs`).

**States:** `FollowingTheClock` (`SelectedDate` null) or `PinnedTo(day)`, each either `Editable` or
`Frozen`; plus two pieces of transient state, `_aRowIsWriting` and `_shown` (the signature of what
is drawn).

| From | Event | To |
| --- | --- | --- |
| FollowingTheClock | `Show(today)` | unchanged: today is never stored |
| FollowingTheClock, Pinned | `Show(otherDay)` | Pinned to it |
| Pinned | `ShowToday`, or `Show(today)` | FollowingTheClock |
| any | a ledger change whose `Signature` differs, and no row is writing | the day is drawn again |
| any | a ledger change whose signature is the same, or a row is writing | unchanged |
| any | a row's edit is refused | the rows are built again |
| Editable | `Declare` a non-working kind | Frozen, rows kept |
| Frozen | `Declare` Working | Editable |

**Invariants:** what is on screen is a function of the fold and the picked day, never a copy;
today is read from the injected clock every time it is needed; the entry line and the totals are
refreshed with the rows; a row's own accepted change is not drawn twice.

## A row of the day

`EntryRowViewModel`, `GapRowViewModel` (`src/Worksheet.Desktop/ViewModels/`), `Components/DayTable.axaml`.

**States per row:** `Idle`; `Typing` (the box holds text the entry does not); from `Typing`,
`Accepted` (`Snapshot` advances, both clocks are written back as the day keeps them) or `Refused`
(every field is loaded from `Snapshot` again and the day is told to rebuild the row).

**Invariants:** `Snapshot` is the last version the journal took; nothing is written while a key is
being typed — the boxes are bound `UpdateSourceTrigger=LostFocus`, so a field is taken when it is
left or Enter is pressed; a partial or impossible description (`9:` as a clock, an end before the
start) is a refusal, and a refusal writes nothing; a hole row is only ever between the two blocks
that leave it.

The rebuild after a refusal is not an optimisation left undone: a two-way box keeps the text it
was refused, and only a row built from the journal shows what is stored instead (see
`/memories/avalonia-gotchas.md` — a binding does not publish a value it has already published).

## A row of the catalogue

`CatalogueRow`, `ManageViewModel` (`src/Worksheet.Desktop/ViewModels/`), `Views/ManageWindow.axaml.cs`.

**States per row:** `Unchanged` (`Draft` is the journal's name), `Changed`, `Settled` (a decision
was taken on this row). **States of the whole panel:** `Quiet` (no message), `Refused`,
`Done`.

| From | Event | To |
| --- | --- | --- |
| Unchanged | `Draft` changes | Changed; the panel says what Enter would do |
| Changed | Enter or leaving the field | Settled, if the panel took the name |
| Changed | Enter or leaving the field, refused | Still able to take another name |
| Changed | Escape | Unchanged: the journal's name comes back |
| Done | the panel refreshes | fresh rows, all Unchanged |

**Invariants:** a name is written by one decision (`Catalogue`), never by the row; the panel
refreshes from the fold after every accepted decision; Escape puts the name back before it closes
the window.

## Projects and topics

`LedgerState` (`src/Worksheet.Core/LedgerState.cs`), `Catalogue` (`src/Worksheet.Core/Catalogue.cs`),
`EntryComposer`.

**States per project and per topic:** `Absent`, `Active`, `Archived`. An entry is always
`Active`-looking: it keeps the name it was logged under even while that name is out of the
catalogue.

| From | Event | To |
| --- | --- | --- |
| Absent | a line or a row names it | Active (`ProjectOpened` / `TopicAdded`) |
| Active | `Catalogue.RemoveProject` / `RemoveTopic` | Archived; no entry is touched |
| Archived | the name is typed again | Active (`ProjectRestored` / `TopicRestored`), with its topics |
| Active | renamed onto a name nobody has | name changes, identity kept, entries follow |
| Active | renamed onto a name in use | the two become one, as one `Batch` — one undo |

**Invariants.**

- A name answers to one project, and to one topic inside a project, compared case-insensitively —
  archived names included, which is why a revived name can never collide with a live one.
- A prefix that fits more than one name is ambiguous and resolves to nothing.
- Archiving writes the entries it carried back unchanged (`ProjectRemoved` carries both sides so
  that one undo can restore them), so the hours of a retired name keep standing.
- An entry's `TopicId`, when set, belongs to the entry's project; a merge remaps the topics of the
  project that goes away (`Catalogue.Moved`).
- Every catalogue decision is one journal change, so every decision is one undo — a merge included.

## The MCP server

`McpHttpHost.TryStart`, `McpServer`, `SerialToolkit`, `App.StartMcp` / `App.ApplySettings`.

**States:** `Off` (port 0), `Listening`, `Failed` (the port is someone else's).

| From | Event | To |
| --- | --- | --- |
| Off, Listening, Failed | settings saved with another port | the old host is disposed, the server starts again |
| Listening | settings saved with the same port | unchanged |
| any | application start | started once, whatever the settings say |

**Invariants:** every call is run on the UI thread, so the fold has one writer; a port that cannot
be listened on is a sentence in the window, never an exception out of the application.

Every change an agent makes is recorded with `ChangeOrigin.User` (`EntryComposer.Compose`), so the
audit cannot tell an agent's rows from the person's, and an agent's change forgets what could be
redone exactly as a person's does. That is a gap in what the journal says, not in what it holds.

## Weekly copies

`WeeklyBackups` (`src/Worksheet.Storage/WeeklyBackups.cs`), started and polled hourly by `App`.

**States per week:** `Absent`, `HalfWritten`, `Copied`. Four `Copied` weeks are kept.

| From | Event | To |
| --- | --- | --- |
| Absent | the week has closed | HalfWritten, then Copied once the copy is whole |
| HalfWritten | the process died, or the copy failed | half-written file discarded, then Absent again |
| Copied | the same week comes round again | Copied — nothing is written |
| Copied | a fifth week is copied | the oldest is deleted |

**Invariants:** a week is either there in full or not there at all (`<name>.part` is moved onto
`<name>`); a copy is taken on the UI thread, where the one writer is; the names sort
chronologically, so what to delete needs no file to be read.

## One running copy

`SingleInstance` (`src/Worksheet.Desktop/SingleInstance.cs`), `App.TryBecomeTheOnlyInstance`.

**States:** `NobodyRunning`, `FirstCopy` (holds the mutex, listens on the pipe), `SecondCopy`.

| From | Event | To |
| --- | --- | --- |
| NobodyRunning | start | FirstCopy: the mutex is taken, the pipe begins listening |
| FirstCopy | a second copy asks | FirstCopy raises `Summoned`, the window is raised |
| FirstCopy | the process ends | NobodyRunning: the mutex is released |
| SecondCopy | it asked, successfully or not | leaves |

**Invariants:** the mutex, not the pipe, decides who is first; the pipe only carries the ask. A
platform that will not give the pipe costs the second copy its ask, never the first its start.

## The window and the tray

`MainWindow` (`src/Worksheet.Desktop/Views/MainWindow.axaml.cs`), `ClosingProcedure`, `TrayHost`.

**States:** `Visible`, `Hidden`, `Closing`. `AllowClose` is what tells the difference between
"later" and "leaving".

| From | Event | To |
| --- | --- | --- |
| Visible | the close button, `Cmd+Q`, or the system's Quit | Hidden; the first time through a notice |
| Visible, Hidden | the tray icon, or a second copy's ask | Visible, and today is read from the clock again |
| any | the system is shutting down | Closing, without a notice |
| any | the tray's Quit | `AllowClose`, then Closing |

**Invariants:** closing a window never ends the process; the notice is given once and can be
silenced, and it is stored beside the settings, not in the journal (a preference is not domain
state and must not be undoable).

## States the code can reach but should not

Found by reading the transitions above and confirmed with a test before each fix. The first four
could be reached with an ordinary gesture; each is now closed and pinned by a test.

| # | State | Reached by | Impact | Status |
| --- | --- | --- | --- | --- |
| 1 | A weekly copy that stopped halfway is remembered as the week's copy | the copy running out of room or the process ending mid-copy; the next hourly poll decides by the name alone and never tries again | the safety net silently holds a truncated file, and the stub counts toward the four kept, so it can push a good copy out | fixed in `cf4a7e6` |
| 2 | A catalogue row is settled without having been written | Enter on a name the journal refuses — an empty one is the easy way | the row then takes no name at all: Enter does nothing, nothing says why, Escape does not help. The window has to be reopened | fixed in `cdd8411` |
| 3 | The holes of a day keep the layout they had before a correction | correcting an end, which is the ordinary way to fix a block | a hole drawn over the block it borders; its "fill" offers times the ledger then refuses | fixed in `06b176e` |
| 4 | Undo and redo availability stays as it was before a row's change | typing a project into a row after an undo | the Redo button is enabled and answers "There is nothing to redo." | fixed in `06b176e` |
| 5 | A snapshot that cannot be written hides a committed change | the disk refusing the write inside `TakeSnapshotIfDue`, which sits between the fold and `Published` | the change is in the journal and in the fold, but no window is told, and the exception reads as if the commit failed | not fixed: needs a failing disk to reach, and the answer depends on what a failed snapshot should mean. `Commit` should publish before it caches |
| 6 | The hourly copy can take the process with it | a failing copy inside a `DispatcherTimer.Tick`, raised on the UI thread | an unhandled exception once an hour; nothing on screen says the copies have stopped | not fixed: the answer belongs in `App`, which the suite does not start, and needs a decision about where the news goes |
| 7 | Saving the settings with the port unchanged does not start a server that is down | the port was taken at start-up, then freed; Settings → Save writes the same port | the message keeps saying the port is taken and the server stays down until a restart | not fixed: `App.ApplySettings` compares ports, and `App` is composition, not tested here |
| 8 | A case-only rename is reported as done while nothing is written | typing ` website ` over `Website` | the panel says "The project is called 'website' now" while the row shows `Website` | not fixed: that a case-only rename writes nothing is a decision pinned by `CatalogueTests`; only the sentence is wrong, and the right sentence is a product decision |

Three more invalid transitions exist in the code but cannot be reached by the shipped application.
They are recorded so that whoever changes the ordering above knows what is at stake.

- **A stale append to the journal** (`JournalConcurrencyException`) says "Reload and try again", and
  nothing reloads: the ledger would keep its old version and refuse every later write the same way.
  Two writers cannot meet — `ConnectionType.Direct`, one process, one thread — so this is a guard,
  not a state. `LiteDbStoreTests.A_stale_append_is_refused` reaches it with two stores.
- **`TryUndo` arms the redo stack before it commits.** A failing append would leave a change there
  that was never undone, and the exception would escape a method whose contract is a `bool` and a
  sentence. Only a store that fails to write reaches it.
- **`LedgerState.Apply` bumps the version after it folds.** A `Batch` holding a change the fold does
  not know would leave the parts before it applied and the version where it was. `EventCodec`
  refuses an unknown kind before it is written, so the journal cannot hold one; only a direct
  `Ledger.Commit` with a hand-written event reaches it.
