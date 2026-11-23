// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace Worksheet.Desktop;

/// <summary>
/// What a summons from the platform means to the window. macOS sends one when the Dock icon is
/// clicked while no window is on screen — <see cref="ActivationKind.Reopen"/> — and when the
/// application is brought to the front — <see cref="ActivationKind.Background"/>; either means the
/// window is wanted again. The menu-bar icon is one way back, not the only one, so a status item
/// macOS has hidden cannot strand the application.
/// </summary>
internal sealed class ApplicationSummons
{
    private readonly Window _window;
    private readonly Action _beforeShowing;
    private readonly Action _whenReturned;

    /// <param name="window">The window to bring forward.</param>
    /// <param name="beforeShowing">What a window on screen must re-read first; the clock, for one.</param>
    /// <param name="whenReturned">What is worth doing once a window that was put aside is back.</param>
    public ApplicationSummons(Window window, Action beforeShowing, Action whenReturned)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(beforeShowing);
        ArgumentNullException.ThrowIfNull(whenReturned);

        _window = window;
        _beforeShowing = beforeShowing;
        _whenReturned = whenReturned;
    }

    /// <summary>
    /// Answers one activation on the UI thread. A file or a link is not a summons: it says what
    /// the application was asked to open, not that the window was asked for.
    /// </summary>
    public void Handle(ActivatedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Kind is ActivationKind.Reopen or ActivationKind.Background && Raise())
        {
            _whenReturned();
        }
    }

    /// <summary>
    /// Brings the window forward, restoring it first if it was minimised or put aside. Says whether
    /// it had to be shown, which is the moment the menu-bar icon is worth putting back.
    /// </summary>
    private bool Raise()
    {
        _beforeShowing();

        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        var wasHidden = !_window.IsVisible;

        if (wasHidden)
        {
            _window.Show();
        }

        _window.Activate();

        return wasHidden;
    }
}
