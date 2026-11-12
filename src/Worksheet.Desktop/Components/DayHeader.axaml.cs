// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Worksheet.Desktop.ViewModels;
using Worksheet.Export;

namespace Worksheet.Desktop.Components;

/// <summary>
/// The day's heading and its one action that leaves the application: an export. The window owns the
/// save dialog — the day builds the file, this asks where it goes and writes it. It also owns the
/// calendar, which is closed as soon as it has been used or left.
/// </summary>
public partial class DayHeader : UserControl
{
    /// <summary>
    /// True while the calendar is being put on the day the window is already showing: that is not a
    /// pick, and a pick closes the calendar.
    /// </summary>
    private bool _opening;

    public DayHeader() => InitializeComponent();

    /// <summary>
    /// The calendar opens on the day on screen and takes the keyboard, so the arrows and the keys
    /// belong to it. A flyout's content is outside the window's data context — the same reason the
    /// menus here are wired by hand — so the day is put on the calendar here rather than bound.
    /// </summary>
    private void OnCalendarOpened(object? sender, EventArgs e)
    {
        if (sender is not Flyout { Content: Calendar calendar } || DataContext is not DayViewModel day)
        {
            return;
        }

        _opening = true;
        calendar.SelectedDate = day.Date.ToDateTime(TimeOnly.MinValue);
        _opening = false;
        calendar.Focus();
    }

    /// <summary>A day was picked, so the visit is over and the calendar goes.</summary>
    private void OnDayPicked(object? sender, SelectionChangedEventArgs e)
    {
        if (_opening || sender is not Calendar { SelectedDate: { } picked })
        {
            return;
        }

        if (DataContext is DayViewModel day)
        {
            day.Show(DateOnly.FromDateTime(picked));
        }

        HideCalendarOf(sender);
    }

    /// <summary>
    /// The calendar closes when the focus leaves it — Tab to the next control, or a click somewhere
    /// else. Focus moving between the calendar's own parts — its days, its month, its arrows — is
    /// not leaving it, so the question is asked once the move has happened, not while it is.
    /// </summary>
    private void OnCalendarLeft(object? sender, RoutedEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (sender is Control picker && FocusStaysIn(picker))
            {
                return;
            }

            HideCalendarOf(sender);
        });

    /// <summary>The popup belongs to the button the calendar hangs from, which is where it is closed.</summary>
    private void HideCalendarOf(object? sender) =>
        (Calendar.Flyout as Flyout)?.Hide();

    private static bool FocusStaysIn(Control picker) =>
        TopLevel.GetTopLevel(picker)?.FocusManager?.GetFocusedElement() is Visual focused
        && (focused == picker || picker.IsVisualAncestorOf(focused));

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is DayViewModel day)
        {
            day.SaveExport = SaveAsync;
            day.RevealFile = Reveal.Show;
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
            FileTypeChoices = [new FilePickerFileType("Worksheet.net export") { Patterns = ["*.zip"] }],
        };
}
