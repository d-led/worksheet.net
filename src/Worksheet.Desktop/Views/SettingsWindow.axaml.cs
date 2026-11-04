// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

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
    public SettingsWindow() => InitializeComponent();

    /// <summary>Called after a save or a revert, so the application can apply what it can without a restart.</summary>
    public Action? Applied { get; set; }

    private SettingsViewModel Settings => (SettingsViewModel)DataContext!;

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
