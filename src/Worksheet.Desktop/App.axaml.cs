// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Worksheet.Desktop.ViewModels;
using Worksheet.Desktop.Views;
using Worksheet.Mcp;
using Worksheet.Storage;

namespace Worksheet.Desktop;

/// <summary>Composition only: the parts are built here and nothing else lives here.</summary>
public sealed class App : Application
{
    private const string InstanceName = "Worksheet.SingleInstance";

    private LocalLedger? _local;
    private MainWindow? _window;
    private SingleInstance? _instance;
    private McpHttpHost? _mcp;

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
        var day = new DayViewModel(_local);
        _window = new MainWindow { DataContext = day };
        desktop.MainWindow = _window;

        TrayHost.Attach(this, ShowWindow, OpenDataFolder, () => Quit(desktop));

        StartMcp(day, _local);

        // A binary started from a terminal is not a bundle, so macOS does not bring it forward.
        _window.Show();
        _window.Activate();
    }

    private DayViewModel? CurrentDay => _window?.DataContext as DayViewModel;

    /// <summary>
    /// Starts the MCP server on the loopback. Every call is run on the UI thread, because the ledger
    /// has one writer and the window is reading the same fold; the day is re-read only when a call
    /// changed the journal. A port someone else holds is said in the window, and the application
    /// keeps working without its server.
    /// </summary>
    private void StartMcp(DayViewModel day, LocalLedger local)
    {
        var settings = McpSettings.FromEnvironment();

        if (!settings.Enabled)
        {
            return;
        }

        var toolkit = new SerialToolkit(
            new WorksheetTools(local.Ledger),
            work => Dispatcher.UIThread.Invoke(() =>
            {
                var version = local.Ledger.Version;
                var outcome = work();

                if (local.Ledger.Version != version)
                {
                    day.Reload();
                }

                return outcome;
            }));

        _mcp = McpHttpHost.TryStart(new McpServer(toolkit), settings.Port, out var problem);

        if (problem is not null)
        {
            day.Say($"{problem} Set {McpSettings.PortVariable} to another port.", isProblem: true);
        }
    }

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
        _mcp?.Dispose();
        _mcp = null;
        _local?.Dispose();
        _instance?.Dispose();
    }
}
