// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SukiUI.Controls;

namespace Worksheet.Desktop.Views;

public partial class MainWindow : SukiWindow
{
    public MainWindow()
    {
        InitializeComponent();

        Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Worksheet.Desktop/Assets/app.png")));
        Closing += HideInsteadOfClosing;
    }

    /// <summary>Set only when the tray asked to quit; a person closing the window means "later".</summary>
    public bool AllowClose { get; set; }

    private void HideInsteadOfClosing(object? sender, WindowClosingEventArgs e)
    {
        if (AllowClose)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }
}
