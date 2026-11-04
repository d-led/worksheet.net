// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Worksheet.Core;
using Worksheet.Desktop.Components;
using Worksheet.Desktop.ViewModels;
using Worksheet.Desktop.Views;
using Worksheet.Storage;
using Worksheet.TestSupport;

namespace Worksheet.UiTests;

/// <summary>
/// The journal read as an audit through its own controls: what the file holds, the filter over all
/// of it, and a change opened by double clicking its row.
/// </summary>
public sealed class AuditWindowTests
{
    private static readonly DateOnly Day = new(2026, 11, 3);

    private static AuditWindow Opened(TempDirectory temp, out LocalLedger local, out List<AuditRow> opened)
    {
        local = LocalLedger.Open(temp.Path, SnapshotPolicy.Disabled);

        var composer = new EntryComposer(local.Ledger);
        composer.Compose(EntryCommandParser.Parse("Website (login) 09:00 1h", Day).Command!);

        var ran = new List<AuditRow>();
        opened = ran;

        var model = new AuditViewModel(local.Blobs, local.Ledger) { Open = ran.Add };
        var window = new AuditWindow { DataContext = model };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static AuditRow[] Rows(Window window) =>
        [.. window.FindControl<ListBox>("Rows")!.ItemsSource!.Cast<AuditRow>()];

    private static void Double(Window window, AuditRow row)
    {
        var item = window.FindControl<ListBox>("Rows")!
            .GetVisualDescendants()
            .OfType<ListBoxItem>()
            .First(item => item.DataContext is AuditRow candidate && candidate.Version == row.Version);

        var centre = item.TranslatePoint(new Point(item.Bounds.Width / 2, item.Bounds.Height / 2), window)
            ?? throw new InvalidOperationException("The row is not laid out.");

        window.MouseDown(centre, MouseButton.Left);
        window.MouseUp(centre, MouseButton.Left);
        window.MouseDown(centre, MouseButton.Left);
        window.MouseUp(centre, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void The_window_lists_every_change_newest_first()
    {
        using var temp = new TempDirectory("worksheet-audit");
        var window = Opened(temp, out var local, out _);

        Assert.Equal([3L, 2L, 1L], Rows(window).Select(row => row.Version));
        Assert.Equal("3 changes", window.FindControl<TextBlock>("Count")!.Text);

        local.Dispose();
    }

    [AvaloniaFact]
    public void Typing_into_the_filter_narrows_the_list()
    {
        using var temp = new TempDirectory("worksheet-audit");
        var window = Opened(temp, out var local, out _);

        var filter = window.FindControl<TextBox>("Filter")!;
        filter.Text = "entry.logged";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["entry.logged"], Rows(window).Select(row => row.Kind));
        Assert.Equal("1 of 3 changes", window.FindControl<TextBlock>("Count")!.Text);

        local.Dispose();
    }

    [AvaloniaFact]
    public void A_filter_that_holds_nothing_says_so_rather_than_looking_empty()
    {
        using var temp = new TempDirectory("worksheet-audit");
        var window = Opened(temp, out var local, out _);

        window.FindControl<TextBox>("Filter")!.Text = "nothing like this";
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(Rows(window));
        Assert.True(window.FindControl<TextBlock>("Empty")!.IsVisible);

        local.Dispose();
    }

    [AvaloniaFact]
    public void Double_clicking_a_row_opens_that_change()
    {
        using var temp = new TempDirectory("worksheet-audit");
        var window = Opened(temp, out var local, out var opened);

        Double(window, Rows(window)[0]);

        var row = Assert.Single(opened);
        Assert.Equal(3, row.Version);
        Assert.Contains(Environment.NewLine, row.Body);

        local.Dispose();
    }

    [AvaloniaFact]
    public void Escape_leaves_the_audit()
    {
        using var temp = new TempDirectory("worksheet-audit");
        var window = Opened(temp, out var local, out _);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.False(window.IsVisible);

        local.Dispose();
    }

    [AvaloniaFact]
    public void One_change_is_shown_whole_with_its_body_written_out()
    {
        using var temp = new TempDirectory("worksheet-audit");
        var window = Opened(temp, out var local, out _);

        var model = new BlobViewModel(Rows(window)[0]);
        var showing = new BlobWindow { DataContext = model };
        showing.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.StartsWith("Version 3", model.Title);
        Assert.Contains(Environment.NewLine, model.Body);

        var body = showing.FindControl<JsonView>("Body")!;
        Assert.NotNull(body.Inlines);
        Assert.NotEmpty(body.Inlines!);

        showing.Close();
        local.Dispose();
    }
}
