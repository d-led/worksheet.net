// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Worksheet.Desktop.ViewModels;

namespace Worksheet.Desktop.Views;

/// <summary>
/// The journal, read as an audit: every change the file holds, the filter over all of them, and a
/// change opened by double clicking it. The window owns the opening, since it owns what a person
/// sees; the view model only says which change was asked for.
/// </summary>
public partial class AuditWindow : SecondaryWindow
{
    public AuditWindow() => InitializeComponent();

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

    /// <summary>A double click on a row opens the body of that change; a double click on the list itself does nothing.</summary>
    private void Row_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not AuditViewModel audit)
        {
            return;
        }

        if (e.Source is Visual source && Row(source) is { } row)
        {
            audit.Open?.Invoke(row);
        }
    }

    private static AuditRow? Row(Visual source) =>
        source.GetSelfAndVisualAncestors().OfType<Control>().Select(control => control.DataContext).OfType<AuditRow>().FirstOrDefault();
}
