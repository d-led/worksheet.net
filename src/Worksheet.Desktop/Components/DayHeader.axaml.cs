// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Worksheet.Desktop.ViewModels;
using Worksheet.Export;

namespace Worksheet.Desktop.Components;

/// <summary>
/// The day's heading and its one action that leaves the application: an export. The window owns the
/// save dialog — the day builds the file, this asks where it goes and writes it.
/// </summary>
public partial class DayHeader : UserControl
{
    public DayHeader() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is DayViewModel day)
        {
            day.SaveExport = SaveAsync;
            WireExportMenu(day);
            WireScreens();
        }
    }

    /// <summary>
    /// The screens live in a flyout, which is outside the window's data context, so the clicks are
    /// wired here; each item names the screen it opens and the day's screens say what that is.
    /// </summary>
    private void WireScreens()
    {
        if (Screens.Flyout is not MenuFlyout menu)
        {
            return;
        }

        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            item.Click += (_, _) => Opening(item);
        }
    }

    /// <summary>Opens the screen the pressed item names, read from the day as it is pressed rather than when wired.</summary>
    private void Opening(MenuItem item)
    {
        var name = item.Name ?? throw new InvalidOperationException("A screen item needs a name.");

        (DataContext as DayViewModel)?.Screens?.Open(name);
    }

    /// <summary>
    /// The ranges live in a flyout, which is not part of the window's data context until it opens,
    /// so a binding there resolves to nothing. The commands are put on the items instead — and a tag
    /// that does not name a range is a bug this makes loud rather than silent.
    /// </summary>
    private void WireExportMenu(DayViewModel day)
    {
        if (Export.Flyout is not MenuFlyout menu)
        {
            return;
        }

        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            item.Command = Enum.TryParse<ExportPeriod>(item.Tag?.ToString(), out var period)
                ? day.CommandFor(period)
                : throw new InvalidOperationException($"'{item.Header}' names no export range.");
        }
    }

    /// <summary>Asks where the export goes and writes it; nothing is written when the dialog is dismissed.</summary>
    private async Task<string?> SaveAsync(ExportFile file)
    {
        if (await ChooseAsync(file) is not { } chosen)
        {
            return null;
        }

        await using (var stream = await chosen.OpenWriteAsync())
        {
            await stream.WriteAsync(file.Bytes);
        }

        return chosen.Path.LocalPath;
    }

    /// <summary>Where it should go, or nothing when there is no dialog to ask (a headless window) or it was dismissed.</summary>
    private async Task<IStorageFile?> ChooseAsync(ExportFile file) =>
        TopLevel.GetTopLevel(this)?.StorageProvider is { } storage
            ? await storage.SaveFilePickerAsync(OptionsFor(file))
            : null;

    /// <summary>The dialog offers one thing, named after the range, so the file saves as a zip.</summary>
    private static FilePickerSaveOptions OptionsFor(ExportFile file) =>
        new()
        {
            Title = "Export",
            SuggestedFileName = file.FileName,
            DefaultExtension = "zip",
            FileTypeChoices = [new FilePickerFileType("Worksheet export") { Patterns = ["*.zip"] }],
        };
}
