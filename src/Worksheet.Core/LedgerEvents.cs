// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace Worksheet.Core;

/// <summary>
/// A change to the ledger. Changes are append-only and immutable; each one carries what is
/// needed to invert it, which is what makes undo an ordinary append.
/// </summary>
public abstract record LedgerEvent;

public sealed record ProjectOpened(Guid ProjectId, string Name) : LedgerEvent;

public sealed record TopicAdded(Guid TopicId, Guid ProjectId, string Name) : LedgerEvent;

public sealed record EntryLogged(Entry Entry) : LedgerEvent;

public sealed record EntryRevised(Entry Before, Entry After) : LedgerEvent;

/// <summary>Carries the whole entry so that the inverse needs nothing else.</summary>
public sealed record EntryRetracted(Entry Entry) : LedgerEvent;

/// <summary>
/// What a day was declared to be. Both sides are the declaration, not the effective kind, so the
/// inverse needs nothing else: <c>null</c> means "follow the calendar".
/// </summary>
public sealed record DayKindChanged(DateOnly Date, DayKind? Before, DayKind? After) : LedgerEvent;

/// <summary>A project kept its identity and changed its name.</summary>
public sealed record ProjectRenamed(Guid ProjectId, string Before, string After) : LedgerEvent;

/// <summary>A topic kept its identity and changed its name.</summary>
public sealed record TopicRenamed(Guid TopicId, string Before, string After) : LedgerEvent;

/// <summary>
/// A project taken out of the catalogue, with the entries that were moved somewhere else and the
/// topics that went with it. Both sides of the entries are carried, so undo restores everything
/// in one change — a deletion is one decision, not one per entry.
/// </summary>
public sealed record ProjectRemoved(Project Project, Topic[] Topics, Entry[] Before, Entry[] After) : LedgerEvent;

/// <summary>The inverse of <see cref="ProjectRemoved"/>: the project, its topics and its entries come back.</summary>
public sealed record ProjectRestored(Project Project, Topic[] Topics, Entry[] Before, Entry[] After) : LedgerEvent;

/// <summary>A topic taken out of a project; its entries stay, with no topic.</summary>
public sealed record TopicRemoved(Topic Topic, Entry[] Before, Entry[] After) : LedgerEvent;

/// <summary>The inverse of <see cref="TopicRemoved"/>.</summary>
public sealed record TopicRestored(Topic Topic, Entry[] Before, Entry[] After) : LedgerEvent;

/// <summary>Where a change came from. Recorded, never interpreted.</summary>
public enum ChangeOrigin
{
    User,
    Import,
    Undo,
    Redo,
}
