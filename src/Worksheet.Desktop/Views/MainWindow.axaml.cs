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

    /// <summary>Set only when the tray asked to quit; a person closing the window means "later".</summary>
    public bool AllowClose { get; set; }

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
    /// Closing puts the day aside rather than ending it. On macOS that means the Dock — a minimised
    /// window, which the application's own icon brings back when it is clicked, since an application
    /// with no visible window gets no other signal that its icon was used. Elsewhere the tray owns
    /// the click, so the window simply hides.
    /// </summary>
    private void HideInsteadOfClosing(object? sender, WindowClosingEventArgs e)
    {
        if (AllowClose)
        {
            return;
        }

        e.Cancel = true;

        if (OperatingSystem.IsMacOS())
        {
            WindowState = WindowState.Minimized;
            return;
        }

        Hide();
    }
}
