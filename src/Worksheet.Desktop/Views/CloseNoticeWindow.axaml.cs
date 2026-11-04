// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Interactivity;
using Worksheet.Desktop.ViewModels;

namespace Worksheet.Desktop.Views;

/// <summary>
/// The first close, said out loud: what happens now, where the icon is, and how to leave instead.
/// The window answers with <see cref="QuitInstead"/>; whether the person wants to hear it again is
/// <see cref="DoNotTellAgain"/>, which starts ticked — the notice is a courtesy, not a step.
/// </summary>
public partial class CloseNoticeWindow : SecondaryWindow
{
    public CloseNoticeWindow() => InitializeComponent();

    /// <summary>True when the person chose to leave the application rather than put the window aside.</summary>
    public bool QuitInstead { get; private set; }

    public bool DoNotTellAgain => (DataContext as CloseNoticeViewModel)?.DoNotTellAgain ?? true;

    private void Quit_Click(object? sender, RoutedEventArgs e)
    {
        QuitInstead = true;
        Close();
    }

    private void CloseWindow_Click(object? sender, RoutedEventArgs e) => Close();
}
