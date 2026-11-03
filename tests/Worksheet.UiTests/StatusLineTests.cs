// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Worksheet.Desktop.Components;

namespace Worksheet.UiTests;

public sealed class StatusLineTests : UiTestBase
{
    [AvaloniaFact]
    public void Undo_and_redo_are_unavailable_until_they_mean_something()
    {
        var day = NewDay();
        var status = Show<StatusLine>(day);

        var undo = status.FindControl<Button>("Undo")!;
        var redo = status.FindControl<Button>("Redo")!;

        Assert.False(undo.IsEnabled);
        Assert.False(redo.IsEnabled);

        Seed(day, "Website 09:00 1h");
        Assert.True(undo.IsEnabled);

        Click(undo);
        Assert.True(redo.IsEnabled);

        Click(redo);
        Assert.False(redo.IsEnabled);
    }

    [AvaloniaFact]
    public void A_refused_line_is_shown_and_marked_as_a_problem()
    {
        var day = NewDay();
        var status = Show<StatusLine>(day);
        var message = status.FindControl<TextBlock>("Message")!;

        Assert.False(message.IsVisible);

        day.EntryText = "Website without a time";
        day.LogCommand.Execute(null);
        Settle();

        Assert.True(message.IsVisible);
        Assert.Contains("problem", message.Classes);
        Assert.Equal(day.Message, message.Text);
    }

    [AvaloniaFact]
    public void A_successful_line_clears_the_message()
    {
        var day = NewDay();
        var status = Show<StatusLine>(day);
        var message = status.FindControl<TextBlock>("Message")!;

        day.EntryText = "nonsense";
        day.LogCommand.Execute(null);
        Settle();
        Assert.True(message.IsVisible);

        Seed(day, "Website 09:00 1h");

        Assert.False(message.IsVisible);
        Assert.DoesNotContain("problem", message.Classes);
    }
}
