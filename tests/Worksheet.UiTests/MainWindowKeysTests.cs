// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Worksheet.Desktop.Components;
using Worksheet.Desktop.ViewModels;
using Worksheet.Desktop.Views;

namespace Worksheet.UiTests;

/// <summary>
/// The arrow keys: they walk the day back and forward, but only where nothing else is using them —
/// a field being typed in keeps its own arrows.
/// </summary>
public sealed class MainWindowKeysTests : UiTestBase
{
    [AvaloniaFact]
    public void The_right_arrow_walks_to_the_next_day_and_the_left_arrow_back()
    {
        var (window, day) = Open();

        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Settle();
        Assert.Equal(Day(4), day.Date);

        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        Settle();
        Assert.Equal(Day(2), day.Date);

        window.Close();
    }

    [AvaloniaFact]
    public void Walking_back_to_today_says_so()
    {
        var (window, day) = Open();

        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Settle();

        Assert.StartsWith("Today", day.DayTitle);

        window.Close();
    }

    [AvaloniaFact]
    public void An_arrow_typed_into_a_field_stays_in_the_field()
    {
        var (window, day) = Open();
        var field = Descendants<TextBox>(window.FindControl<QuickEntry>("Entry")!).First();

        field.Focus();
        Settle();
        Assert.True(field.IsFocused);

        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        Settle();

        Assert.Equal(Day(3), day.Date);

        window.Close();
    }

    [AvaloniaFact]
    public void A_key_that_walks_nowhere_leaves_the_day_alone()
    {
        var (window, day) = Open();

        window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.None);
        Settle();

        Assert.Equal(Day(3), day.Date);

        window.Close();
    }

    private static DateOnly Day(int dayOfNovember) =>
        new(Today.Year, Today.Month, dayOfNovember);

    private (MainWindow Window, DayViewModel Day) Open()
    {
        var day = NewDay();
        var window = new MainWindow { DataContext = day };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, day);
    }
}
