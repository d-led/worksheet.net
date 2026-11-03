// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Worksheet.Desktop;

/// <summary>
/// The tray icon and its menu: the same menu on every platform, and a click that brings the
/// window back. The menu is the application's right-click affordance as well as its only exit.
/// </summary>
internal static class TrayHost
{
    private const string TrayIconUri = "avares://Worksheet.Desktop/Assets/tray.png";

    public static void Attach(
        Application application,
        Action showWindow,
        Action undoLastChange,
        Action redoLastChange,
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
                MenuItem("Undo last change", undoLastChange),
                MenuItem("Redo last change", redoLastChange),
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
