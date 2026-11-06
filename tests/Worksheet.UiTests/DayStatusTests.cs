// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Worksheet.Core;
using Worksheet.Desktop.Components;
using Worksheet.Desktop.ViewModels;

namespace Worksheet.UiTests;

/// <summary>
/// The five words a day can be, as radios. Choosing one is a change to the ledger, and any answer
/// but "working day" freezes the timetable beside it.
/// </summary>
public sealed class DayStatusTests : UiTestBase
{
    private static readonly DateOnly Saturday = new(2026, 11, 7);

    private static IEnumerable<RadioButton> Kinds(Visual root) =>
        Descendants<RadioButton>(root).Where(radio => radio.Classes.Contains("kind"));

    private static RadioButton Kind(Visual root, string label) =>
        Kinds(root).Single(radio => (radio.Content as string) == label);

    /// <summary>The status row directly above the table, as the window stacks them.</summary>
    private static StackPanel StatusOverTable()
    {
        var panel = new StackPanel { Spacing = 10 };

        panel.Children.Add(new DayStatus());
        panel.Children.Add(new DayTable());

        return panel;
    }

    [AvaloniaFact]
    public void A_saturday_offers_five_statuses_with_the_weekend_already_chosen()
    {
        MoveTo(Saturday);
        var day = NewDay();

        var panel = Show(day, StatusOverTable());

        Assert.Equal(
            new[] { "Working day", "Flex day", "Sick day", "Holiday", "Weekend" },
            Kinds(panel).Select(radio => radio.Content as string));
        Assert.True(Kind(panel, "Weekend").IsChecked);
    }

    [AvaloniaFact]
    public void Choosing_a_holiday_is_recorded_and_freezes_the_table()
    {
        var day = NewDay();
        Seed(day, "Website 09:00 1h");

        var panel = Show(day, StatusOverTable());
        Click(Kind(panel, "Holiday"));

        Assert.Equal(DayKind.Holiday, Ledger.Ledger.State.KindOf(Today));
        Assert.All(
            Descendants<Border>(panel).Where(border => border.Classes.Contains("row")),
            row => Assert.False(Descendants<TextBox>(row).First().IsEffectivelyEnabled));
        Assert.True(Descendants<TextBlock>(panel).Single(block => block.Name == "FrozenNote").IsVisible);
    }

    [AvaloniaFact]
    public void Working_a_saturday_back_thaws_the_table()
    {
        MoveTo(Saturday);
        var day = NewDay();

        var panel = Show(day, StatusOverTable());
        Assert.True(day.IsFrozen);

        Click(Kind(panel, "Working day"));

        Assert.False(day.IsFrozen);
        Assert.Equal(DayKind.Working, day.Kinds.Single(option => option.IsSelected).Kind);
    }
}
