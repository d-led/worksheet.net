// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Worksheet.Desktop;

namespace Worksheet.UiTests;

/// <summary>
/// What the platform's own summons means to the window: a click on the Dock icon with nothing on
/// screen, or the application being brought to the front, is a person asking for the window. The
/// menu-bar icon is then one way back rather than the only one.
/// </summary>
public sealed class ApplicationSummonsTests
{
    /// <summary>A window put aside, the summons that watches it, and what each of them saw.</summary>
    private static (Window Window, ApplicationSummons Summons, Counting Counting) PutAside()
    {
        var window = new Window();
        var counting = new Counting();
        var summons = new ApplicationSummons(window, counting.ClockRead, counting.IconPutBack);
        window.Hide();

        return (window, summons, counting);
    }

    private sealed class Counting
    {
        public int ClocksRead { get; private set; }

        public int IconsPutBack { get; private set; }

        public void ClockRead() => ClocksRead++;

        public void IconPutBack() => IconsPutBack++;
    }

    private static void AppIsActivated(ApplicationSummons summons, ActivationKind kind) =>
        summons.Handle(new ActivatedEventArgs(kind));

    [AvaloniaFact]
    public void A_click_on_the_dock_brings_back_a_window_that_was_put_aside()
    {
        var (window, summons, counting) = PutAside();

        AppIsActivated(summons, ActivationKind.Reopen);

        Assert.True(window.IsVisible);
        Assert.Equal(1, counting.ClocksRead);
    }

    [AvaloniaFact]
    public void Coming_back_to_the_application_brings_the_window_back_too()
    {
        var (window, summons, _) = PutAside();

        AppIsActivated(summons, ActivationKind.Background);

        Assert.True(window.IsVisible);
    }

    /// <summary>
    /// The moment a person is looking for the way back is the moment the icon is worth putting in
    /// the menu bar again: macOS may have taken it away without a word.
    /// </summary>
    [AvaloniaFact]
    public void A_window_brought_back_is_the_moment_the_icon_is_put_back()
    {
        var (_, summons, counting) = PutAside();

        AppIsActivated(summons, ActivationKind.Reopen);

        Assert.Equal(1, counting.IconsPutBack);
    }

    [AvaloniaFact]
    public void A_window_already_on_screen_has_its_clock_read_but_is_not_put_back_again()
    {
        var (window, summons, counting) = PutAside();
        window.Show();

        AppIsActivated(summons, ActivationKind.Background);

        Assert.True(window.IsVisible);
        Assert.Equal(1, counting.ClocksRead);
        Assert.Equal(0, counting.IconsPutBack);
    }

    [AvaloniaFact]
    public void A_file_handed_to_the_application_is_not_a_summons()
    {
        var (window, summons, counting) = PutAside();

        AppIsActivated(summons, ActivationKind.File);

        Assert.False(window.IsVisible);
        Assert.Equal(0, counting.ClocksRead);
        Assert.Equal(0, counting.IconsPutBack);
    }
}
