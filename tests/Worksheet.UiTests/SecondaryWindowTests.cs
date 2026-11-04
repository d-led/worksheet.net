// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Worksheet.Core;
using Worksheet.Desktop.ViewModels;
using Worksheet.Desktop.Views;
using Worksheet.Mcp;
using Worksheet.Storage;
using Worksheet.TestSupport;

namespace Worksheet.UiTests;

/// <summary>
/// The windows that stand beside the day — settings, the agent connection, the catalogue — leave on
/// Escape, except where a field has already taken that key for itself.
/// </summary>
public sealed class SecondaryWindowTests
{
    private static readonly AppSettings Defaults = new(McpPort: 5317, SnapshotEvery: 500);

    private static void Escape(Window window)
    {
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    private static bool IsOpen(Window window) => window.IsVisible;

    [AvaloniaFact]
    public void Settings_leaves_on_escape()
    {
        using var temp = new TempDirectory("worksheet-secondary");
        var window = new SettingsWindow
        {
            DataContext = new SettingsViewModel(new SettingsResolver(temp.Path, _ => null, Defaults), temp.Path),
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Escape(window);

        Assert.False(IsOpen(window));
    }

    [AvaloniaFact]
    public void The_agent_connection_leaves_on_escape()
    {
        var window = new ConnectWindow
        {
            DataContext = new ConnectViewModel(new McpSettings(5317), "Listening on 127.0.0.1:5317.", isProblem: false),
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Escape(window);

        Assert.False(IsOpen(window));
    }

    [AvaloniaFact]
    public void The_catalogue_leaves_on_escape()
    {
        using var temp = new TempDirectory("worksheet-secondary");
        using var local = LocalLedger.Open(temp.Path, SnapshotPolicy.Disabled);
        var window = new ManageWindow { DataContext = new ManageViewModel(local.Ledger) };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Escape(window);

        Assert.False(IsOpen(window));
    }

    [AvaloniaFact]
    public void A_name_being_typed_is_put_back_first_and_the_same_key_then_leaves()
    {
        using var temp = new TempDirectory("worksheet-secondary");
        using var local = LocalLedger.Open(temp.Path, SnapshotPolicy.Disabled);

        new EntryComposer(local.Ledger).Compose(
            EntryCommandParser.Parse("Website 09:00 1h", new DateOnly(2026, 11, 3)).Command!);

        var window = new ManageWindow { DataContext = new ManageViewModel(local.Ledger) };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var field = window.GetVisualDescendants()
            .OfType<TextBox>()
            .Single(box => box.DataContext is CatalogueRow row && row.Name == "Website");

        field.Focus();
        field.Text = "Client portal";
        Dispatcher.UIThread.RunJobs();

        Escape(window);

        Assert.True(IsOpen(window));
        Assert.Equal("Website", field.Text);

        Escape(window);

        Assert.False(IsOpen(window));
    }
}
