// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Interactivity;
using SukiUI.Controls;

namespace Worksheet.Desktop.Views;

/// <summary>A frame around <see cref="Components.ConnectPanel"/> that closes without touching the day.</summary>
public partial class ConnectWindow : SecondaryWindow
{
    public ConnectWindow() => InitializeComponent();

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
