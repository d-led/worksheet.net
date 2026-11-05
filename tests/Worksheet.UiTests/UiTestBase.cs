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

    /// <summary>
    /// The window as it was drawn, kept under <c>artifacts/ui-tests</c> so the build can show what a
    /// test saw. A test that captures a frame is a test about the glass, not about the tree.
    /// </summary>
    protected Frame Shot(string name) =>
        Frame.Of(Window, Screens.PathFor(GetType().Name, name));

    /// <summary>A real click at the middle of the control, so hit testing and bindings both run.</summary>
    protected void Click(Control target)
    {
        Mouse.Click(Window, target);
        Settle();
    }

    protected static IEnumerable<T> Descendants<T>(Visual root) => root.GetVisualDescendants().OfType<T>();

    /// <summary>The rows of a day table: one border per entry, holes carrying their own class.</summary>
    protected static List<Border> EntryRows(Visual root) =>
        [.. Descendants<Border>(root).Where(border => border.Classes.Contains("row"))];

    /// <summary>One of the fields of a row, named by the class the template gives it.</summary>
    protected static TextBox Cell(Border row, string name) =>
        Descendants<TextBox>(row).Single(box => box.Classes.Contains(name));

    /// <summary>Where a control sits on the window, for a frame's pixels are in that same place.</summary>
    protected Point Centre(Control control) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), Window)
            ?? throw new InvalidOperationException($"{control.GetType().Name} is not laid out.");

    /// <summary>The stretch of a frame a control covers, so a test can ask what was drawn there.</summary>
    protected PixelRect AreaOf(Control control)
    {
        var topLeft = control.TranslatePoint(new Point(0, 0), Window)
            ?? throw new InvalidOperationException($"{control.GetType().Name} is not laid out.");

        return new PixelRect(
            (int)Math.Round(topLeft.X),
            (int)Math.Round(topLeft.Y),
            (int)Math.Round(control.Bounds.Width),
            (int)Math.Round(control.Bounds.Height));
    }
}
