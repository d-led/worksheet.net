// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;

namespace Worksheet.Tests;

public sealed class UndoPlannerTests
{
    private static readonly Entry Entry = new()
    {
        Id = EntryId.Create(),
        ProjectId = Guid.NewGuid(),
        Date = new DateOnly(2026, 11, 3),
        Start = new TimeOnly(9, 0),
        Duration = TimeSpan.FromMinutes(90),
    };

    [Fact]
    public void Reversing_a_logged_entry_retracts_it()
    {
        var inverse = UndoPlanner.Reverse(new EntryLogged(Entry));

        Assert.Equal(new EntryRetracted(Entry), inverse);
    }

    [Fact]
    public void Reversing_a_retraction_restores_the_entry()
    {
        var inverse = UndoPlanner.Reverse(new EntryRetracted(Entry));

        Assert.Equal(new EntryLogged(Entry), inverse);
    }

    [Fact]
    public void Reversing_a_revision_swaps_the_two_sides()
    {
        var after = Entry with { Duration = TimeSpan.FromMinutes(120) };

        var inverse = UndoPlanner.Reverse(new EntryRevised(Entry, after));

        Assert.Equal(new EntryRevised(after, Entry), inverse);
    }

    [Fact]
    public void Not_every_change_has_an_inverse()
    {
        Assert.Null(UndoPlanner.Reverse(new ProjectOpened(Guid.NewGuid(), "Website")));
    }
}
