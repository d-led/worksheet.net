// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace Worksheet.Core;

/// <summary>Identity of a logged time range.</summary>
public readonly record struct EntryId(Guid Value)
{
    public static EntryId Create() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString("N")[..8];
}

/// <summary>The thing being worked on.</summary>
public sealed record Project(Guid Id, string Name, bool Archived);

/// <summary>An optional part of a project. Names are unique within the project.</summary>
public sealed record Topic(Guid Id, Guid ProjectId, string Name, bool Archived);

/// <summary>
/// One time range on one day. <see cref="Duration"/> is authoritative; the end is derived,
/// so an entry cannot disagree with itself.
/// </summary>
public sealed record Entry
{
    public required EntryId Id { get; init; }

    public required Guid ProjectId { get; init; }

    public Guid? TopicId { get; init; }

    public required DateOnly Date { get; init; }

    public required TimeOnly Start { get; init; }

    public required TimeSpan Duration { get; init; }

    public string? Note { get; init; }

    /// <summary>
    /// End of the range on the same day. A range that ends exactly at midnight shows 00:00 rather
    /// than throwing; a range that would run past midnight is refused before it is stored.
    /// </summary>
    public TimeOnly End
    {
        get
        {
            var end = Start.ToTimeSpan() + Duration;

            return end >= TimeSpan.FromDays(1) ? TimeOnly.MinValue : TimeOnly.FromTimeSpan(end);
        }
    }

    /// <summary>True when the range would cross midnight, which the ledger refuses.</summary>
    public bool CrossesMidnight => Start.ToTimeSpan() + Duration > TimeSpan.FromDays(1);
}

/// <summary>
/// The outcome of resolving a name the user typed. <see cref="Ambiguous"/> matters: a project
/// must never be picked by accident, so a prefix that fits several names resolves to nothing.
/// </summary>
public readonly record struct NameMatch<T>(T? Value, bool Ambiguous)
    where T : class
{
    public bool Found => Value is not null;

    public static NameMatch<T> None => new(null, false);

    public static NameMatch<T> Ambiguity => new(null, true);
}

/// <summary>The fold of a journal, written whole. Snapshots carry this shape.</summary>
public sealed record LedgerSnapshot(long Version, Project[] Projects, Topic[] Topics, Entry[] Entries)
{
    /// <summary>
    /// The days that carry a declared kind. Snapshots written before days had kinds do not hold
    /// this at all, so it reads as an empty list rather than an unreadable file.
    /// </summary>
    public DayKindMark[] Days { get; init; } = [];
}

/// <summary>One day that was declared to be something other than what the calendar says.</summary>
public sealed record DayKindMark(DateOnly Date, DayKind Kind);
