// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Controls;

namespace Worksheet.Desktop;

/// <summary>
/// Showing a dialog over another window. Avalonia refuses a window whose owner is not on screen,
/// and this application hides its main window whenever it is left alone in the tray — so a dialog
/// opened from the tray must stand on its own rather than claim a parent it cannot have.
/// </summary>
internal static class Dialog
{
    public static void ShowOver(Window window, Window? owner)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (owner is { IsVisible: true })
        {
            window.Show(owner);
            return;
        }

        window.Show();
    }
}
