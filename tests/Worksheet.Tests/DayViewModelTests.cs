// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Globalization;
using Worksheet.Core;
using Worksheet.Desktop.ViewModels;
using Worksheet.Storage;
using Worksheet.TestSupport;

namespace Worksheet.Tests;

/// <summary>
/// The day as the window sees it: a line becomes a row, the row shows a start and an end, the
/// holes between rows are visible, and the line is checked while it is still being typed.
/// </summary>
public sealed class DayViewModelTests : IDisposable
{
    /// <summary>A Tuesday: the tests are about the day on screen, never about when they run.</summary>
    private static readonly DateOnly Today = new(2026, 11, 3);

    private static readonly DateOnly Saturday = new(2026, 11, 7);

    private readonly TempDirectory _temp = new("worksheet-view");
    private readonly LocalLedger _local;

    public DayViewModelTests() => _local = LocalLedger.Open(_temp.Path, SnapshotPolicy.Disabled);

    public void Dispose()
    {
        _local.Dispose();
        _temp.Dispose();
    }

    private DayViewModel DayWith(params string[] lines)
    {
        var day = new DayViewModel(_local, Today);

        foreach (var line in lines)
        {
            day.EntryText = line;
            day.LogCommand.Execute(null);
        }

        return day;
    }

    private static EntryRowViewModel EntryRow(DayViewModel day) => (EntryRowViewModel)day.Rows[0];

    private static IEnumerable<GapRowViewModel> Gaps(DayViewModel day) => day.Rows.OfType<GapRowViewModel>();

    [Fact]
    public void The_day_is_worded_in_the_interfaces_language_whatever_the_machine_speaks()
    {
        var before = CultureInfo.CurrentCulture;

        try
        {
            // A German machine used to write "Donnerstag" in front of the English word "Today".
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            var day = new DayViewModel(_local, Today);
            Assert.Equal("Today, 3 November", day.DayTitle);

            day.SelectedDate = Today.AddDays(1).ToDateTime(TimeOnly.MinValue);
            Assert.Equal("Wednesday 4 November", day.DayTitle);
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    [Fact]
    public void A_day_shows_what_a_writer_outside_the_window_logged()
    {
        var day = DayWith("Website 09:00 1h");

        new EntryComposer(_local.Ledger).Compose(
            new EntryCommand(Today, "Mobile", Topic: null, new TimeOnly(10, 0), TimeSpan.FromHours(1), Note: null));

        Assert.Equal(2, day.Rows.Count);
        Assert.Equal("2h", day.Total);
    }

    [Fact]
    public void A_write_on_another_day_leaves_the_rows_a_person_is_typing_in_alone()
    {
        var day = DayWith("Website 09:00 1h");
        var shown = Assert.Single(day.Rows);

        new EntryComposer(_local.Ledger).Compose(
            new EntryCommand(Today.AddDays(1), "Website", Topic: null, new TimeOnly(10, 0), TimeSpan.FromHours(1), Note: null));

        Assert.Same(shown, Assert.Single(day.Rows));
    }

    [Fact]
    public void A_project_renamed_behind_the_window_reaches_the_rows()
    {
        var day = DayWith("Website 09:00 1h");

        Assert.True(new Catalogue(_local.Ledger).RenameProject("Website", "Client portal").Accepted);

        Assert.Contains("Client portal", day.Projects);
        Assert.Equal("Client portal", EntryRow(day).ProjectText);
    }

    [Fact]
    public void The_window_can_say_what_went_wrong_at_start_up()
    {
        var day = DayWith();

        day.Say("Port 5317 is taken, so the MCP server is not running.", isProblem: true);

        Assert.True(day.MessageIsProblem);
        Assert.Equal("Port 5317 is taken, so the MCP server is not running.", day.Message);
    }

    [Fact]
    public void A_line_becomes_a_row_showing_absolute_times()
    {
        var day = DayWith("Website (login) 09:00 1h30m");

        var row = Assert.IsType<EntryRowViewModel>(Assert.Single(day.Rows));
        Assert.Equal("09:00", row.StartText);
        Assert.Equal("10:30", row.EndText);
        Assert.Equal("Website", row.ProjectText);
        Assert.Equal("login", row.TopicText);
        Assert.Equal("1h30m", day.Total);
        Assert.Equal(string.Empty, day.EntryText);
        Assert.False(day.HasMessage);
    }

    [Fact]
    public void Rows_are_in_start_order_and_the_holes_between_them_are_shown()
    {
        var day = DayWith("Mobile 10:30 1h", "Website 09:00 1h");

        Assert.Equal(3, day.Rows.Count);
        Assert.Equal("09:00", ((EntryRowViewModel)day.Rows[0]).StartText);

        var gap = Assert.Single(Gaps(day));
        Assert.Equal("10:00 – 10:30", gap.Span);
        Assert.Equal("30m", gap.Length);
        Assert.Equal("10:30", ((EntryRowViewModel)day.Rows[2]).StartText);
    }

    [Fact]
    public void Filling_a_gap_puts_its_times_into_the_line()
    {
        var day = DayWith("Website 09:00 1h", "Mobile 11:00 1h");

        Assert.Single(Gaps(day)).FillCommand.Execute(null);

        Assert.Equal("Website 10:00-11:00", day.EntryText);
        Assert.Contains("filled in", day.Message);
    }

    [Fact]
    public void Filling_the_day_line_leaves_a_valid_line()
    {
        var day = DayWith("Website 09:00 1h", "Mobile 11:00 1h");
        Assert.Single(Gaps(day)).FillCommand.Execute(null);

        day.LogCommand.Execute(null);

        Assert.Equal("3h", day.Total);
        Assert.Empty(Gaps(day));
    }

    [Fact]
    public void The_hint_says_what_to_write_next()
    {
        var day = new DayViewModel(_local, Today);

        Assert.Equal("next: project", day.NextStep);

        day.EntryText = "Web";
        Assert.Equal("next: topic in (brackets), optional", day.NextStep);

        day.EntryText = "Web (login) 09:00";
        Assert.Equal("next: length 1h30m, or an end 09:00-11:00", day.NextStep);

        day.EntryText = "Web (login) 09:00 1h";
        Assert.Equal("complete", day.NextStep);
    }

    [Fact]
    public void The_hint_offers_the_shorthands_once_the_day_has_an_end_to_continue_from()
    {
        var day = DayWith("Website 09:00 1h");

        day.EntryText = "Mobile (app) ";

        Assert.Equal("next: start 09:00, or from 10:00 \"till 11:30\" / \"for 1h30m\"", day.NextStep);
    }

    [Fact]
    public void A_line_that_says_till_is_logged_from_where_the_last_entry_ended()
    {
        var day = DayWith("Website 09:00 1h");

        day.EntryText = "Mobile (app) till 12:00";
        Assert.Equal("complete", day.NextStep);

        day.LogCommand.Execute(null);

        var row = (EntryRowViewModel)day.Rows[1];
        Assert.Equal("10:00", row.StartText);
        Assert.Equal("12:00", row.EndText);
        Assert.Equal("Mobile", row.ProjectText);
        Assert.Equal("app", row.TopicText);
    }

    [Fact]
    public void An_intersection_is_reported_while_typing_not_after_enter()
    {
        var day = DayWith("Website 09:00 2h");

        day.EntryText = "Mobile 10:00 1h";

        Assert.True(day.HasValidation);
        Assert.Equal("intersects Website 09:00-11:00", day.Validation);
    }

    [Fact]
    public void A_free_span_says_nothing_while_typing()
    {
        var day = DayWith("Website 09:00 2h");

        day.EntryText = "Mobile 11:00 1h";

        Assert.False(day.HasValidation);
        Assert.Null(day.Validation);
    }

    [Fact]
    public void A_half_written_line_is_not_complained_about()
    {
        var day = DayWith("Website 09:00 2h");

        day.EntryText = "Mob";

        Assert.False(day.HasValidation);
    }

    [Fact]
    public void Editing_the_end_revises_the_entry_instead_of_adding_one()
    {
        var day = DayWith("Website 09:00 1h");

        EntryRow(day).EndText = "10:30";

        Assert.Single(day.Rows);
        Assert.Equal("1h30m", day.Total);
        Assert.Equal(TimeSpan.FromMinutes(90), EntryRow(day).Snapshot.Duration);
    }

    [Fact]
    public void Editing_the_start_keeps_the_end_and_shortens_the_block()
    {
        var day = DayWith("Website 09:00 1h");

        EntryRow(day).StartText = "09:30";

        Assert.Equal("09:30", EntryRow(day).StartText);
        Assert.Equal("10:00", EntryRow(day).EndText);
        Assert.Equal("30m", day.Total);
    }

    [Fact]
    public void An_end_before_the_start_puts_the_fields_back()
    {
        var day = DayWith("Website 09:00 1h");

        EntryRow(day).EndText = "08:00";

        Assert.Equal("09:00", EntryRow(day).StartText);
        Assert.Equal("10:00", EntryRow(day).EndText);
        Assert.Equal("1h", day.Total);
    }

    [Fact]
    public void A_correction_that_would_intersect_another_entry_is_refused()
    {
        var day = DayWith("Website 09:00-10:00", "Mobile 11:00-12:00");
        var mobile = (EntryRowViewModel)day.Rows[^1];

        mobile.StartText = "09:30";

        Assert.True(day.MessageIsProblem);
        Assert.Equal("11:00", mobile.StartText);
        Assert.Equal("12:00", mobile.EndText);
        Assert.Equal("2h", day.Total);
    }

    [Fact]
    public void A_known_project_is_offered_while_typing()
    {
        var day = DayWith("Website 09:00 1h");

        day.EntryText = "Web";

        Assert.Equal(new[] { "Website" }, day.Suggestions.Select(suggestion => suggestion.Text));
    }

    [Fact]
    public void Taking_a_suggestion_completes_the_line_without_duplicating_the_project()
    {
        var day = DayWith("Website 09:00 1h");

        day.EntryText = "Web";

        day.Suggestions[0].Use.Execute(null);

        // The trailing space is where the time goes next.
        Assert.Equal("Website ", day.EntryText);
        Assert.Single(_local.Ledger.State.Projects);
    }

    [Fact]
    public void The_topics_of_the_project_are_offered_once_its_bracket_is_open()
    {
        var day = DayWith("Website (login) 09:00 1h", "Website (reports) 11:00 1h");

        day.EntryText = "Web";

        day.CompleteSuggestionCommand.Execute(null);

        Assert.Equal("Website (", day.EntryText);
        Assert.Equal(new[] { "login", "reports" }, day.Suggestions.Select(suggestion => suggestion.Text));
        Assert.Equal("TOPICS", day.SuggestionCaption);
    }

    [Fact]
    public void The_arrows_walk_the_offers_and_Tab_takes_the_one_they_are_on()
    {
        var day = DayWith("Website (login) 09:00 1h", "Webshop (orders) 11:00 1h");

        day.EntryText = "Web";
        Assert.Equal(new[] { "Webshop", "Website" }, day.Suggestions.Select(suggestion => suggestion.Text));
        Assert.True(day.Suggestions[0].IsSelected);

        day.NextSuggestionCommand.Execute(null);
        Assert.True(day.Suggestions[1].IsSelected);

        day.NextSuggestionCommand.Execute(null);
        Assert.True(day.Suggestions[0].IsSelected, "the walk wraps round");

        day.PreviousSuggestionCommand.Execute(null);
        Assert.True(day.Suggestions[1].IsSelected);

        day.CompleteSuggestionCommand.Execute(null);
        Assert.Equal("Website (", day.EntryText);
    }

    [Fact]
    public void Taking_a_topic_closes_the_bracket_and_the_hint_moves_on_to_the_start()
    {
        var day = DayWith("Website (login) 09:00 1h");

        day.EntryText = "Website (lo";
        Assert.Equal(new[] { "login" }, day.Suggestions.Select(suggestion => suggestion.Text));

        day.CompleteSuggestionCommand.Execute(null);

        Assert.Equal("Website (login) ", day.EntryText);
        Assert.False(day.HasSuggestions);
        Assert.Equal("next: start 09:00, or from 10:00 \"till 11:30\" / \"for 1h30m\"", day.NextStep);
    }

    [Fact]
    public void Undo_and_redo_move_the_day_and_flip_the_buttons()
    {
        var day = DayWith("Website 09:00 1h");
        Assert.True(day.CanUndo);
        Assert.False(day.CanRedo);

        day.UndoCommand.Execute(null);

        Assert.Empty(day.Rows);
        Assert.Equal("0m", day.Total);
        Assert.True(day.CanRedo);

        day.RedoCommand.Execute(null);

        Assert.Single(day.Rows);
        Assert.Equal("1h", day.Total);
        Assert.False(day.CanRedo);
    }

    [Fact]
    public void An_entry_written_after_an_undo_forgets_what_could_be_redone()
    {
        var day = DayWith("Website 09:00 1h");
        day.UndoCommand.Execute(null);
        Assert.True(day.CanRedo);

        day.EntryText = "Mobile 11:00 1h";
        day.LogCommand.Execute(null);

        Assert.False(day.CanRedo);
        Assert.Single(day.Rows);
        Assert.Equal("1h", day.Total);

        day.RedoCommand.Execute(null);
        Assert.Equal("There is nothing to redo.", day.Message);
        Assert.Single(day.Rows);
    }

    [Fact]
    public void Retracting_leaves_the_project_for_next_time()
    {
        var day = DayWith("Website (login) 09:00 1h");

        EntryRow(day).RetractCommand.Execute(null);

        Assert.Empty(day.Rows);
        Assert.Single(_local.Ledger.State.Projects);
        Assert.Single(_local.Ledger.State.Topics);
    }

    [Fact]
    public void The_calendar_moves_the_day_and_changes_what_is_shown()
    {
        var day = DayWith("Website 09:00 1h");

        day.PreviousDayCommand.Execute(null);

        Assert.Empty(day.Rows);
        Assert.Equal("0m", day.Total);

        day.NextDayCommand.Execute(null);

        Assert.Single(day.Rows);
    }

    [Fact]
    public void A_saturday_starts_as_a_weekend_and_is_frozen()
    {
        var day = new DayViewModel(_local, Saturday);

        Assert.True(day.IsFrozen);
        Assert.Equal("Weekend", day.Kinds.Single(option => option.IsSelected).Label);
        Assert.Contains("frozen", day.FrozenNote);
    }

    [Fact]
    public void Choosing_a_sick_day_freezes_what_is_already_on_it()
    {
        var day = DayWith("Website 09:00 1h");

        day.Kinds.Single(option => option.Kind == DayKind.Sick).IsSelected = true;

        Assert.True(day.IsFrozen);
        Assert.False(EntryRow(day).IsEditable);
        Assert.Equal(DayKind.Sick, _local.Ledger.State.KindOf(Today));
        Assert.Equal("1h", day.Total);
    }

    [Fact]
    public void A_frozen_day_refuses_a_new_line_and_says_why()
    {
        var day = DayWith();

        day.Kinds.Single(option => option.Kind == DayKind.Holiday).IsSelected = true;
        day.EntryText = "Website 09:00 1h";
        day.LogCommand.Execute(null);

        Assert.True(day.MessageIsProblem);
        Assert.Contains("is marked Holiday", day.Message);
        Assert.Empty(day.Rows);
    }

    [Fact]
    public void Working_a_saturday_back_makes_it_editable_again()
    {
        var day = new DayViewModel(_local, Saturday);

        day.Kinds.Single(option => option.Kind == DayKind.Working).IsSelected = true;
        day.EntryText = "Website 09:00 1h";
        day.LogCommand.Execute(null);

        Assert.False(day.IsFrozen);
        Assert.Equal("1h", day.Total);
    }

    [Fact]
    public void The_break_button_records_the_hole_as_a_break_with_a_note_to_change()
    {
        var day = DayWith("Website 09:00-10:00", "Mobile 11:00-12:00");

        Assert.Single(Gaps(day)).BreakCommand.Execute(null);

        Assert.Empty(Gaps(day));
        var taken = Assert.Single(
            _local.Ledger.State.Entries.Values,
            entry => entry.Start == new TimeOnly(10, 0));
        Assert.Equal("Break", _local.Ledger.State.ProjectName(taken.ProjectId));
        Assert.Equal(TimeSpan.FromHours(1), taken.Duration);
        Assert.Equal("break", taken.Note);
    }

    [Fact]
    public void A_break_row_has_no_topic_to_pick()
    {
        var day = DayWith("Break 12:30 45m");

        Assert.False(EntryRow(day).TopicPicker);
    }

    [Fact]
    public void A_clock_written_in_a_hurry_is_read_in_24_hours()
    {
        var day = DayWith("bla (test) 12:0 2h");

        var row = EntryRow(day);

        Assert.Equal("12:00", row.StartText);
        Assert.Equal("14:00", row.EndText);
        Assert.Equal("bla", row.ProjectText);
        Assert.Equal("test", row.TopicText);
    }

    [Fact]
    public void Filling_a_hole_keeps_the_topic_of_the_block_before_it()
    {
        var day = DayWith("Website (login) 09:00-10:00", "Mobile 11:00-12:00");

        Assert.Single(Gaps(day)).FillCommand.Execute(null);

        Assert.Equal("Website (login) 10:00-11:00", day.EntryText);
    }

    [Fact]
    public void Turning_a_row_into_a_break_drops_its_topic()
    {
        var day = DayWith("Website (login) 09:00 1h", "Break 13:00 15m");
        var row = EntryRow(day);

        row.ProjectText = "Break";

        Assert.False(row.TopicPicker);
        Assert.Equal(string.Empty, row.TopicText);
        Assert.Null(_local.Ledger.State.Entries[row.Id].TopicId);
    }
}
