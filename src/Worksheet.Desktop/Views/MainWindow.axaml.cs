// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SukiUI.Controls;
using Worksheet.Desktop.ViewModels;
using System.Windows.Input;

namespace Worksheet.Desktop.Views;

public partial class MainWindow : SukiWindow
{
    public MainWindow()
    {
        InitializeComponent();

        Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Worksheet.Desktop/Assets/app.png")));
        Closing += HideInsteadOfClosing;
    }

    /// <summary>
    /// Set only when leaving is meant — the tray asked to quit, or the system is shutting down; a
    /// person closing the window means "later".
    /// </summary>
    public bool AllowClose { get; set; }

    /// <summary>
    /// Set by the shell: what closing means besides hiding. The window asks before it disappears,
    /// because vanishing without a word reads as having been closed for good.
    /// </summary>
    public ClosingProcedure? CloseProcedure { get; set; }

    /// <summary>
    /// The arrows walk the day, unless the caret is in a field: an empty field leaves a left arrow
    /// unhandled, so the focus decides rather than whether the key was taken.
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled || Typing() || DayCommand(e.Key) is not { } command)
        {
            return;
        }

        command.Execute(null);
        e.Handled = true;
    }

    /// <summary>Whether what has the focus is a text field, which keeps the arrows for its caret.</summary>
    private bool Typing() => FocusManager?.GetFocusedElement() is TextBox or AutoCompleteBox;

    /// <summary>The day the key walks to, or nothing when it walks nowhere.</summary>
    private ICommand? DayCommand(Key key) =>
        DataContext is DayViewModel day
            ? key switch
            {
                Key.Left => day.PreviousDayCommand,
                Key.Right => day.NextDayCommand,
                _ => null,
            }
            : null;

    /// <summary>
    /// Puts the window aside, saying so the first time. This is what closing means, and what the
    /// system's own Quit means: the day keeps counting, and only the tray's Quit ends the process.
    /// </summary>
    public void PutAside()
    {
        if (IsVisible && CloseProcedure is { } procedure && !procedure.Noticed())
        {
            procedure.Notice(this);
            return;
        }

        Hide();
    }

    private void HideInsteadOfClosing(object? sender, WindowClosingEventArgs e)
    {
        // The machine is going down: nothing is gained by being the application that would not let
        // it. Everything else — the close button, and the system's own Quit, which arrives as an
        // application shutdown — is answered by putting the window aside.
        if (AllowClose || e.CloseReason == WindowCloseReason.OSShutdown)
        {
            return;
        }

        e.Cancel = true;

        PutAside();
    }
}
