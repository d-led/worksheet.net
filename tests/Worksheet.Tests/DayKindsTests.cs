// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;

namespace Worksheet.Tests;

/// <summary>
/// What each kind of day is called, and the one rule that matters: a day nobody works on takes
/// no entries. Without a declaration the calendar decides, and the calendar rests at weekends.
/// </summary>
public sealed class DayKindsTests
{
    private static readonly DateOnly Tuesday = new(2026, 11, 3);
    private static readonly DateOnly Saturday = new(2026, 11, 7);

    [Theory]
    [InlineData(DayKind.Working, "Working day")]
    [InlineData(DayKind.Flex, "Flex day")]
    [InlineData(DayKind.Sick, "Sick day")]
    [InlineData(DayKind.Holiday, "Holiday")]
    [InlineData(DayKind.Weekend, "Weekend")]
    public void Every_kind_of_day_has_its_own_word(DayKind kind, string label) =>
        Assert.Equal(label, DayKinds.LabelOf(kind));

    [Fact]
    public void Only_a_working_day_takes_entries() =>
        Assert.All(
            Enum.GetValues<DayKind>(),
            kind => Assert.Equal(kind == DayKind.Working, DayKinds.TakesEntries(kind)));

    [Fact]
    public void A_weekday_works_and_a_weekend_rests_until_somebody_says_otherwise()
    {
        var state = LedgerState.Create();

        Assert.Equal(DayKind.Working, state.KindOf(Tuesday));
        Assert.Equal(DayKind.Weekend, state.KindOf(Saturday));
        Assert.True(state.TakesEntries(Tuesday));
        Assert.False(state.TakesEntries(Saturday));
    }
}
