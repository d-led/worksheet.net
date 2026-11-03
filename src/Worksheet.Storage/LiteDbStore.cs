// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Text.Json;
using LiteDB;
using Worksheet.Core;

namespace Worksheet.Storage;

/// <summary>
/// One LiteDB file holding the journal and its snapshots. Documents are written as explicit
/// <see cref="BsonDocument"/> fields rather than mapped objects: the column names are then part of
/// the file's contract, visible in any viewer, and nothing in the store depends on reflection.
/// </summary>
public sealed class LiteDbStore : IDisposable
{
    private readonly LiteDatabase _database;
    private readonly Lock _gate = new();

    public LiteDbStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Direct connection: one process owns the file. A second writer fails loudly instead of
        // interleaving, which is what the single-writer design wants.
        _database = new LiteDatabase(new ConnectionString
        {
            Filename = fullPath,
            Connection = ConnectionType.Direct,
        });

        Journal = new JournalCollection(_database, _gate);
        Snapshots = new SnapshotCollection(_database, _gate);
    }

    public IEventJournal Journal { get; }

    public ISnapshotStore Snapshots { get; }

    public void Dispose() => _database.Dispose();

    private static class Fields
    {
        public const string Version = "version";
        public const string Kind = "kind";
        public const string Payload = "payload";
        public const string RecordedAt = "recordedAt";
        public const string Origin = "origin";
        public const string State = "state";
    }

    private sealed class JournalCollection(LiteDatabase database, Lock gate) : IEventJournal
    {
        private ILiteCollection<BsonDocument> Documents => database.GetCollection("journal");

        public long Version
        {
            get
            {
                lock (gate)
                {
                    var last = Documents.FindAll().Select(document => document[Fields.Version].AsInt64).DefaultIfEmpty(0).Max();
                    return last;
                }
            }
        }

        public IReadOnlyList<JournalRecord> ReadFrom(long versionExclusive)
        {
            lock (gate)
            {
                return
                [
                    .. Documents
                        .Find(Query.GT(Fields.Version, versionExclusive))
                        .OrderBy(document => document[Fields.Version].AsInt64)
                        .Select(ToRecord),
                ];
            }
        }

        public void Append(IReadOnlyList<LedgerEvent> changes, long expectedVersion, ChangeOrigin origin)
        {
            ArgumentNullException.ThrowIfNull(changes);

            lock (gate)
            {
                var current = Version;

                if (current != expectedVersion)
                {
                    throw new JournalConcurrencyException(expectedVersion, current);
                }

                var recordedAt = DateTime.UtcNow;
                var version = current;

                database.BeginTrans();

                try
                {
                    foreach (var change in changes)
                    {
                        version++;

                        Documents.Insert(new BsonDocument
                        {
                            ["_id"] = version,
                            [Fields.Version] = version,
                            [Fields.Kind] = EventCodec.KindOf(change),
                            [Fields.Payload] = EventCodec.Encode(change),
                            [Fields.RecordedAt] = recordedAt,
                            [Fields.Origin] = origin.ToString(),
                        });
                    }

                    database.Commit();
                }
                catch
                {
                    database.Rollback();
                    throw;
                }
            }
        }

        private static JournalRecord ToRecord(BsonDocument document) =>
            new(
                document[Fields.Version].AsInt64,
                EventCodec.Decode(document[Fields.Kind].AsString, document[Fields.Payload].AsString),
                new DateTimeOffset(DateTime.SpecifyKind(document[Fields.RecordedAt].AsDateTime, DateTimeKind.Utc)),
                Enum.Parse<ChangeOrigin>(document[Fields.Origin].AsString));
    }

    private sealed class SnapshotCollection(LiteDatabase database, Lock gate) : ISnapshotStore
    {
        private ILiteCollection<BsonDocument> Documents => database.GetCollection("snapshots");

        public LedgerState? LoadLatest()
        {
            lock (gate)
            {
                var latest = Documents.Find(Query.All(Fields.Version, Query.Descending)).FirstOrDefault();

                if (latest is null)
                {
                    return null;
                }

                var snapshot = System.Text.Json.JsonSerializer
                    .Deserialize<LedgerSnapshot>(latest[Fields.State].AsString, LedgerJson.Options)
                    ?? throw new JournalIntegrityException("A snapshot has an empty body.");

                return LedgerState.Restore(snapshot);
            }
        }

        public void Save(LedgerState state)
        {
            ArgumentNullException.ThrowIfNull(state);

            lock (gate)
            {
                var body = System.Text.Json.JsonSerializer.Serialize(state.ToSnapshot(), LedgerJson.Options);

                database.BeginTrans();

                try
                {
                    Documents.DeleteAll();
                    Documents.Insert(new BsonDocument
                    {
                        ["_id"] = state.Version,
                        [Fields.Version] = state.Version,
                        [Fields.RecordedAt] = DateTime.UtcNow,
                        [Fields.State] = body,
                    });
                    database.Commit();
                }
                catch
                {
                    database.Rollback();
                    throw;
                }
            }
        }
    }
}
