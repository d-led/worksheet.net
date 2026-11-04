// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Worksheet.Desktop;

/// <summary>What the tray menu can do, passed as one thing rather than six arguments in a row.</summary>
internal sealed record TrayActions(
    Action ShowWindow,
    Action Connect,
    Action Settings,
    Action Manage,
    Action OpenDataFolder,
    Action Quit);

/// <summary>
/// The tray icon and its menu. A click on the icon raises the window — it never hides it, so a
/// double click is simply two raises rather than a show and a hide — and the menu, which is a right
/// click on Windows and Linux and the only click on macOS, holds what is safe without a window: how
/// to connect an agent, the settings, the catalogue, the folder the journal lives in, and the one
/// way to quit.
///
/// Undo and redo are deliberately *not* here. They change the day, and the day is not on screen:
/// a compensating change taken blind is a change nobody can see the effect of.
/// </summary>
internal static class TrayHost
{
    private const string TrayIconUri = "avares://Worksheet.Desktop/Assets/tray.png";

    public static void Attach(Application application, TrayActions actions)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(actions);

        var tray = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri(TrayIconUri))),
            ToolTipText = "Worksheet",
            IsVisible = true,
        };

        tray.Clicked += (_, _) => actions.ShowWindow();
        tray.Menu = new NativeMenu
        {
            Items =
            {
                MenuItem("Show window", actions.ShowWindow),
                new NativeMenuItemSeparator(),
                MenuItem("Connect an agent…", actions.Connect),
                MenuItem("Projects and topics…", actions.Manage),
                MenuItem("Settings…", actions.Settings),
                MenuItem("Open data folder", actions.OpenDataFolder),
                new NativeMenuItemSeparator(),
                MenuItem("Quit", actions.Quit),
            },
        };

        TrayIcon.SetIcons(application, new TrayIcons { tray });
    }

    private static NativeMenuItem MenuItem(string header, Action action)
    {
        var item = new NativeMenuItem(header);
        item.Click += (_, _) => action();
        return item;
    }
}
