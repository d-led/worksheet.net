// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Worksheet.Desktop.Components;
using Worksheet.Desktop.ViewModels;

namespace Worksheet.UiTests;

/// <summary>
/// The banner an export leaves behind: what was written, the folder it went to, and the two things
/// a person may want next — to be shown the file, or to put the news away.
/// </summary>
public sealed class ExportBannerTests : UiTestBase
{
    /// <summary>
    /// The path the save dialog answered with: one folder deep, so no separator in it is the host's
    /// and the folder shown is the same word everywhere.
    /// </summary>
    private static readonly string Written = Path.Combine("somewhere", "2026-11-04-0930-worksheet-2026-11.zip");

    /// <summary>The banner of a day that exported once, over a window that can show the file.</summary>
    private async Task<(DayViewModel Day, List<string> Revealed)> Exported()
    {
        var day = NewDay();
        day.EntryText = "Website 09:00 1h";
        day.LogCommand.Execute(null);

        var revealed = new List<string>();

        Show<ExportBanner>(day);
        day.RevealFile = revealed.Add;
        day.SaveExport = _ => Task.FromResult<string?>(Written);

        await day.ExportMonthCommand.ExecuteAsync(null);
        Settle();

        return (day, revealed);
    }

    private Border Banner() => Descendants<Border>(Window).Single(border => border.Classes.Contains("banner"));

    private static IEnumerable<string?> Shown(Border banner) => Descendants<TextBlock>(banner).Select(block => block.Text);

    [AvaloniaFact]
    public async Task The_banner_names_the_file_and_the_folder_it_went_to()
    {
        await Exported();

        var banner = Banner();

        Assert.True(banner.IsVisible);
        Assert.Contains("2026-11-04-0930-worksheet-2026-11.zip", Shown(banner));
        Assert.Contains("somewhere", Shown(banner));

        // Kept as a picture too: the banner is a thing a person reads at a glance, not a list of
        // parts, and what it looks like is only answerable from the frame.
        using var frame = Shot("export-banner");
        Assert.True(frame.InkExtent(Area(banner)) > 0);
    }

    /// <summary>The banner as the frame sees it: the window's pixels.</summary>
    private PixelRect Area(Control control)
    {
        var topLeft = control.TranslatePoint(new Point(0, 0), Window)
            ?? throw new InvalidOperationException("The banner is not laid out.");

        return new PixelRect(
            (int)Math.Round(topLeft.X),
            (int)Math.Round(topLeft.Y),
            (int)Math.Round(control.Bounds.Width),
            (int)Math.Round(control.Bounds.Height));
    }

    [AvaloniaFact]
    public async Task The_banner_shows_the_file_when_its_button_is_pressed()
    {
        var (_, revealed) = await Exported();

        Click(Descendants<Button>(Banner()).Single(button => button.Name == "Reveal"));

        Assert.Equal([Written], revealed);
    }

    [AvaloniaFact]
    public async Task Dismissing_the_banner_puts_it_away()
    {
        var (day, _) = await Exported();
        var banner = Banner();

        Click(Descendants<Button>(banner).Single(button => (button.Content as string) == "✕"));

        Assert.False(banner.IsVisible);
        Assert.False(day.HasExported);
    }

    [AvaloniaFact]
    public void No_banner_is_shown_before_anything_is_exported()
    {
        var day = NewDay();

        Show<ExportBanner>(day);

        Assert.False(Banner().IsVisible);
    }
}
