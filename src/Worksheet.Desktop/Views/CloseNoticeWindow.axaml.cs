// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Interactivity;
using Worksheet.Desktop.ViewModels;

namespace Worksheet.Desktop.Views;

/// <summary>
/// The first close, said out loud: what happens now and where the icon is. The window has one
/// answer — the window closes and the day keeps going — and whether the person wants to hear it
/// again is <see cref="DoNotTellAgain"/>, which starts ticked: the notice is a courtesy, not a step.
/// </summary>
public partial class CloseNoticeWindow : SecondaryWindow
{
    public CloseNoticeWindow() => InitializeComponent();

    public bool DoNotTellAgain => (DataContext as CloseNoticeViewModel)?.DoNotTellAgain ?? true;

    private void CloseWindow_Click(object? sender, RoutedEventArgs e) => Close();
}
