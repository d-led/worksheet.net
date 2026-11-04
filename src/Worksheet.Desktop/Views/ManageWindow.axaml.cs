// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Interactivity;
using SukiUI.Controls;

namespace Worksheet.Desktop.Views;

/// <summary>The catalogue, in a window of its own. It closes without touching the day.</summary>
public partial class ManageWindow : SukiWindow
{
    public ManageWindow() => InitializeComponent();

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
