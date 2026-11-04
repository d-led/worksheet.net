// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Worksheet.Desktop.ViewModels;
using Worksheet.Storage;

namespace Worksheet.Desktop.Views;

/// <summary>
/// The knobs. The window writes the file and then says so; what the application can apply while it
/// runs — the MCP port — is asked for by <see cref="Applied"/>.
/// </summary>
public partial class SettingsWindow : SecondaryWindow
{
    public SettingsWindow()
    {
        InitializeComponent();

        AddHandler(KeyDownEvent, EnterSaves, RoutingStrategies.Tunnel);
    }

    /// <summary>The caret starts where the window is about: the port, the one number a person changes.</summary>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        PortBox.Focus();
        PortBox.SelectAll();
    }

    /// <summary>Called after a save or a revert, so the application can apply what it can without a restart.</summary>
    public Action? Applied { get; set; }

    private SettingsViewModel Settings => (SettingsViewModel)DataContext!;

    /// <summary>A number typed in a field and meant is saved by the key that means it, not left for a click.</summary>
    private void EnterSaves(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.Source is not TextBox)
        {
            return;
        }

        Apply(Settings.Save());
        e.Handled = true;
    }

    private void Save_Click(object? sender, RoutedEventArgs e) => Apply(Settings.Save());

    private void Revert_Click(object? sender, RoutedEventArgs e)
    {
        Settings.Revert();
        Applied?.Invoke();
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

    private void Apply(AppSettings? saved)
    {
        if (saved is not null)
        {
            Applied?.Invoke();
        }
    }
}
