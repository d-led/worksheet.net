// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Worksheet.Core;
using Worksheet.Desktop.ViewModels;
using Worksheet.Storage;
using Worksheet.TestSupport;

namespace Worksheet.UiTests;

/// <summary>
/// One component, one window, one real day view model over a throwaway store. The application is
/// never started: no tray, no single-instance guard, no shared state between tests.
/// </summary>
public abstract class UiTestBase : IDisposable
{
    /// <summary>A Tuesday, so no test is frozen by the day the suite happens to run on.</summary>
    protected static readonly DateOnly Today = new(2026, 11, 3);

    private readonly TempDirectory _temp = new("worksheet-ui");

    protected UiTestBase() => Ledger = LocalLedger.Open(_temp.Path, SnapshotPolicy.Disabled);

    protected LocalLedger Ledger { get; }

    protected Window Window { get; private set; } = null!;

    public void Dispose()
    {
        Ledger.Dispose();
        _temp.Dispose();
        GC.SuppressFinalize(this);
    }

    protected DayViewModel NewDay() => new(Ledger, Today);

    protected TControl Show<TControl>(DayViewModel day)
        where TControl : Control, new() => Show(day, new TControl());

    /// <summary>Shows a control the test built itself, e.g. two components in one window.</summary>
    protected TControl Show<TControl>(DayViewModel day, TControl control)
        where TControl : Control
    {
        control.DataContext = day;

        Window = new Window { Content = control, Width = 1000, Height = 640 };
        Window.Show();
        Settle();

        return control;
    }

    protected static void Seed(DayViewModel day, string line)
    {
        day.EntryText = line;
        day.LogCommand.Execute(null);
        Settle();
    }

    protected static void Settle() => Dispatcher.UIThread.RunJobs();

    /// <summary>A real click at the middle of the control, so hit testing and bindings both run.</summary>
    protected void Click(Control target)
    {
        Mouse.Click(Window, target);
        Settle();
    }

    protected static IEnumerable<T> Descendants<T>(Visual root) => root.GetVisualDescendants().OfType<T>();
}
