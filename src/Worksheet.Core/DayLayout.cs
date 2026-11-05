// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace Worksheet.Core;

/// <summary>A stretch of the day with nothing on it, in minutes from midnight.</summary>
public sealed record DaySlot(TimeSpan Start, TimeSpan End)
{
    public TimeSpan Duration => End - Start;

    public TimeOnly StartsAt => TimeOnly.FromTimeSpan(Start);

    public TimeOnly EndsAt => End >= TimeSpan.FromDays(1) ? TimeOnly.MinValue : TimeOnly.FromTimeSpan(End);
}

/// <summary>
/// A day laid out: the entries in order, and the holes between them. The holes are what a person
/// still has to account for, so they are computed here rather than guessed at in the view.
/// </summary>
public sealed record DayLayout(IReadOnlyList<Entry> Entries, IReadOnlyList<DaySlot> Gaps)
{
    public static DayLayout Empty { get; } = new([], []);

    public TimeSpan Total => Entries.Aggregate(TimeSpan.Zero, (sum, entry) => sum + entry.Duration);

    public TimeSpan GapTotal => Gaps.Aggregate(TimeSpan.Zero, (sum, gap) => sum + gap.Duration);

    public static DayLayout Of(IEnumerable<Entry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var ordered = entries.OrderBy(entry => entry.Start).ThenBy(entry => entry.Id.Value).ToList();

        return new DayLayout(ordered, GapsBetween(ordered));
    }

    private static List<DaySlot> GapsBetween(IReadOnlyList<Entry> ordered) =>
        [.. ordered
            .Zip(ordered.Skip(1), (before, after) => (Ends: Ordered(before), Starts: after.Start.ToTimeSpan()))
            .Where(pair => pair.Starts > pair.Ends)
            .Select(pair => new DaySlot(pair.Ends, pair.Starts))];

    /// <summary>The end of an entry as an offset from midnight; midnight itself stays on the day.</summary>
    private static TimeSpan Ordered(Entry entry) => entry.Start.ToTimeSpan() + entry.Duration;
}
