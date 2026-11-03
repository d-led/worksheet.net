# Design review

Written 2026-11-03, before the surface grew: the point is to fix the shape of the window while
there is one window.

## What was wrong

The first version of the shell was a single 90-line view with four problems that get expensive to
undo later:

1. **One file owned everything.** Layout, styles, formatting and commands lived in `MainWindow.axaml`
   and one view model. Every new requirement edited the same file, so the second feature costs more
   than the first.
2. **Typography was per-element.** Sizes were set where they were needed, so nothing was
   consistently the same size as anything else.
3. **Only one way in.** The single-line entry was the only input; a mistake meant retracting the
   entry and typing it again. That is a data model problem wearing a UI hat.
4. **Undo was one-way.** A compensation could be appended but never taken back.

## The shape now

```
Views/MainWindow.axaml        SukiWindow: four components, no logic
Components/DayHeader          navigation, the day, the calendar, the total
Components/QuickEntry         the entry language, one line, Enter, known-name chips
Components/DayTable           one column per fact, each editable in place
Components/StatusLine         what happened, undo, redo
Styles/Tokens.axaml           every font size, weight and surface, in one place
ViewModels/DayViewModel       the day: rows, totals, commands, suggestions
ViewModels/EntryRowViewModel  one row: editors, and a revision when one changes
```

A component is a `UserControl` that expects `DayViewModel` as its data context and knows nothing
about the window. Adding a settings page, a week view or a second window does not touch the day.

## Decisions

**A row edit is a revision, not a mutation.** `EntryRowViewModel` builds the corrected entry from
its last accepted snapshot and asks the day to revise it, which appends `EntryRevised(before,
after)`. The journal keeps both sides, so the correction is undoable and the history says what
changed. When the ledger refuses (a range past midnight, a stale project) the editors are put back
to the last accepted value and the reason is shown in the status line.

**Undo and redo are a cursor, not a stack of states.** Undo appends the inverse and remembers the
original; redo appends it again. Any new user change forgets the redo path — the behaviour every
editor has. Both are `IsEnabled`-bound, so the buttons tell the truth about what is possible.

**Project and topic are chosen from what exists.** The quick-entry line resolves by exact name and
then by unique prefix; the row offers dropdowns of known projects and of the topics inside the
chosen project. A project cannot be opened twice under two spellings, and when the typed prefix
matches existing names they are offered as chips.

**SukiUI 7.0.1** for the base look: its theme, its accent colour handling, its control templates.
Only two brushes are ours (`SurfaceBrush`, `HairlineBrush`) and both are translucent, so they blend
with whichever background Suki paints in light or dark.

**Typography is a closed set.** `title`, `total`, `body`, `numeric`, `caption`, `meta`, `quiet`.
No view sets a font size inline; if a size is missing, it is added to the tokens, not to the view.

**Hit targets have a floor.** Navigation is 44×44 (`Button.nav`), row actions 32×32
(`Button.row-action`). The previous 24×24 chevrons were the most obviously wrong control on screen.

## Known gaps, deliberately left

- A topic can only be chosen from the list; creating a new one is still the quick-entry line.
- An entry cannot be moved to another day by editing; it must be retracted and logged again.
- A second instance exits quietly instead of raising the first one's window (needs a small local
  IPC channel; the store's single-writer rule makes the guard necessary now, not the handoff).
- No week or month view. The data model supports it (`EntriesOn` over a range is trivial); the
  screen does not exist yet.
