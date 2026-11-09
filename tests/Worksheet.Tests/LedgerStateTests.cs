// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;
using Worksheet.TestSupport;

namespace Worksheet.Tests;

public sealed class LedgerStateTests
{
    private static readonly DateOnly Day = new(2026, 11, 3);
    private static readonly DateOnly OtherDay = new(2026, 11, 4);

    private static readonly Guid Website = Guid.NewGuid();
    private static readonly Guid Webshop = Guid.NewGuid();
    private static readonly Guid Pause = Guid.NewGuid();
    private static readonly Guid Topic = Guid.NewGuid();

    private static LedgerState Ledger()
    {
        var state = LedgerState.Create();

        state.Apply(new ProjectOpened(Website, "Website"));
        state.Apply(new ProjectOpened(Webshop, "Webshop"));
        state.Apply(new TopicAdded(Topic, Website, "login"));
        state.Apply(new EntryLogged(EntryAt(Day, "09:00", 90)));
        state.Apply(new EntryLogged(EntryAt(Day, "10:30", 30)));
        state.Apply(new EntryLogged(EntryAt(OtherDay, "08:00", 60)));

        return state;
    }

    private static Entry EntryAt(DateOnly day, string start, int minutes, Guid? project = null) =>
        new()
        {
            Id = EntryId.Create(),
            ProjectId = project ?? Website,
            Date = day,
            Start = Clocks.At(start),
            Duration = TimeSpan.FromMinutes(minutes),
        };

    [Fact]
    public void The_total_covers_only_the_day_asked_about()
    {
        var state = Ledger();

        Assert.Equal(TimeSpan.FromMinutes(120), state.TotalOn(Day));
        Assert.Equal(TimeSpan.FromMinutes(60), state.TotalOn(OtherDay));
        Assert.Equal(TimeSpan.Zero, state.TotalOn(new DateOnly(2026, 11, 5)));
    }

    [Fact]
    public void A_day_lists_its_entries_in_start_order()
    {
        var state = Ledger();

        var starts = state.EntriesOn(Day).Select(entry => Clocks.Text(entry.Start));

        Assert.Equal(new[] { "09:00", "10:30" }, starts);
    }

    [Fact]
    public void A_retracted_entry_leaves_the_total()
    {
        var state = Ledger();
        var entry = state.EntriesOn(Day).First();

        state.Apply(new EntryRetracted(entry));

        Assert.Equal(TimeSpan.FromMinutes(30), state.TotalOn(Day));
    }

    [Fact]
    public void A_break_is_time_on_the_day_without_being_time_worked()
    {
        var state = Ledger();
        state.Apply(new ProjectOpened(Pause, Break.Project));
        state.Apply(new EntryLogged(EntryAt(OtherDay, "09:00", 30, Pause)));

        Assert.Equal(TimeSpan.FromMinutes(90), state.TotalOn(OtherDay));
        Assert.Equal(TimeSpan.FromMinutes(60), state.WorkedOn(OtherDay));
    }

    [Fact]
    public void A_day_with_no_break_is_worth_its_total_as_work()
    {
        var state = Ledger();

        Assert.Equal(state.TotalOn(Day), state.WorkedOn(Day));
    }

    [Fact]
    public void A_day_stops_where_its_last_entry_ends() =>
        Assert.Equal(Clocks.At("11:00"), Ledger().EndOfLast(Day));

    [Fact]
    public void A_day_with_nothing_on_it_has_no_end_to_continue_from() =>
        Assert.Null(Ledger().EndOfLast(new DateOnly(2026, 11, 5)));

    [Fact]
    public void A_day_that_ends_at_midnight_has_no_end_to_continue_from()
    {
        var state = LedgerState.Create();
        state.Apply(new EntryLogged(EntryAt(Day, "22:00", 120)));

        Assert.Null(state.EndOfLast(Day));
    }

    [Fact]
    public void A_project_is_found_by_its_name_in_any_case()
    {
        var match = Ledger().MatchProject("  webshop ");

        Assert.True(match.Found);
        Assert.Equal("Webshop", match.Value!.Name);
    }

    [Fact]
    public void A_prefix_that_fits_one_project_finds_it()
    {
        var match = Ledger().MatchProject("Websi");

        Assert.Equal("Website", match.Value!.Name);
    }

    [Fact]
    public void A_prefix_that_fits_two_projects_is_ambiguous()
    {
        var match = Ledger().MatchProject("Web");

        Assert.False(match.Found);
        Assert.True(match.Ambiguous);
    }

    [Fact]
    public void A_topic_is_resolved_inside_its_own_project_only()
    {
        var state = Ledger();

        Assert.True(state.MatchTopic(Website, "log").Found);
        Assert.False(state.MatchTopic(Webshop, "log").Found);
    }

    [Fact]
    public void An_exact_name_beats_a_longer_name_that_starts_with_it()
    {
        var state = LedgerState.Create();
        state.Apply(new ProjectOpened(Guid.NewGuid(), "Web"));
        state.Apply(new ProjectOpened(Guid.NewGuid(), "Website"));

        Assert.Equal("Web", state.MatchProject("web").Value!.Name);
        Assert.Equal("Website", state.MatchProject("websi").Value!.Name);
    }

    [Fact]
    public void An_empty_name_matches_nothing_and_is_not_ambiguous()
    {
        var match = Ledger().MatchProject("   ");

        Assert.False(match.Found);
        Assert.False(match.Ambiguous);
    }

    [Fact]
    public void An_unknown_project_has_no_name()
    {
        Assert.Equal("?", Ledger().ProjectName(Guid.NewGuid()));
        Assert.Null(Ledger().TopicName(Guid.NewGuid()));
    }

    [Fact]
    public void An_unknown_change_is_refused_rather_than_ignored()
    {
        var applying = () => LedgerState.Create().Apply(new UnknownChange());

        Assert.Throws<NotSupportedException>(applying);
    }

    [Fact]
    public void A_snapshot_restores_the_same_state_and_version()
    {
        var original = Ledger();

        var restored = LedgerState.Restore(original.ToSnapshot());

        Assert.Equal(original.Version, restored.Version);
        Assert.Equal(original.TotalOn(Day), restored.TotalOn(Day));
        Assert.Equal(original.Projects.Keys.Order(), restored.Projects.Keys.Order());
        Assert.Equal("Website", restored.ProjectName(Website));
    }

    [Fact]
    public void A_declared_kind_of_day_survives_a_snapshot()
    {
        var original = Ledger();
        original.Apply(new DayKindChanged(Day, null, DayKind.Holiday));

        var restored = LedgerState.Restore(original.ToSnapshot());

        Assert.Equal(DayKind.Holiday, restored.KindOf(Day));
        Assert.Equal(DayKind.Working, restored.KindOf(new DateOnly(2026, 11, 5)));
    }

    private sealed record UnknownChange : LedgerEvent;
}
