// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Worksheet.Desktop.ViewModels;

namespace Worksheet.Desktop.Components;

/// <summary>
/// Completion is reached with keys the entry box would otherwise keep for itself: Tab would move the
/// focus, and the arrows would move the caret. They are taken before the box sees them, and only
/// while there is something to complete — and the left and right arrows are only taken at the end of
/// the line, so the caret still moves inside a line being corrected.
/// </summary>
public partial class QuickEntry : UserControl
{
    private static readonly Dictionary<Key, Action<DayViewModel>> Offers = new()
    {
        [Key.Tab] = day => day.CompleteSuggestionCommand.Execute(null),
        [Key.Right] = day => day.NextSuggestionCommand.Execute(null),
        [Key.Down] = day => day.NextSuggestionCommand.Execute(null),
        [Key.Left] = day => day.PreviousSuggestionCommand.Execute(null),
        [Key.Up] = day => day.PreviousSuggestionCommand.Execute(null),
    };

    public QuickEntry()
    {
        InitializeComponent();

        AddHandler(KeyDownEvent, PreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(Button.ClickEvent, ChipClicked, RoutingStrategies.Bubble);
    }

    private void PreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is DayViewModel day && day.HasSuggestions && Offer(day, e))
        {
            e.Handled = true;
        }
    }

    private bool Offer(DayViewModel day, KeyEventArgs e)
    {
        if (!Offers.TryGetValue(e.Key, out var offer) || !Allowed(e.Key, e.KeyModifiers))
        {
            return false;
        }

        offer(day);

        if (e.Key is Key.Tab)
        {
            CarryOnTyping();
        }

        return true;
    }

    /// <summary>Tab on its own takes an offer; Shift+Tab is the focus, and the arrows edit the line.</summary>
    private bool Allowed(Key key, KeyModifiers modifiers) =>
        key switch
        {
            Key.Tab => modifiers == KeyModifiers.None,
            Key.Left or Key.Right => AtTheEndOfTheLine(),
            _ => true,
        };

    private bool AtTheEndOfTheLine() => EntryBox.CaretIndex >= (EntryBox.Text?.Length ?? 0);

    /// <summary>A chip was clicked: the line has changed under the pointer, so typing belongs there.</summary>
    private void ChipClicked(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Button { } chip && chip.Classes.Contains("chip"))
        {
            CarryOnTyping();
        }
    }

    /// <summary>
    /// Puts the caret after what was just written. The line grew under it, and a caret left in the
    /// middle would have the next keystroke land inside the completed name.
    /// </summary>
    private void CarryOnTyping()
    {
        EntryBox.Focus();
        EntryBox.CaretIndex = EntryBox.Text?.Length ?? 0;
    }
}
