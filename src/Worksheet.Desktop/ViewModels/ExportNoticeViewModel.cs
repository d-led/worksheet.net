// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using CommunityToolkit.Mvvm.Input;

namespace Worksheet.Desktop.ViewModels;

/// <summary>
/// One export that was written: the file, the folder it went to, and the two things a person may
/// want next — to be shown the file, or to put the news away. A save dialog already asked where the
/// file goes, so the window says where it landed rather than asking again.
/// </summary>
public sealed class ExportNoticeViewModel
{
    public ExportNoticeViewModel(string path, Action<string>? reveal, Action dismiss)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(dismiss);

        FullPath = path;
        FileName = System.IO.Path.GetFileName(path);
        Folder = System.IO.Path.GetDirectoryName(path) ?? path;
        RevealCommand = new RelayCommand(() => reveal?.Invoke(FullPath), () => reveal is not null);
        DismissCommand = new RelayCommand(dismiss);
    }

    public string FullPath { get; }

    public string FileName { get; }

    public string Folder { get; }

    /// <summary>
    /// Shows the file in the platform's file manager. The window says what that means; while nothing
    /// can show it — no window, a test — the command says so instead of doing nothing.
    /// </summary>
    public IRelayCommand RevealCommand { get; }

    public IRelayCommand DismissCommand { get; }
}
