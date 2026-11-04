// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;

namespace Worksheet.Storage;

/// <summary>Opens the ledger in the per-user data directory. The owner of the process owns the file.</summary>
public sealed class LocalLedger : IDisposable
{
    /// <summary>Snapshot after this many changes; the journal stays the truth either way.</summary>
    public const int SnapshotEveryChanges = 500;

    private readonly LiteDbStore _store;

    private LocalLedger(LiteDbStore store, Ledger ledger)
    {
        _store = store;
        Ledger = ledger;
    }

    public Ledger Ledger { get; }

    /// <summary>The journal file itself.</summary>
    public string File => _store.FilePath;

    /// <summary>The folder the journal sits in, and where its weekly copies go.</summary>
    public string Folder => Path.GetDirectoryName(_store.FilePath)!;

    /// <summary>Writes a copy of the journal that another instance can open.</summary>
    public void CopyTo(string target) => _store.CopyTo(target);

    /// <summary>
    /// Where the journal lives. <c>WORKSHEET_DATA_DIR</c> overrides the per-user location, which is
    /// what makes a portable copy and an isolated test run possible.
    /// </summary>
    public static string DefaultDirectory => DataDirectory(Environment.GetEnvironmentVariable(DataDirectoryVariable));

    internal static string DataDirectory(string? overridden) =>
        string.IsNullOrWhiteSpace(overridden)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Worksheet")
            : overridden.Trim();

    private const string DataDirectoryVariable = "WORKSHEET_DATA_DIR";

    public static LocalLedger Open(string? directory = null, SnapshotPolicy? policy = null)
    {
        var target = directory ?? DefaultDirectory;
        var store = new LiteDbStore(Path.Combine(target, "ledger.db"));

        try
        {
            var ledger = Ledger.Open(store.Journal, store.Snapshots, policy ?? SnapshotPolicy.EveryChanges(SnapshotEveryChanges));
            return new LocalLedger(store, ledger);
        }
        catch
        {
            store.Dispose();
            throw;
        }
    }

    public void Dispose() => _store.Dispose();
}
