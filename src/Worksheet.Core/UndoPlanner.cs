// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace Worksheet.Core;

/// <summary>
/// Undo is the inverse change, not a state stack. Every change records enough to invert itself
/// exactly, which makes this total and testable without a store.
/// </summary>
public static class UndoPlanner
{
    public static LedgerEvent? Reverse(LedgerEvent change) =>
        change switch
        {
            EntryLogged logged => new EntryRetracted(logged.Entry),
            EntryRetracted retracted => new EntryLogged(retracted.Entry),
            EntryRevised revised => new EntryRevised(revised.After, revised.Before),
            DayKindChanged changed => new DayKindChanged(changed.Date, changed.After, changed.Before),
            ProjectRenamed renamed => new ProjectRenamed(renamed.ProjectId, renamed.After, renamed.Before),
            TopicRenamed renamed => new TopicRenamed(renamed.TopicId, renamed.After, renamed.Before),
            ProjectRemoved removed => new ProjectRestored(removed.Project, removed.Topics, removed.Before, removed.After),
            ProjectRestored restored => new ProjectRemoved(restored.Project, restored.Topics, restored.Before, restored.After),
            TopicRemoved removed => new TopicRestored(removed.Topic, removed.Before, removed.After),
            TopicRestored restored => new TopicRemoved(restored.Topic, restored.Before, restored.After),
            TopicMoved moved => new TopicMoved(moved.TopicId, moved.ToProjectId, moved.FromProjectId),
            Batch batch => Reverse(batch),
            _ => null,
        };

    /// <summary>
    /// A batch is undone part by part, latest first, so a change that depends on the one before it is
    /// taken back in the order that keeps the fold consistent. A batch holding something that cannot
    /// be undone cannot be undone either, and says so by answering nothing.
    /// </summary>
    private static LedgerEvent? Reverse(Batch batch)
    {
        var inverses = new List<LedgerEvent>(batch.Changes.Length);

        foreach (var part in batch.Changes.Reverse())
        {
            if (Reverse(part) is not { } inverse)
            {
                return null;
            }

            inverses.Add(inverse);
        }

        return new Batch([.. inverses]);
    }
}
