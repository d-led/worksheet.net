// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace Worksheet.Core;

/// <summary>
/// A stretch of the day, in minutes from midnight: a hole between two blocks, or what is left of a
/// block once another range took minutes out of it. Taking one stretch out of another is the whole
/// of the set algebra a day needs — an interruption is the same minutes described by newer news.
/// </summary>
public sealed record DaySlot(TimeSpan Start, TimeSpan End)
{
    public TimeSpan Duration => End - Start;

    public TimeOnly StartsAt => TimeOnly.FromTimeSpan(Start);

    public TimeOnly EndsAt => End >= TimeSpan.FromDays(1) ? TimeOnly.MinValue : TimeOnly.FromTimeSpan(End);

    /// <summary>The minutes an entry covers.</summary>
    public static DaySlot Of(Entry entry) =>
        new(entry.Start.ToTimeSpan(), entry.Start.ToTimeSpan() + entry.Duration);

    /// <summary>Whether the two stretches share a minute; stretches that only touch do not.</summary>
    public bool Overlaps(DaySlot other) => Start < other.End && other.Start < End;

    /// <summary>
    /// What is left once <paramref name="cut"/> is taken out: the whole stretch when nothing is
    /// taken, the two ends when the middle goes, one end when a side goes, and nothing when the cut
    /// covers it.
    /// </summary>
    public IReadOnlyList<DaySlot> Minus(DaySlot cut)
    {
        if (!Overlaps(cut))
        {
            return [this];
        }

        var pieces = new List<DaySlot>(2);

        if (Start < cut.Start)
        {
            pieces.Add(new DaySlot(Start, cut.Start));
        }

        if (cut.End < End)
        {
            pieces.Add(new DaySlot(cut.End, End));
        }

        return pieces;
    }
}
