// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Text.Json;
using Worksheet.Core;

namespace Worksheet.Storage;

/// <summary>
/// Turns a change into a stored kind and a JSON body, and back. The kind is stored next to the
/// body rather than inside it, so the journal is greppable and old rows stay readable when the
/// property names of an event evolve.
/// </summary>
internal static class EventCodec
{
    private const string ProjectOpenedKind = "project.opened";
    private const string TopicAddedKind = "topic.added";
    private const string EntryLoggedKind = "entry.logged";
    private const string EntryRevisedKind = "entry.revised";
    private const string EntryRetractedKind = "entry.retracted";
    private const string DayKindChangedKind = "day.kind-changed";

    public static string KindOf(LedgerEvent change) =>
        change switch
        {
            ProjectOpened => ProjectOpenedKind,
            TopicAdded => TopicAddedKind,
            EntryLogged => EntryLoggedKind,
            EntryRevised => EntryRevisedKind,
            EntryRetracted => EntryRetractedKind,
            DayKindChanged => DayKindChangedKind,
            _ => throw new JournalIntegrityException($"{change.GetType().Name} has no stored kind."),
        };

    public static string Encode(LedgerEvent change) =>
        JsonSerializer.Serialize(change, change.GetType(), LedgerJson.Options);

    public static LedgerEvent Decode(string kind, string payload) =>
        kind switch
        {
            ProjectOpenedKind => Read<ProjectOpened>(kind, payload),
            TopicAddedKind => Read<TopicAdded>(kind, payload),
            EntryLoggedKind => Read<EntryLogged>(kind, payload),
            EntryRevisedKind => Read<EntryRevised>(kind, payload),
            EntryRetractedKind => Read<EntryRetracted>(kind, payload),
            DayKindChangedKind => Read<DayKindChanged>(kind, payload),
            _ => throw new JournalIntegrityException($"The journal holds a change of unknown kind '{kind}'."),
        };

    private static T Read<T>(string kind, string payload) =>
        JsonSerializer.Deserialize<T>(payload, LedgerJson.Options)
        ?? throw new JournalIntegrityException($"A change of kind '{kind}' has an empty body.");
}
