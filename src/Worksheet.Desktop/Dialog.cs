// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Controls;

namespace Worksheet.Desktop;

/// <summary>
/// Showing a window that stands beside another. Avalonia refuses a window whose owner is not on
/// screen, and this application hides its main window whenever it is left alone in the tray — so a
/// window opened from the tray stands on its own rather than claim a parent it cannot have. Either
/// way it is brought to the front and takes the keyboard, which is what opening a window is supposed
/// to do: one that appears behind the window it was opened from has not been opened.
/// </summary>
internal static class Dialog
{
    public static void ShowOver(Window window, Window? owner)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (owner is { IsVisible: true })
        {
            window.Show(owner);
        }
        else
        {
            window.Show();
        }

        window.Activate();
    }
}
