// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Worksheet.Desktop;

/// <summary>
/// The tray icon and its menu. A click on the icon raises the window — it never hides it, so a
/// double click is simply two raises rather than a show and a hide — and the menu, which is a right
/// click on Windows and Linux and the only click on macOS, holds what is safe without a window: how
/// to connect an agent, the folder the journal lives in, and the one way to quit.
///
/// Undo and redo are deliberately *not* here. They change the day, and the day is not on screen:
/// a compensating change taken blind is a change nobody can see the effect of.
/// </summary>
internal static class TrayHost
{
    private const string TrayIconUri = "avares://Worksheet.Desktop/Assets/tray.png";

    public static void Attach(
        Application application,
        Action showWindow,
        Action connect,
        Action settings,
        Action openDataFolder,
        Action quit)
    {
        ArgumentNullException.ThrowIfNull(application);

        var tray = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri(TrayIconUri))),
            ToolTipText = "Worksheet",
            IsVisible = true,
        };

        tray.Clicked += (_, _) => showWindow();
        tray.Menu = new NativeMenu
        {
            Items =
            {
                MenuItem("Show window", showWindow),
                new NativeMenuItemSeparator(),
                MenuItem("Connect an agent…", connect),
                MenuItem("Settings…", settings),
                MenuItem("Open data folder", openDataFolder),
                new NativeMenuItemSeparator(),
                MenuItem("Quit", quit),
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
