// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Worksheet.Desktop;

namespace Worksheet.UiTests;

/// <summary>
/// Opening a dialog from the tray, where the main window is usually hidden. Avalonia refuses a
/// window whose owner is not on screen, which is why every tray item used to quit the application.
/// </summary>
public sealed class DialogTests
{
    [AvaloniaFact]
    public void A_dialog_opened_while_the_main_window_is_hidden_still_opens()
    {
        var main = new Window { Width = 400, Height = 300 };
        main.Show();
        main.Hide();

        var dialog = new Window { Width = 200, Height = 120 };

        Dialog.ShowOver(dialog, main);

        Assert.True(dialog.IsVisible);
    }

    [AvaloniaFact]
    public void A_dialog_opened_over_the_main_window_still_opens()
    {
        var main = new Window { Width = 400, Height = 300 };
        main.Show();

        var dialog = new Window { Width = 200, Height = 120 };

        Dialog.ShowOver(dialog, main);

        Assert.True(dialog.IsVisible);
    }

    [AvaloniaFact]
    public void A_dialog_with_no_main_window_still_opens()
    {
        var dialog = new Window { Width = 200, Height = 120 };

        Dialog.ShowOver(dialog, owner: null);

        Assert.True(dialog.IsVisible);
    }
}
