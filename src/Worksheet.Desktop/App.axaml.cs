// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Worksheet.Desktop.ViewModels;
using Worksheet.Desktop.Views;
using Worksheet.Storage;

namespace Worksheet.Desktop;

/// <summary>Composition only: the parts are built here and nothing else lives here.</summary>
public sealed class App : Application
{
    private const string InstanceName = "Worksheet.SingleInstance";

    private LocalLedger? _local;
    private MainWindow? _window;
    private SingleInstance? _instance;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (TryBecomeTheOnlyInstance())
            {
                Start(desktop);
            }
            else
            {
                // The store is single-writer, so a second instance would fail on the lock anyway.
                // Handing the window to the running instance is a later slice.
                desktop.Shutdown();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private bool TryBecomeTheOnlyInstance()
    {
        _instance = SingleInstance.TryAcquire(InstanceName);

        return _instance is not null;
    }

    private void Start(IClassicDesktopStyleApplicationLifetime desktop)
    {
        // Closing the window must not end the process: only the tray decides that.
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        desktop.Exit += (_, _) => Release();

        _local = LocalLedger.Open();
        _window = new MainWindow { DataContext = new DayViewModel(_local) };
        desktop.MainWindow = _window;

        TrayHost.Attach(this, ShowWindow, OpenDataFolder, () => Quit(desktop));

        // A binary started from a terminal is not a bundle, so macOS does not bring it forward.
        _window.Show();
        _window.Activate();
    }

    private DayViewModel? CurrentDay => _window?.DataContext as DayViewModel;

    private void ShowWindow()
    {
        if (_window is null)
        {
            return;
        }

        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    /// <summary>Shows the journal in the file manager: the folder exists once a ledger was opened.</summary>
    private static void OpenDataFolder()
    {
        var directory = LocalLedger.DefaultDirectory;

        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
    }

    private void Quit(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (_window is not null)
        {
            _window.AllowClose = true;
        }

        desktop.Shutdown();
    }

    private void Release()
    {
        _local?.Dispose();
        _instance?.Dispose();
    }
}
