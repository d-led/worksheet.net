// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using CommunityToolkit.Mvvm.Input;

namespace Worksheet.Desktop.ViewModels;

/// <summary>
/// One export that was written: the file, the folder it went to, and the two things a person may
/// want next — to be shown the file, or to put the news away. A save dialog already asked where the
/// file goes, so the window says where it landed rather than asking again. Showing the file answers
/// the only question the notice asks, so it puts the notice away as well.
/// </summary>
public sealed class ExportNoticeViewModel
{
    private readonly Action<string>? _reveal;
    private readonly Action _dismiss;

    public ExportNoticeViewModel(string path, Action<string>? reveal, Action dismiss)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(dismiss);

        _reveal = reveal;
        _dismiss = dismiss;
        FullPath = path;
        FileName = System.IO.Path.GetFileName(path);
        Folder = System.IO.Path.GetDirectoryName(path) ?? path;
        RevealCommand = new RelayCommand(Show, () => _reveal is not null);
        DismissCommand = new RelayCommand(_dismiss);
    }

    private void Show()
    {
        if (_reveal is null)
        {
            return;
        }

        _reveal(FullPath);
        _dismiss();
    }

    public string FullPath { get; }

    public string FileName { get; }

    public string Folder { get; }

    /// <summary>
    /// Shows the file in the platform's file manager, then puts the notice away: it has just said
    /// where the export went, which is the whole of its news. The window says what showing a file
    /// means; while nothing can show it — no window, a test — the command says so instead of doing
    /// nothing.
    /// </summary>
    public IRelayCommand RevealCommand { get; }

    public IRelayCommand DismissCommand { get; }
}
