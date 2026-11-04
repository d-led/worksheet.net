// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Worksheet.Desktop.Components;
using Worksheet.Desktop.ViewModels;

namespace Worksheet.UiTests;

/// <summary>
/// The heading's menu: the four screens the tray also offers, reachable without finding an icon in
/// the menu bar.
/// </summary>
public sealed class DayHeaderScreensTests : UiTestBase
{
    private static readonly string[] Screens =
        ["Settings…", "Projects and topics…", "Connect an agent…", "Open data folder", "The journal…"];

    [AvaloniaFact]
    public void The_heading_offers_the_five_screens_the_tray_also_offers()
    {
        var header = Show<DayHeader>(NewDay());

        var items = Menu(header).Items.OfType<MenuItem>().Select(item => item.Header?.ToString() ?? string.Empty);

        Assert.Equal(Screens, items.ToArray());
    }

    [AvaloniaFact]
    public void Every_item_opens_what_it_names()
    {
        var day = NewDay();
        var opened = new List<string>();
        day.Screens = new WindowScreens(
            () => opened.Add("settings"),
            () => opened.Add("manage"),
            () => opened.Add("connect"),
            () => opened.Add("audit"),
            () => opened.Add("data"));

        var header = Show<DayHeader>(day);

        foreach (var item in Menu(header).Items.OfType<MenuItem>())
        {
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        }

        Assert.Equal(["settings", "manage", "connect", "data", "audit"], opened);
    }

    [AvaloniaFact]
    public void A_window_with_no_screens_does_not_fail_when_one_is_pressed()
    {
        var header = Show<DayHeader>(NewDay());

        var pressed = Menu(header).Items.OfType<MenuItem>().Select(item =>
        {
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            return true;
        });

        Assert.All(pressed, Assert.True);
    }

    private static MenuFlyout Menu(DayHeader header) =>
        (MenuFlyout)header.FindControl<Button>("Screens")!.Flyout!;
}
