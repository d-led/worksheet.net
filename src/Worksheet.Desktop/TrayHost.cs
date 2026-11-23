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
    Action Audit,
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
///
/// The icon can be put again on request: Windows puts a tray icon back after its taskbar starts
/// anew, and Linux follows the host it arranged the item with, but macOS takes a status item away —
/// with the menu bar full, or when its own bar settings change — without a word.
/// </summary>
internal sealed class TrayHost
{
    private const string TrayIconUri = "avares://Worksheet.Desktop/Assets/tray.png";

    private readonly Application _application;
    private readonly TrayActions _actions;

    private TrayHost(Application application, TrayActions actions)
    {
        _application = application;
        _actions = actions;
    }

    /// <summary>Puts the icon in the menu bar and hands back what can put it there again.</summary>
    public static TrayHost Attach(Application application, TrayActions actions)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(actions);

        var host = new TrayHost(application, actions);
        host.Put();

        return host;
    }

    /// <summary>
    /// Puts the icon in the menu bar, replacing one that may no longer be there. Called again when
    /// a window that was put aside comes back, which is the moment a person is looking for the way
    /// in — and the only way to know whether macOS still shows the icon.
    /// </summary>
    public void Put()
    {
        var tray = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri(TrayIconUri))),
            ToolTipText = "Worksheet.net",
            IsVisible = true,
            Menu = Menu(_actions),
        };

        tray.Clicked += (_, _) => _actions.ShowWindow();

        TrayIcon.SetIcons(_application, new TrayIcons { tray });
    }

    private static NativeMenu Menu(TrayActions actions) => new()
    {
        Items =
        {
            MenuItem("Show window", actions.ShowWindow),
            new NativeMenuItemSeparator(),
            MenuItem("Connect an agent…", actions.Connect),
            MenuItem("Projects and topics…", actions.Manage),
            MenuItem("Settings…", actions.Settings),
            MenuItem("Open data folder", actions.OpenDataFolder),
            MenuItem("The journal…", actions.Audit),
            new NativeMenuItemSeparator(),
            MenuItem("Quit", actions.Quit),
        },
    };

    private static NativeMenuItem MenuItem(string header, Action action)
    {
        var item = new NativeMenuItem(header);
        item.Click += (_, _) => action();
        return item;
    }
}
