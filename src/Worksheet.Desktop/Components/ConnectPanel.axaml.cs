// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Worksheet.Desktop.Components;

/// <summary>
/// Two snippets and two ways to copy them. The text box copies for itself — it owns the selection,
/// and the clipboard is its business rather than this panel's.
/// </summary>
public partial class ConnectPanel : UserControl
{
    public ConnectPanel() => InitializeComponent();

    private void CopyVsCode_Click(object? sender, RoutedEventArgs e) => Copy(VsCodeConfig);

    private void CopyOther_Click(object? sender, RoutedEventArgs e) => Copy(OtherConfig);

    private void Copy(TextBox snippet)
    {
        snippet.SelectAll();
        snippet.Copy();

        Copied.Text = "Copied.";
    }
}
