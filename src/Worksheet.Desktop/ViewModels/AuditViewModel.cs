// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Worksheet.Core;

namespace Worksheet.Desktop.ViewModels;

/// <summary>One stored change as the audit lists it: what was written, when, and from where.</summary>
public sealed record AuditRow(JournalBlob Blob)
{
    public long Version => Blob.Version;

    public string Kind => Blob.Kind;

    public string Origin => Blob.Origin.ToString();

    public string When => Blob.RecordedAt.ToLocalTime().ToString("d MMM yyyy HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>The head of the body, so a row says something before it is opened.</summary>
    public string Glimpse => Blob.Payload.Length <= 90 ? Blob.Payload : Blob.Payload[..90] + "…";

    /// <summary>The whole body, written out with an indent for reading.</summary>
    public string Body => JsonText.Pretty(Blob.Payload);
}

/// <summary>
/// The journal itself, as a person auditing it reads it: every change the file holds, newest first,
/// narrowed by a free-text filter whose words must all appear. Nothing here folds or interprets —
/// the point of looking is to see what was written, not what was made of it — and a change is opened
/// for its body, since the body is the only place the details live.
/// </summary>
public sealed partial class AuditViewModel : ObservableObject, IDisposable
{
    private readonly IDisposable _untilTheLedgerChanges;
    private readonly Func<IReadOnlyList<JournalBlob>> _read;
    private IReadOnlyList<JournalBlob> _all = [];

    [ObservableProperty]
    private string _filter = string.Empty;

    public AuditViewModel(Func<IReadOnlyList<JournalBlob>> read, IObservable<LedgerState> ledger)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(ledger);

        _read = read;
        _untilTheLedgerChanges = ledger.Subscribe(new Watch(_ => Refresh()));
        Refresh();
    }

    public void Dispose() => _untilTheLedgerChanges.Dispose();

    /// <summary>What the filter kept, newest first.</summary>
    public ObservableCollection<AuditRow> Rows { get; } = [];

    /// <summary>How the list reads at the moment, in the words a person uses for it.</summary>
    public string Count =>
        Rows.Count == _all.Count
            ? $"{_all.Count} changes"
            : $"{Rows.Count} of {_all.Count} changes";

    public bool HasRows => Rows.Count > 0;

    /// <summary>What opening a row does; the shell sets it, since the shell owns the windows.</summary>
    public Action<AuditRow>? Open { get; set; }

    partial void OnFilterChanged(string value) => Refresh();

    private void Refresh()
    {
        _all = _read();
        Rows.Clear();

        foreach (var blob in BlobFilter.Apply(_all, Filter).OrderByDescending(blob => blob.Version))
        {
            Rows.Add(new AuditRow(blob));
        }

        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(HasRows));
    }
}
