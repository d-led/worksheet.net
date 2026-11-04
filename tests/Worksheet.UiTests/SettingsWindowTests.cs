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
using Worksheet.Desktop.ViewModels;
using Worksheet.Desktop.Views;
using Worksheet.Storage;
using Worksheet.TestSupport;

namespace Worksheet.UiTests;

/// <summary>
/// The settings window: the values in force are on screen with where they came from, and saving
/// writes what was typed — and asks the application to apply it.
/// </summary>
public sealed class SettingsWindowTests
{
    private static readonly AppSettings Defaults = new(McpPort: 5317, SnapshotEvery: 500);

    private static SettingsWindow Opened(TempDirectory temp, out SettingsResolver resolver, Action applied)
    {
        resolver = new SettingsResolver(temp.Path, _ => null, Defaults);

        var window = new SettingsWindow
        {
            DataContext = new SettingsViewModel(resolver, temp.Path),
            Applied = applied,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static void Click(Window window, Button button) => Mouse.Click(window, button);

    [AvaloniaFact]
    public void The_window_shows_the_values_in_force_and_where_they_came_from()
    {
        using var temp = new TempDirectory("worksheet-settings-window");

        var window = Opened(temp, out _, () => { });

        Assert.Equal("5317", window.FindControl<TextBox>("PortBox")!.Text);
        Assert.Equal("500", window.FindControl<TextBox>("SnapshotBox")!.Text);
        Assert.Equal("built-in", window.FindControl<TextBlock>("PortSource")!.Text);
        Assert.Equal(temp.Path, window.FindControl<TextBlock>("DataDirectory")!.Text);
    }

    [AvaloniaFact]
    public void Saving_writes_the_number_and_asks_the_application_to_apply_it()
    {
        using var temp = new TempDirectory("worksheet-settings-window");
        var applied = 0;

        var window = Opened(temp, out var resolver, () => applied++);
        window.FindControl<TextBox>("PortBox")!.Text = "7000";

        Click(window, window.FindControl<Button>("Save")!);

        Assert.Equal(1, applied);
        Assert.Equal(7000, resolver.Resolve().Values.McpPort);
        Assert.Equal("Saved.", window.FindControl<TextBlock>("Message")!.Text);
    }
}
