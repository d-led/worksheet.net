// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Worksheet.Desktop.Components;

namespace Worksheet.UiTests;

public sealed class DayHeaderTests : UiTestBase
{
    [AvaloniaFact]
    public void The_day_arrows_are_forty_four_pixels_and_not_a_glyph_dot()
    {
        var header = Show<DayHeader>(NewDay());

        foreach (var name in new[] { "PreviousDay", "NextDay" })
        {
            var button = header.FindControl<Button>(name)!;

            Assert.True(button.Bounds.Width >= 44, $"{name} is {button.Bounds.Width} wide");
            Assert.True(button.Bounds.Height >= 44, $"{name} is {button.Bounds.Height} tall");
        }
    }

    [AvaloniaFact]
    public void The_next_arrow_moves_the_day_and_the_title_says_so()
    {
        var day = NewDay();
        var header = Show<DayHeader>(day);
        var today = day.Date;
        Assert.StartsWith("Today", day.DayTitle);

        Click(header.FindControl<Button>("NextDay")!);

        Assert.Equal(today.AddDays(1), day.Date);
        Assert.DoesNotContain("Today", day.DayTitle);
        Assert.Equal(day.DayTitle, header.FindControl<TextBlock>("DayTitle")!.Text);
    }

    [AvaloniaFact]
    public void The_calendar_moves_the_day_and_the_total_follows()
    {
        var day = NewDay();
        var header = Show<DayHeader>(day);
        Seed(day, "Website 09:00 1h");
        var titleBefore = day.DayTitle;

        var calendar = OpenCalendar(header);
        calendar.SelectedDate = calendar.SelectedDate!.Value.AddDays(-5);
        Settle();

        Assert.NotEqual(titleBefore, day.DayTitle);
        Assert.Equal("0m", header.FindControl<TextBlock>("Total")!.Text);
    }

    [AvaloniaFact]
    public void The_calendar_is_a_glyph_with_no_number_of_its_own()
    {
        var header = Show<DayHeader>(NewDay());
        var button = header.FindControl<Button>("Calendar")!;

        // The stock picker draws a number on its sheet, which next to a day on screen reads as a
        // date. This one is an outline, and nothing in the heading is a second date to be misread.
        var icon = Assert.IsType<PathIcon>(button.Content);

        Assert.NotNull(icon.Data);
        Assert.Empty(Descendants<TextBlock>(button));
    }

    [AvaloniaFact]
    public void The_calendar_closes_the_moment_a_day_is_picked()
    {
        var day = NewDay();
        var header = Show<DayHeader>(day);
        var flyout = (Flyout)header.FindControl<Button>("Calendar")!.Flyout!;

        var calendar = OpenCalendar(header);

        // Open, it is a picture on the glass and not just a flag: the frame keeps it as one.
        using (var frame = Shot("calendar-open"))
        {
            Assert.True(frame.InkExtent(AreaOf(calendar)) > 0);
        }

        calendar.SelectedDate = calendar.SelectedDate!.Value.AddDays(-2);
        Settle();

        Assert.False(flyout.IsOpen);
        Assert.NotEqual(day.Date, NewDay().Date);
    }

    [AvaloniaFact]
    public void The_calendar_closes_when_the_focus_leaves_it()
    {
        var header = Show<DayHeader>(NewDay());
        var flyout = (Flyout)header.FindControl<Button>("Calendar")!.Flyout!;

        var calendar = OpenCalendar(header);
        Assert.True(flyout.IsOpen);

        // The keyboard goes somewhere else: the calendar has been left, so it goes with it.
        header.FindControl<Button>("Today")!.Focus();
        Settle();

        Assert.False(flyout.IsOpen);
    }

    /// <summary>The calendar of an open heading: the button pressed, and what the popup holds.</summary>
    private Calendar OpenCalendar(DayHeader header)
    {
        var button = header.FindControl<Button>("Calendar")!;

        Click(button);

        return Assert.IsType<Calendar>(Assert.IsType<Flyout>(button.Flyout).Content);
    }

    [AvaloniaFact]
    public void Today_returns_to_the_day_the_application_started_on()
    {
        var day = NewDay();
        var header = Show<DayHeader>(day);

        Click(header.FindControl<Button>("PreviousDay")!);
        Click(header.FindControl<Button>("Today")!);

        Assert.StartsWith("Today", day.DayTitle);
    }
}
