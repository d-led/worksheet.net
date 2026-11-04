// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Worksheet.Desktop.ViewModels;

namespace Worksheet.Desktop.Views;

/// <summary>
/// The catalogue, in a window of its own. It closes without touching the day, and every name in it
/// is a field: nothing here asks a second question after the first answer.
/// </summary>
public partial class ManageWindow : SecondaryWindow
{
    public ManageWindow() => InitializeComponent();

    /// <summary>
    /// Escape belongs to a name that differs from the journal before it belongs to the window: the
    /// first press puts the name back, the next one leaves.
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && Typed() is { } row)
        {
            row.RevertCommand.Execute(null);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    /// <summary>The row whose name is being typed and no longer says what the journal says.</summary>
    private CatalogueRow? Typed() =>
        FocusManager?.GetFocusedElement() is Control { DataContext: CatalogueRow { IsChanged: true } row }
            ? row
            : null;

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// A name field that takes the focus chooses its row, so the topics beside it follow the project
    /// being worked on even though a field can swallow the click that would have selected it.
    /// </summary>
    private void Row_Focused(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: CatalogueRow row } field)
        {
            field.FindAncestorOfType<ListBox>()?.SelectedItem = row;
        }
    }

    /// <summary>Leaving a name field settles what was typed there, the way leaving a cell of the day does.</summary>
    private void Row_Left(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: CatalogueRow row })
        {
            row.CommitCommand.Execute(null);
        }
    }
}
