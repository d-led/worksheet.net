// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;
using Worksheet.TestSupport;

namespace Worksheet.Tests;

public sealed class EntryComposerTests
{
    private static readonly DateOnly Day = new(2026, 11, 3);

    private static ComposeResult Compose(Ledger ledger, string line)
    {
        var parse = EntryCommandParser.Parse(line, Day);
        Assert.True(parse.Succeeded, parse.Error);

        return new EntryComposer(ledger).Compose(parse.Command!);
    }

    private static Ledger NewLedger(out InMemoryJournal journal)
    {
        journal = new InMemoryJournal();
        return Core.Ledger.Open(journal);
    }

    /// <summary>The day as it reads: every range with the work it holds, in start order.</summary>
    private static IReadOnlyList<string> OnTheDay(Ledger ledger) =>
    [
        .. ledger.State.EntriesOn(Day).Select(entry =>
            $"{Clocks.Text(entry.Start)}-{Clocks.Text(entry.End)} {LabelOf(ledger, entry)}"),
    ];

    /// <summary>A range as a person names the work on it.</summary>
    private static string LabelOf(Ledger ledger, Entry entry) =>
        ledger.State.TopicName(entry.TopicId) is { } topic
            ? $"{ledger.State.ProjectName(entry.ProjectId)} ({topic})"
            : ledger.State.ProjectName(entry.ProjectId);

    [Fact]
    public void An_unknown_project_and_topic_are_opened_as_part_of_the_entry()
    {
        var ledger = NewLedger(out _);

        Compose(ledger, "Website (login) 09:00 1h30m");

        Assert.Equal("Website", Assert.Single(ledger.State.Projects).Value.Name);
        Assert.Equal("login", Assert.Single(ledger.State.Topics).Value.Name);
        Assert.Equal(TimeSpan.FromMinutes(90), ledger.State.TotalOn(Day));
    }

    [Fact]
    public void A_project_named_in_another_case_keeps_its_original_name()
    {
        var ledger = NewLedger(out _);
        Compose(ledger, "Website (login) 09:00 1h");

        Compose(ledger, "WEBSITE (login) 11:00 1h");

        Assert.Equal("Website", Assert.Single(ledger.State.Projects).Value.Name);
        Assert.Equal(TimeSpan.FromMinutes(120), ledger.State.TotalOn(Day));
    }

    [Fact]
    public void A_unique_prefix_finds_the_project_without_opening_a_second_one()
    {
        var ledger = NewLedger(out _);
        Compose(ledger, "Website (login) 09:00 1h");

        Compose(ledger, "web 11:00 30m");

        Assert.Single(ledger.State.Projects);
        Assert.Equal(TimeSpan.FromMinutes(90), ledger.State.TotalOn(Day));
    }

    [Fact]
    public void An_ambiguous_project_is_refused_and_nothing_is_written()
    {
        var ledger = NewLedger(out var journal);
        Compose(ledger, "Website 09:00 1h");
        Compose(ledger, "Webshop 10:00 1h");
        var version = ledger.Version;

        var result = Compose(ledger, "Web 11:00 1h");

        Assert.False(result.Created);
        Assert.Equal("'Web' fits more than one project; write more of the name.", Assert.Single(result.Problems));
        Assert.Equal(version, journal.Version);
        Assert.Equal(TimeSpan.FromMinutes(120), ledger.State.TotalOn(Day));
    }

    [Fact]
    public void An_interruption_splits_the_work_it_crosses_and_takes_its_minutes()
    {
        var ledger = NewLedger(out _);
        Compose(ledger, "Client portal 09:00-12:00");

        var result = Compose(ledger, "Outage 11:00 30m");

        Assert.True(result.Created, string.Join(" ", result.Problems));
        Assert.Equal(["splits Client portal 09:00-12:00 into 09:00-11:00 and 11:30-12:00"], result.Yielded);
        Assert.Equal(
            ["09:00-11:00 Client portal", "11:00-11:30 Outage", "11:30-12:00 Client portal"],
            OnTheDay(ledger));
    }

    [Fact]
    public void Both_pieces_of_a_split_keep_the_note_the_work_had()
    {
        var ledger = NewLedger(out _);
        Compose(ledger, "Reading 09:00-12:00 # the paper");

        Compose(ledger, "Outage 11:00 30m");

        Assert.Equal(
            ["the paper", "the paper"],
            ledger.State.EntriesOn(Day).Where(entry => entry.Note is not null).Select(entry => entry.Note));
    }

    [Fact]
    public void A_line_with_no_length_takes_no_minutes_from_the_work_it_sits_in()
    {
        var ledger = NewLedger(out _);
        Compose(ledger, "Client portal 09:00-12:00");

        var result = Compose(ledger, "Outage 11:00");

        Assert.True(result.Created, string.Join(" ", result.Problems));
        Assert.Empty(result.Yielded);
        Assert.Equal(["09:00-12:00 Client portal", "11:00-11:00 Outage"], OnTheDay(ledger));
    }

    [Fact]
    public void Work_the_interruption_only_shortens_is_trimmed_and_not_split()
    {
        var ledger = NewLedger(out _);
        Compose(ledger, "Client portal 09:00-12:00");

        var result = Compose(ledger, "Outage 11:00-13:00");

        Assert.Equal(["trims Client portal 09:00-12:00 to 09:00-11:00"], result.Yielded);
        Assert.Equal(["09:00-11:00 Client portal", "11:00-13:00 Outage"], OnTheDay(ledger));
    }

    [Fact]
    public void Work_the_interruption_covers_whole_goes_off_the_day()
    {
        var ledger = NewLedger(out _);
        Compose(ledger, "Client portal 09:00 1h");

        var result = Compose(ledger, "Outage 08:00-12:00");

        Assert.Equal(["covers Client portal 09:00-10:00"], result.Yielded);
        Assert.Equal(["08:00-12:00 Outage"], OnTheDay(ledger));
    }

    [Fact]
    public void An_interruption_takes_every_block_of_a_stretch_it_crosses()
    {
        var ledger = NewLedger(out _);
        Compose(ledger, "Client portal 09:00 1h");
        Compose(ledger, "Mobile 10:00 1h");

        var result = Compose(ledger, "Outage 09:30 1h");

        Assert.Equal(
            ["trims Client portal 09:00-10:00 to 09:00-09:30", "trims Mobile 10:00-11:00 to 10:30-11:00"],
            result.Yielded);
        Assert.Equal(
            ["09:00-09:30 Client portal", "09:30-10:30 Outage", "10:30-11:00 Mobile"],
            OnTheDay(ledger));
    }

    [Fact]
    public void Work_the_line_does_not_cross_is_left_as_it_is()
    {
        var ledger = NewLedger(out _);
        Compose(ledger, "Client portal 09:00 1h");
        var untouched = Assert.Single(ledger.State.EntriesOn(Day));

        var result = Compose(ledger, "Mobile 11:00 1h");

        Assert.Empty(result.Yielded);
        Assert.Equal(untouched, ledger.State.EntriesOn(Day).First());
    }

    [Fact]
    public void Touching_entries_do_not_intersect()
    {
        var ledger = NewLedger(out _);
        Compose(ledger, "Website 09:00-11:00");

        var result = Compose(ledger, "Mobile 11:00-12:00");

        Assert.True(result.Created, string.Join(" ", result.Problems));
        Assert.Empty(result.Yielded);
        Assert.Equal(TimeSpan.FromHours(3), ledger.State.TotalOn(Day));
    }

    [Fact]
    public void An_entry_that_ends_where_another_starts_takes_nothing_from_it()
    {
        var ledger = NewLedger(out _);
        Compose(ledger, "Mobile 11:00-12:00");

        var result = Compose(ledger, "Website 09:00-11:00");

        Assert.Empty(result.Yielded);
        Assert.Equal(["09:00-11:00 Website", "11:00-12:00 Mobile"], OnTheDay(ledger));
    }

    [Fact]
    public void A_start_with_no_length_yet_is_not_taken_by_a_line_that_runs_past_it()
    {
        var ledger = NewLedger(out _);
        Compose(ledger, "Website 10:00");

        var result = Compose(ledger, "Outage 09:00-11:00");

        Assert.Empty(result.Yielded);
        Assert.Equal(["09:00-11:00 Outage", "10:00-10:00 Website"], OnTheDay(ledger));
    }

    [Fact]
    public void A_note_lands_on_the_stretch_that_ends_last()
    {
        var ledger = NewLedger(out _);
        Compose(ledger, "Client portal 09:00 1h");
        Compose(ledger, "Mobile 10:00 5h");

        Assert.True(new EntryComposer(ledger).Note(Day, "the paper").Created);

        Assert.Equal(Clocks.At("10:00"), ledger.State.EntriesOn(Day).Single(entry => entry.Note is not null).Start);
    }

    [Fact]
    public void A_correction_that_lands_inside_another_block_splits_it_around_itself()
    {
        var ledger = NewLedger(out _);
        Compose(ledger, "Website 09:00-10:00");
        Compose(ledger, "Mobile 11:00-12:00");
        var mobile = ledger.State.EntriesOn(Day).Last();

        var result = new EntryComposer(ledger).Revise(
            mobile,
            mobile with { Start = Clocks.At("09:30"), Duration = TimeSpan.FromMinutes(15) });

        Assert.True(result.Created, string.Join(" ", result.Problems));
        Assert.Equal(["splits Website 09:00-10:00 into 09:00-09:30 and 09:45-10:00"], result.Yielded);
        Assert.Equal(
            ["09:00-09:30 Website", "09:30-09:45 Mobile", "09:45-10:00 Website"],
            OnTheDay(ledger));
    }

    /// <summary>The whole of one decision goes back on one undo, and comes forward again on one redo.</summary>
    private static void UndoAndRedo(Ledger ledger, IReadOnlyList<Entry> was, IReadOnlyList<Entry> now)
    {
        Assert.True(ledger.TryUndo(out var undoReason), undoReason);
        Assert.Equal(was, ledger.State.EntriesOn(Day));

        Assert.True(ledger.TryRedo(out var redoReason), redoReason);
        Assert.Equal(now, ledger.State.EntriesOn(Day));
    }

    [Fact]
    public void One_undo_takes_an_interruption_and_the_split_it_made_back_together()
    {
        var ledger = NewLedger(out var journal);
        Compose(ledger, "Outage 08:00 15m");
        Compose(ledger, "Client portal 09:00-12:00");
        var was = ledger.State.EntriesOn(Day).ToArray();
        var version = journal.Version;

        Compose(ledger, "Outage 11:00 30m");

        Assert.Equal(version + 1, journal.Version);
        UndoAndRedo(ledger, was, ledger.State.EntriesOn(Day).ToArray());
    }

    [Fact]
    public void One_undo_takes_a_correction_and_the_work_it_moved_back_together()
    {
        var ledger = NewLedger(out var journal);
        Compose(ledger, "Website 09:00-10:00");
        Compose(ledger, "Mobile 11:00-12:00");
        var was = ledger.State.EntriesOn(Day).ToArray();
        var version = journal.Version;

        var corrected = new EntryComposer(ledger).Revise(was[^1], was[^1] with { Start = Clocks.At("09:30") });

        Assert.True(corrected.Created, string.Join(" ", corrected.Problems));
        Assert.Equal(version + 1, journal.Version);
        UndoAndRedo(ledger, was, ledger.State.EntriesOn(Day).ToArray());
    }

    [Fact]
    public void A_correction_that_butts_up_against_the_next_entry_is_taken()
    {
        var ledger = NewLedger(out _);
        Compose(ledger, "Website 09:00-10:00");
        Compose(ledger, "Mobile 11:00-12:00");
        var website = ledger.State.EntriesOn(Day).First();

        var result = new EntryComposer(ledger).Revise(website, website with { Duration = TimeSpan.FromHours(2) });

        Assert.True(result.Created, string.Join(" ", result.Problems));
        Assert.Equal(TimeSpan.FromHours(3), ledger.State.TotalOn(Day));
    }

    [Fact]
    public void A_negative_duration_is_refused_and_nothing_is_written()
    {
        var ledger = NewLedger(out var journal);
        var command = new EntryCommand(Day, "Website", null, new TimeOnly(9, 0), TimeSpan.FromMinutes(-30), null);

        var result = new EntryComposer(ledger).Compose(command);

        Assert.Equal("A duration cannot be negative.", Assert.Single(result.Problems));
        Assert.Empty(journal.ReadFrom(0));
    }

    [Fact]
    public void An_ambiguous_topic_is_refused_and_nothing_is_written()
    {
        var ledger = NewLedger(out var journal);
        Compose(ledger, "Website (login) 09:00 1h");
        Compose(ledger, "Website (logout) 10:00 1h");
        var version = ledger.Version;

        var result = Compose(ledger, "Website (log) 11:00 1h");

        Assert.False(result.Created);
        Assert.Equal("'log' fits more than one topic of this project; write more of the name.", Assert.Single(result.Problems));
        Assert.Equal(version, journal.Version);
    }

    [Fact]
    public void An_entry_may_end_exactly_at_midnight()
    {
        var ledger = NewLedger(out _);

        var result = Compose(ledger, "Late 23:00 1h");

        Assert.True(result.Created, string.Join(" ", result.Problems));
        Assert.Equal(new TimeOnly(0, 0), result.Entry!.End);
        Assert.Equal(TimeSpan.FromMinutes(60), ledger.State.TotalOn(Day));
    }

    [Fact]
    public void A_range_that_runs_past_midnight_is_refused_and_writes_nothing()
    {
        var ledger = NewLedger(out var journal);

        var result = Compose(ledger, "Late 23:00 2h");

        Assert.False(result.Created);
        Assert.Equal(
            "23:00 + 2h runs past midnight; split it into two entries.",
            Assert.Single(result.Problems));
        Assert.Empty(journal.ReadFrom(0));
        Assert.Empty(ledger.State.Projects);
    }

    [Fact]
    public void Revising_an_entry_changes_it_in_place()
    {
        var ledger = NewLedger(out _);
        var composed = Compose(ledger, "Website (login) 09:00 1h");
        var before = composed.Entry!;

        var result = new EntryComposer(ledger).Revise(before, before with { Duration = TimeSpan.FromMinutes(120) });

        Assert.True(result.Created);
        var entry = Assert.Single(ledger.State.Entries).Value;
        Assert.Equal(before.Id, entry.Id);
        Assert.Equal(TimeSpan.FromMinutes(120), ledger.State.TotalOn(Day));
    }

    [Fact]
    public void Revising_can_move_an_entry_to_another_project()
    {
        var ledger = NewLedger(out _);
        Compose(ledger, "Webshop 08:00 30m");
        var before = Compose(ledger, "Website (login) 09:00 1h").Entry!;
        var webshop = ledger.State.MatchProject("Webshop").Value!;

        var result = new EntryComposer(ledger).Revise(before, before with { ProjectId = webshop.Id, TopicId = null });

        Assert.True(result.Created, string.Join(" ", result.Problems));
        Assert.Equal(webshop.Id, ledger.State.Entries[before.Id].ProjectId);
        Assert.Null(ledger.State.Entries[before.Id].TopicId);
        Assert.Equal(TimeSpan.FromMinutes(90), ledger.State.TotalOn(Day));
    }

    [Fact]
    public void Revising_refuses_a_range_that_runs_past_midnight()
    {
        var ledger = NewLedger(out _);
        var before = Compose(ledger, "Website 09:00 1h").Entry!;

        var result = new EntryComposer(ledger).Revise(
            before,
            before with { Start = new TimeOnly(23, 0), Duration = TimeSpan.FromHours(3) });

        Assert.False(result.Created);
        Assert.Equal(
            "23:00 + 3h runs past midnight; split it into two entries.",
            Assert.Single(result.Problems));
        Assert.Equal(new TimeOnly(9, 0), ledger.State.Entries[before.Id].Start);
    }

    [Fact]
    public void Retracting_an_entry_takes_it_off_the_day()
    {
        var ledger = NewLedger(out _);
        var composed = Compose(ledger, "Website 09:00 1h");

        var retracted = new EntryComposer(ledger).Retract(composed.Entry!.Id);

        Assert.True(retracted.Created);
        Assert.Equal(TimeSpan.Zero, ledger.State.TotalOn(Day));
        Assert.Single(ledger.State.Projects);
    }

    [Fact]
    public void A_day_that_does_not_work_takes_no_entries_and_says_why()
    {
        var ledger = NewLedger(out var journal);
        new DayCalendar(ledger).Declare(Day, DayKind.Sick);
        var version = ledger.Version;

        var result = Compose(ledger, "Website 09:00 1h");

        Assert.False(result.Created);
        Assert.Contains("is marked Sick day", Assert.Single(result.Problems));
        Assert.Equal(version, journal.Version);
    }

    [Fact]
    public void A_break_has_no_topic()
    {
        var ledger = NewLedger(out var journal);
        var version = ledger.Version;

        var result = Compose(ledger, "Break (pause) 12:30 45m");

        Assert.False(result.Created);
        Assert.Equal("A break has no topic; leave the brackets out.", Assert.Single(result.Problems));
        Assert.Equal(version, journal.Version);
        Assert.Empty(ledger.State.Topics);
    }

    [Fact]
    public void A_break_without_brackets_is_an_ordinary_entry()
    {
        var ledger = NewLedger(out _);

        var result = Compose(ledger, "Break 12:30 45m");

        Assert.True(result.Created, string.Join(" ", result.Problems));
        Assert.Null(result.Entry!.TopicId);
        Assert.Equal("Break", ledger.State.ProjectName(result.Entry.ProjectId));
    }
}
