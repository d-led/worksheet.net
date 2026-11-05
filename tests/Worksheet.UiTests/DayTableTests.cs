// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Worksheet.Core;
using Worksheet.Desktop.Components;
using Worksheet.Desktop.ViewModels;

namespace Worksheet.UiTests;

public sealed class DayTableTests : UiTestBase
{
    private static List<Border> EntryRows(Visual root) =>
        [.. Descendants<Border>(root).Where(border => border.Classes.Contains("row"))];

    private static List<Border> GapRows(Visual root) =>
        [.. Descendants<Border>(root).Where(border => border.Classes.Contains("gap"))];

    private static TextBox Cell(Border row, string name) =>
        Descendants<TextBox>(row).Single(box => box.Classes.Contains(name));

    private void Enter()
    {
        Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Settle();
    }

    /// <summary>A day holding these lines, shown as a table and settled.</summary>
    private DayTable Table(params string[] lines)
    {
        var day = NewDay();

        foreach (var line in lines)
        {
            Seed(day, line);
        }

        var table = Show<DayTable>(day);
        Settle();

        return table;
    }

    [AvaloniaFact]
    public void The_table_shows_a_start_and_an_end_per_entry_and_nothing_else()
    {
        var day = NewDay();
        Seed(day, "Website 09:00 1h30m");

        var table = Show<DayTable>(day);
        Settle();

        var row = Assert.Single(EntryRows(table));
        Assert.Equal("09:00", Cell(row, "start-input").Text);
        Assert.Equal("10:30", Cell(row, "end-input").Text);
    }

    [AvaloniaFact]
    public void Ten_entries_fit_in_a_window_without_scrolling()
    {
        var day = NewDay();

        for (var index = 0; index < 10; index++)
        {
            Seed(day, $"Website {9 + index:00}:00 45m");
        }

        var table = Show<DayTable>(day);
        Settle();

        var rows = EntryRows(table);
        Assert.Equal(10, rows.Count);

        // A row may not be so tall that a working day stops fitting on a screen.
        Assert.All(rows, row => Assert.True(row.Bounds.Height <= 40, $"a row is {row.Bounds.Height} high"));
    }

    [AvaloniaFact]
    public void A_row_carries_its_projects_pastel_and_a_break_carries_none()
    {
        var rows = EntryRows(Table("Website 09:00 1h", "Break 10:00 30m"));

        Assert.NotNull(Stripe(rows[0]));
        Assert.Null(Stripe(rows[1]));
    }

    private static IBrush? Stripe(Border row) =>
        Descendants<Border>(row).Single(border => border.Classes.Contains("tint")).Background;

    [AvaloniaFact]
    public void A_hole_between_entries_is_visible_with_its_length()
    {
        var gap = Assert.Single(GapRows(Table("Website 09:00-10:00", "Mobile 11:00-12:00")));
        var texts = Descendants<TextBlock>(gap).Select(block => block.Text).ToList();

        Assert.Contains("10:00 – 11:00", texts);
        Assert.Contains("1h", texts);
    }

    [AvaloniaFact]
    public void Clicking_fill_puts_the_hole_times_in_the_entry_line()
    {
        var day = NewDay();
        Seed(day, "Website 09:00-10:00");
        Seed(day, "Mobile 11:00-12:00");

        var table = Show<DayTable>(day);
        Settle();

        Click(Descendants<Button>(Assert.Single(GapRows(table))).Single(button => (button.Content as string) == "＋"));

        Assert.Equal("Website 10:00-11:00", day.EntryText);
    }

    [AvaloniaFact]
    public void The_empty_state_is_visible_only_while_the_day_is_empty()
    {
        var day = NewDay();
        var table = Show<DayTable>(day);

        var notice = Descendants<TextBlock>(table).Single(block => block.Classes.Contains("quiet"));
        Assert.True(notice.IsVisible);

        Seed(day, "Website 09:00 1h");

        Assert.False(notice.IsVisible);
    }

    [AvaloniaFact]
    public void A_row_offers_the_known_projects_and_the_topics_of_its_own_project()
    {
        var day = NewDay();
        Seed(day, "Website (login) 09:00 1h");
        Seed(day, "Mobile (checkout) 10:00 1h");

        var table = Show<DayTable>(day);
        Settle();

        var row = EntryRows(table)[0];
        var projectOffers = Descendants<AutoCompleteBox>(row).First();
        var topicOffers = Descendants<AutoCompleteBox>(row).Last();

        Assert.Equal(new[] { "Mobile", "Website" }, projectOffers.ItemsSource!.Cast<string>());
        Assert.Equal(new[] { "login" }, topicOffers.ItemsSource!.Cast<string>());
    }

    [AvaloniaFact]
    public void Typing_a_project_nobody_used_yet_in_a_row_opens_it()
    {
        var day = NewDay();
        Seed(day, "Website 09:00 1h");

        var table = Show<DayTable>(day);
        Settle();

        var row = Assert.Single(EntryRows(table));
        var projectBox = Descendants<AutoCompleteBox>(row).First();

        projectBox.Focus();
        projectBox.Text = "Client portal";
        Cell(row, "note-input").Focus();
        Settle();

        var entry = Assert.Single(Ledger.Ledger.State.Entries).Value;
        Assert.Equal("Client portal", Ledger.Ledger.State.ProjectName(entry.ProjectId));
    }

    [AvaloniaFact]
    public void A_break_row_keeps_its_topic_box_closed()
    {
        var day = NewDay();
        Seed(day, "Break 12:30 45m");

        var table = Show<DayTable>(day);
        Settle();

        var topicBox = Descendants<AutoCompleteBox>(Assert.Single(EntryRows(table))).Last();

        Assert.False(topicBox.IsEffectivelyEnabled);
    }

    [AvaloniaFact]
    public void Clicking_retract_takes_the_entry_off_the_day()
    {
        var day = NewDay();
        Seed(day, "Website 09:00 1h");

        var table = Show<DayTable>(day);
        Settle();

        Click(Descendants<Button>(Assert.Single(EntryRows(table))).Single(button => button.Classes.Contains("row-action")));

        Assert.Empty(day.Rows);
    }

    [AvaloniaFact]
    public void Typing_a_note_in_a_row_saves_it_on_the_entry()
    {
        var day = NewDay();
        Seed(day, "Website 09:00 1h");

        var table = Show<DayTable>(day);
        Settle();

        var noteBox = Cell(Assert.Single(EntryRows(table)), "note-input");
        noteBox.Text = "called the client";
        Settle();

        Assert.Equal("called the client", Assert.Single(Ledger.Ledger.State.Entries).Value.Note);
    }

    [AvaloniaFact]
    public void Typing_an_end_in_a_row_changes_the_block()
    {
        var day = NewDay();
        Seed(day, "Website 09:00 1h");

        var table = Show<DayTable>(day);
        Settle();

        var row = Assert.Single(EntryRows(table));
        var endBox = Cell(row, "end-input");
        var noteBox = Cell(row, "note-input");

        endBox.Focus();
        endBox.Text = "11:15";
        noteBox.Focus();
        Settle();

        Assert.Equal(TimeSpan.FromMinutes(135), Assert.Single(Ledger.Ledger.State.Entries).Value.Duration);
        Assert.Equal("2h15m", day.Total);
    }

    [AvaloniaFact]
    public void Pressing_enter_in_a_clock_field_takes_the_time_typed()
    {
        var day = NewDay();
        Seed(day, "Website 09:00 1h");

        var table = Show<DayTable>(day);
        Settle();

        var endBox = Cell(Assert.Single(EntryRows(table)), "end-input");
        endBox.Focus();
        endBox.Text = "11:15";

        Enter();

        Assert.Equal(TimeSpan.FromMinutes(135), Assert.Single(Ledger.Ledger.State.Entries).Value.Duration);
    }

    [AvaloniaFact]
    public void Pressing_enter_in_a_project_field_takes_the_name_typed()
    {
        var day = NewDay();
        Seed(day, "Website 09:00 1h");

        var table = Show<DayTable>(day);
        Settle();

        var projectBox = Descendants<AutoCompleteBox>(Assert.Single(EntryRows(table))).First();
        projectBox.Focus();
        Settle();
        Window.KeyTextInput("Client portal");
        Settle();

        Enter();

        var entry = Assert.Single(Ledger.Ledger.State.Entries).Value;
        Assert.Equal("Client portal", Ledger.Ledger.State.ProjectName(entry.ProjectId));

        // The row is built again from the journal, and shows the name the key took.
        var restored = Descendants<AutoCompleteBox>(Assert.Single(EntryRows(table))).First();
        Assert.Equal("Client portal", restored.Text);
    }

    [AvaloniaFact]
    public void A_start_that_is_not_a_clock_puts_the_field_back()
    {
        var day = NewDay();
        Seed(day, "Website 09:00 1h");

        var table = Show<DayTable>(day);
        Settle();

        var row = Assert.Single(EntryRows(table));
        var startBox = Cell(row, "start-input");
        var noteBox = Cell(row, "note-input");

        startBox.Focus();
        startBox.Text = "quarter past";
        noteBox.Focus();
        Settle();

        // The refused row is rebuilt from the journal, so the field shows what was stored.
        var restored = Cell(Assert.Single(EntryRows(table)), "start-input");
        Assert.Equal("09:00", restored.Text);
        Assert.Equal(new TimeOnly(9, 0), Assert.Single(Ledger.Ledger.State.Entries).Value.Start);
    }

    [AvaloniaFact]
    public void Clicking_break_turns_the_hole_into_an_entry_with_a_comment()
    {
        var day = NewDay();
        Seed(day, "Website 09:00-10:00");
        Seed(day, "Mobile 11:00-12:00");

        var table = Show<DayTable>(day);
        Settle();

        Click(Descendants<Button>(Assert.Single(GapRows(table))).Single(button => (button.Content as string) == "Break"));
        Settle();

        var rows = EntryRows(table);
        Assert.Equal(3, rows.Count);
        Assert.Empty(GapRows(table));
        Assert.Equal("break", Cell(rows[1], "note-input").Text);
    }

    [AvaloniaFact]
    public void A_frozen_day_keeps_its_rows_and_offers_no_change()
    {
        var day = NewDay();
        Seed(day, "Website 09:00-10:00");
        Seed(day, "Mobile 11:00-12:00");
        day.Kinds.Single(option => option.Kind == DayKind.Holiday).IsSelected = true;

        var table = Show<DayTable>(day);
        Settle();

        Assert.All(EntryRows(table), row => Assert.False(Cell(row, "start-input").IsEffectivelyEnabled));
        Assert.All(
            Descendants<Button>(Assert.Single(GapRows(table))),
            button => Assert.False(button.IsEffectivelyVisible));
    }
}
