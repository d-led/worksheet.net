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

namespace Worksheet.UiTests;

/// <summary>
/// The day as it was really drawn. Skia lays the text out and paints the frame, so these are tests
/// about what a person sees rather than about what the control tree holds — and every one of them
/// keeps its picture under <c>artifacts/ui-tests</c>, where a build can show it.
/// </summary>
public sealed class RenderedDayTests : UiTestBase
{
    private DayTable DayOnScreen(params string[] lines)
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

    private static Border Stripe(Border row) =>
        Descendants<Border>(row).Single(border => border.Classes.Contains("tint"));

    private static Color ColourOf(string? tint) => Color.Parse(tint ?? throw new InvalidOperationException("no tint"));

    [AvaloniaFact]
    public void Every_row_is_drawn_with_its_projects_colour_down_its_left_edge()
    {
        var table = DayOnScreen("Website 09:00 1h", "Mobile 10:00 1h", "Break 11:00 30m");

        using var frame = Shot("day-rows");

        var rows = EntryRows(table);
        Assert.Equal(3, rows.Count);

        var (websiteX, websiteY) = OnTheEdge(rows[0]);
        var (mobileX, mobileY) = OnTheEdge(rows[1]);

        Assert.Equal(ColourOf(Tints.Of(Ledger.Ledger.State.Catalogue, "Website")), frame.At(websiteX, websiteY));
        Assert.Equal(ColourOf(Tints.Of(Ledger.Ledger.State.Catalogue, "Mobile")), frame.At(mobileX, mobileY));
    }

    [AvaloniaFact]
    public void A_break_is_marked_in_grey_at_its_edge_rather_than_in_a_projects_colour()
    {
        var table = DayOnScreen("Website 09:00 1h", "Break 10:00 30m");
        var rows = EntryRows(table);

        using var frame = Shot("break-row");

        var (projectX, projectY) = OnTheEdge(rows[0]);
        var (breakX, breakY) = OnTheEdge(rows[1]);

        // A project's edge is its pastel; a pause has no colour of its own, so its edge is a grey mark.
        Assert.True(Chroma(frame.At(projectX, projectY)) > 24, $"a project's edge is its tint: {frame.At(projectX, projectY)}");
        Assert.True(Chroma(frame.At(breakX, breakY)) < 16, $"a break's edge is grey: {frame.At(breakX, breakY)}");
    }

    /// <summary>How far a colour is from grey: the spread between its widest and narrowest channel.</summary>
    private static int Chroma(Color colour) =>
        Math.Max(colour.R, Math.Max(colour.G, colour.B)) - Math.Min(colour.R, Math.Min(colour.G, colour.B));

    /// <summary>A point inside the stripe: its middle, which the rounded ends cannot reach.</summary>
    private (int X, int Y) OnTheEdge(Border row)
    {
        var stripe = Stripe(row);
        var centre = stripe.TranslatePoint(new Point(stripe.Bounds.Width / 2, stripe.Bounds.Height / 2), Window)
            ?? throw new InvalidOperationException("The stripe is not laid out.");

        return ((int)Math.Round(centre.X), (int)Math.Round(centre.Y));
    }

    [AvaloniaFact]
    public void The_note_a_person_typed_is_written_on_the_glass()
    {
        var table = DayOnScreen("Website 09:00 1h", "Mobile 10:00 1h");
        var rows = EntryRows(table);

        var typed = Cell(rows[0], "note-input");
        typed.Focus();
        Window.KeyTextInput("called the client about the invoice");
        Cell(rows[1], "note-input").Focus();
        Settle();

        using var frame = Shot("typed-note");

        // The row with a note keeps its words on the glass; the one without shows only the dash of
        // its empty state, so its ink does not reach nearly as far across the cell.
        var written = frame.InkExtent(AreaOf(rows[0], "note-input"));
        var empty = frame.InkExtent(AreaOf(rows[1], "note-input"));

        Assert.True(written > empty + 40, $"a note covers {written}px of its cell, an empty one {empty}px");
    }

    /// <summary>A field of a row as the frame sees it: the window's pixels, not the control's.</summary>
    private PixelRect AreaOf(Border row, string name)
    {
        var box = Cell(row, name);
        var topLeft = box.TranslatePoint(new Point(0, 0), Window)
            ?? throw new InvalidOperationException("The field is not laid out.");

        return new PixelRect(
            (int)Math.Round(topLeft.X),
            (int)Math.Round(topLeft.Y),
            (int)Math.Round(box.Bounds.Width),
            (int)Math.Round(box.Bounds.Height));
    }

    [AvaloniaFact]
    public void A_frozen_row_is_still_written_out_readably()
    {
        var day = NewDay();
        Seed(day, "Website 09:00 1h");
        day.Kinds.Single(option => option.Kind == DayKind.Sick).IsSelected = true;

        var table = Show<DayTable>(day);
        Settle();

        using var frame = Shot("frozen-day");

        // What was worked is history worth reading: the field cannot be changed, and its ink is
        // still drawn at full strength rather than washed out with the theme's disabled colour.
        var row = Assert.Single(EntryRows(table));
        Assert.True(frame.InkExtent(AreaOf(row, "start-input")) > 20, "the times are still written out");
    }

    [AvaloniaFact]
    public void A_note_logged_from_the_line_is_drawn_on_the_row()
    {
        var day = NewDay();
        var line = new QuickEntry();
        var screen = Show(day, DayScreen(line));
        Settle();

        var box = line.FindControl<TextBox>("EntryBox")!;

        box.Focus();
        Window.KeyTextInput("Reading 14:00 2h");
        Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Settle();

        Window.KeyTextInput("# the LiteDB paper");
        Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Settle();

        using var frame = Shot("note-from-the-line");

        var row = Assert.Single(EntryRows(screen));
        Assert.Equal("the LiteDB paper", Cell(row, "note-input").Text);
        Assert.True(frame.InkExtent(AreaOf(row, "note-input")) > 40, "the note is written across its cell");
        Assert.Equal(string.Empty, box.Text);
    }

    /// <summary>The day as a person meets it: the line above, the rows below, on one window.</summary>
    private static Control DayScreen(QuickEntry line) =>
        new StackPanel
        {
            Margin = new Thickness(14),
            Spacing = 10,
            Children = { line, new DayTable() },
        };
}
