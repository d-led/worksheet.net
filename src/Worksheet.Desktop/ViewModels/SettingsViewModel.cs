// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Worksheet.Storage;

namespace Worksheet.Desktop.ViewModels;

/// <summary>
/// The knobs, as the settings view shows them: the value in force, where it came from, and what a
/// person typed over it. Saving writes the file and answers what the application should apply now;
/// reverting forgets the choice, so the environment and the built-in value decide again.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsResolver _resolver;
    private ResolvedSettings _resolved;

    [ObservableProperty]
    private string _portText = string.Empty;

    [ObservableProperty]
    private string _snapshotText = string.Empty;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _messageIsProblem;

    public SettingsViewModel(SettingsResolver resolver, string directory)
    {
        ArgumentNullException.ThrowIfNull(resolver);

        _resolver = resolver;
        DataDirectory = directory;
        _resolved = resolver.Resolve();

        Load();
    }

    /// <summary>Where the journal and this file live; shown, not changed — moving it is a restart's job.</summary>
    public string DataDirectory { get; }

    public string PortSource => Source(_resolved.McpPortSource, SettingsResolver.McpPortVariable);

    public string SnapshotSource => Source(_resolved.SnapshotSource, SettingsResolver.SnapshotVariable);

    public bool HasMessage => !string.IsNullOrEmpty(Message);

    partial void OnMessageChanged(string? value) => OnPropertyChanged(nameof(HasMessage));

    /// <summary>Writes what was typed, and answers the values to apply now — or nothing and why not.</summary>
    public AppSettings? Save()
    {
        if (Number(PortText) is not { } port || Number(SnapshotText) is not { } every)
        {
            Message = "Both fields take a whole number.";
            MessageIsProblem = true;
            return null;
        }

        var values = new AppSettings(port, every);

        if (SettingsResolver.WhyNot(values) is { } problem)
        {
            Message = problem;
            MessageIsProblem = true;
            return null;
        }

        _resolver.Store(values);
        Reload();

        Message = "Saved.";
        MessageIsProblem = false;

        return values;
    }

    /// <summary>Forgets the choice made here; what the environment or the built-in value says is in force again.</summary>
    public AppSettings Revert()
    {
        _resolver.Forget();
        Reload();

        Message = "Forgotten; the environment or the built-in value decides again.";
        MessageIsProblem = false;

        return _resolved.Values;
    }

    private void Reload()
    {
        _resolved = _resolver.Resolve();

        Load();
    }

    private void Load()
    {
        PortText = _resolved.Values.McpPort.ToString(CultureInfo.InvariantCulture);
        SnapshotText = _resolved.Values.SnapshotEvery.ToString(CultureInfo.InvariantCulture);

        OnPropertyChanged(nameof(PortSource));
        OnPropertyChanged(nameof(SnapshotSource));
    }

    private static int? Number(string text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static string Source(SettingSource source, string variable) =>
        source switch
        {
            SettingSource.Stored => "chosen here",
            SettingSource.Environment => $"from {variable}",
            _ => "built-in",
        };
}
