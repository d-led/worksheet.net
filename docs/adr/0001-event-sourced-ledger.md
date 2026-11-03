# 0001 — Event-sourced ledger in a single LiteDB file

Status: accepted (2026-11-03)

## Context

A time tracker's data is already a history: "on this date, from this time, for
this long, on this project and topic". The requirements that follow from that are
undo, auditability and a cheap way to compare the ledger against itself after a
correction.

## Decision

**The journal is the only state.** `Worksheet.Core` folds journal changes into
`LedgerState`; `Worksheet.Storage` appends them to one LiteDB collection. There is
no table of "current entries" to keep in sync.

**One stream.** The ledger is a single aggregate: a personal tracker has one
writer, one user, one history. Per-stream partitioning earns its complexity when
several devices merge histories, which is not now. The port therefore exposes
`Version`, `ReadFrom(version)` and `Append(changes, expectedVersion)`.

**Undo is a compensating change, not a stack.** Each change carries enough to
invert itself (`EntryRetracted` carries the whole entry, `EntryRevised` carries
before and after). `UndoPlanner.Reverse` is pure, so undo is testable without a
store, and the journal after undo explains itself.

**Snapshots are a cache with one shape.** A snapshot is the fold at a version.
Opening a ledger is always `snapshot ?? empty`, then replay the tail; there is no
second code path. The interval is configurable and `0` disables it. At the sizes
this application sees, replay is milliseconds; snapshots exist so that a decade of
data still opens instantly, not because they are needed today.

**LiteDB is single-writer.** The graphical process owns the file. Any other
process — an MCP client, a command line — must call that process rather than open
the database. This is why the storage port lives behind one seam.

## Consequences

- Any change to an event's shape must be replayable: events are immutable and
  old ones are read by new code. Adding a property with a default is easy;
  renaming a property is a migration over history.
- Compaction (rewriting the journal) is possible and never necessary; if it is
  ever implemented it is a new journal file, not an in-place edit.
- A snapshot that disagrees with the journal is detectable: open verifies that the
  folded version equals the journal's last version and refuses to start otherwise.
