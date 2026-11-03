// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Worksheet.Desktop.Components;

namespace Worksheet.UiTests;

public sealed class QuickEntryTests : UiTestBase
{
    [AvaloniaFact]
    public void Typing_a_line_and_pressing_enter_logs_it_and_clears_the_box()
    {
        var day = NewDay();
        var entry = Show<QuickEntry>(day);
        var box = entry.FindControl<TextBox>("EntryBox")!;

        box.Focus();
        Window.KeyTextInput("Website 09:00 1h");
        Settle();
        Assert.Equal("Website 09:00 1h", box.Text);

        Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Settle();

        Assert.Single(day.Rows);
        Assert.Equal(string.Empty, box.Text);
    }

    [AvaloniaFact]
    public void A_known_project_is_offered_as_a_chip_that_finishes_the_line()
    {
        var day = NewDay();
        Seed(day, "Website 09:00 1h");

        var entry = Show<QuickEntry>(day);
        var box = entry.FindControl<TextBox>("EntryBox")!;

        box.Focus();
        Window.KeyTextInput("Web");
        Settle();

        var chip = Descendants<Button>(entry).Single(button => button.Classes.Contains("chip"));
        Assert.Equal("Website", chip.Content);

        Click(chip);

        // The trailing space is where the time goes next.
        Assert.Equal("Website ", day.EntryText);
        Assert.Equal("Website ", box.Text);
    }

    [AvaloniaFact]
    public void The_chips_become_the_topics_of_the_project_once_its_bracket_is_open()
    {
        var day = NewDay();
        Seed(day, "Website (login) 09:00 1h");
        Seed(day, "Website (reports) 11:00 1h");

        var entry = Show<QuickEntry>(day);
        var box = entry.FindControl<TextBox>("EntryBox")!;

        box.Focus();
        Window.KeyTextInput("Website (");
        Settle();

        Assert.Equal(
            new[] { "login", "reports" },
            Descendants<Button>(entry).Where(button => button.Classes.Contains("chip")).Select(button => button.Content as string));
        Assert.Equal("TOPICS", entry.FindControl<TextBlock>("SuggestionCaption")!.Text);
    }

    [AvaloniaFact]
    public void Tab_takes_the_chip_and_the_right_arrow_moves_between_them()
    {
        var day = NewDay();
        Seed(day, "Website (login) 09:00 1h");
        Seed(day, "Webshop (orders) 11:00 1h");

        var entry = Show<QuickEntry>(day);
        var box = entry.FindControl<TextBox>("EntryBox")!;

        box.Focus();
        Window.KeyTextInput("Web");
        Settle();

        Window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
        Settle();
        Window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
        Settle();

        Assert.Equal("Website (", box.Text);

        Window.KeyTextInput("lo");
        Settle();
        Window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
        Settle();

        Assert.Equal("Website (login) ", box.Text);
    }

    [AvaloniaFact]
    public void Shift_Tab_belongs_to_the_focus_and_not_to_the_offers()
    {
        var day = NewDay();
        Seed(day, "Website 09:00 1h");

        var entry = Show<QuickEntry>(day);
        var box = entry.FindControl<TextBox>("EntryBox")!;

        box.Focus();
        Window.KeyTextInput("Web");
        Settle();

        Window.KeyPress(Key.Tab, RawInputModifiers.Shift, PhysicalKey.Tab, null);
        Settle();

        Assert.Equal("Web", day.EntryText);
    }

    [AvaloniaFact]
    public void The_arrows_inside_the_line_move_the_caret_rather_than_the_offers()
    {
        var day = NewDay();
        Seed(day, "Website 09:00 1h");

        var entry = Show<QuickEntry>(day);
        var box = entry.FindControl<TextBox>("EntryBox")!;

        box.Focus();
        Window.KeyTextInput("Web");
        Window.KeyPress(Key.Home, RawInputModifiers.None, PhysicalKey.Home, null);
        Settle();

        Window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
        Settle();

        Assert.Equal(1, box.CaretIndex);
        Assert.Equal("Web", day.EntryText);
    }

    [AvaloniaFact]
    public void A_rejected_line_is_explained_rather_than_swallowed()
    {
        var day = NewDay();
        var entry = Show<QuickEntry>(day);
        var box = entry.FindControl<TextBox>("EntryBox")!;

        box.Focus();
        Window.KeyTextInput("Website without a time");
        Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Settle();

        Assert.Empty(day.Rows);
        Assert.True(day.MessageIsProblem);
        Assert.Equal("Website without a time", box.Text);
    }
}
