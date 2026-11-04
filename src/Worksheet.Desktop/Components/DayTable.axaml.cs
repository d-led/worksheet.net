// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Worksheet.Desktop.Components;

/// <summary>
/// A cell settles what was typed when it is left — that is how a half-typed clock does not become a
/// journal change on every keystroke. Enter is the other way of meaning it: the same key that adds a
/// line to the day takes the value it is in, so an edit does not have to wait for a click somewhere
/// else. The key is read after the field has had it, because a project box uses Enter to take the
/// offer it is showing, and what is settled has to be what the box then says.
/// </summary>
public partial class DayTable : UserControl
{
    public DayTable()
    {
        InitializeComponent();

        AddHandler(KeyDownEvent, EnterSettles, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void EnterSettles(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Settle(Cell(e.Source as Control)))
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// The cell the key was pressed in. A project or topic box keeps a text box of its own, and the
    /// row's binding is on the box around it rather than on the field inside it.
    /// </summary>
    private static Control? Cell(Control? source) =>
        source is TextBox field && field.FindAncestorOfType<AutoCompleteBox>() is { } owner ? owner : source;

    /// <summary>Writes what the field holds into its row, the way leaving it would; false for anything else.</summary>
    private static bool Settle(Control? field) =>
        field switch
        {
            TextBox box => Write(box, TextBox.TextProperty),
            AutoCompleteBox box => Write(box, AutoCompleteBox.TextProperty),
            _ => false,
        };

    private static bool Write(AvaloniaObject field, AvaloniaProperty property)
    {
        if (BindingOperations.GetBindingExpressionBase(field, property) is not { } binding)
        {
            return false;
        }

        binding.UpdateSource();

        return true;
    }
}
