// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using CommunityToolkit.Mvvm.ComponentModel;

namespace Worksheet.Desktop.ViewModels;

/// <summary>
/// What a person is told the first time they close the window: that the day keeps counting, where
/// the icon they need is, and how to leave for good instead. Told once, because a warning that
/// repeats itself every time is one nobody reads.
/// </summary>
public sealed partial class CloseNoticeViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _doNotTellAgain = true;

    public string Title { get; } = "Worksheet.net keeps running";

    public string Body { get; } =
        "The window closes and the day keeps counting. The icon in the tray brings it back — its menu "
        + "holds the rest, Quit among them — and so does starting Worksheet.net again.";

    public string Quiet { get; } = "Don't tell me again.";
}
