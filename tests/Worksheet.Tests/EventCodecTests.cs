// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;
using Worksheet.Storage;

namespace Worksheet.Tests;

/// <summary>The journal's wire format: what a stored change looks like when it is read back.</summary>
public sealed class EventCodecTests
{
    private static readonly DateOnly Day = new(2026, 11, 3);
    private static readonly Guid Project = Guid.NewGuid();
    private static readonly Guid Topic = Guid.NewGuid();

    private static Entry TheEntry() =>
        new()
        {
            Id = EntryId.Create(),
            ProjectId = Project,
            TopicId = Topic,
            Date = Day,
            Start = new TimeOnly(9, 0),
            Duration = TimeSpan.FromMinutes(90),
            Note = "first light",
        };

    private static LedgerEvent[] EveryChange()
    {
        var entry = TheEntry();

        return
        [
            new ProjectOpened(Project, "Website"),
            new TopicAdded(Topic, Project, "login"),
            new EntryLogged(entry),
            new EntryRevised(entry, entry with { Duration = TimeSpan.FromMinutes(120) }),
            new EntryRetracted(entry),
            new DayKindChanged(Day, DayKind.Working, DayKind.Sick),
            new ProjectRenamed(Project, "Website", "Web"),
            new TopicRenamed(Topic, "login", "sign-in"),
            new TopicMoved(Topic, Project, Guid.NewGuid()),
        ];
    }

    /// <summary>The changes that carry entries compare by identity of their arrays, not by value.</summary>
    private static LedgerEvent[] EveryRemoval()
    {
        var entry = TheEntry();
        Entry[] before = [entry];
        Entry[] after = [entry with { ProjectId = Guid.Empty, TopicId = null }];
        Project project = new(Project, "Website", Archived: false);
        Topic topic = new(Topic, Project, "login", Archived: false);
        Entry[] unTopiced = [entry with { TopicId = null }];

        return
        [
            new ProjectRemoved(project, [topic], before, after),
            new ProjectRestored(project, [topic], before, after),
            new TopicRemoved(topic, before, unTopiced),
            new TopicRestored(topic, before, unTopiced),
            new Batch([new TopicRemoved(topic, before, unTopiced), new TopicMoved(Topic, Project, Guid.NewGuid())]),
        ];
    }

    public static TheoryData<LedgerEvent> Removals()
    {
        var data = new TheoryData<LedgerEvent>();

        foreach (var change in EveryRemoval())
        {
            data.Add(change);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Removals))]
    public void A_removal_survives_encoding_and_decoding(LedgerEvent change)
    {
        var decoded = EventCodec.Decode(EventCodec.KindOf(change), EventCodec.Encode(change));

        Assert.Equal(change.GetType(), decoded.GetType());
        Assert.Equal(EventCodec.KindOf(change), EventCodec.KindOf(decoded));
        Assert.Contains('.', EventCodec.KindOf(change));
        Assert.Equal(EventCodec.Encode(change), EventCodec.Encode(decoded));
    }

    public static TheoryData<LedgerEvent> Changes()
    {
        var data = new TheoryData<LedgerEvent>();

        foreach (var change in EveryChange())
        {
            data.Add(change);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Changes))]
    public void A_change_survives_encoding_and_decoding(LedgerEvent change)
    {
        var kind = EventCodec.KindOf(change);

        Assert.Equal(change, EventCodec.Decode(kind, EventCodec.Encode(change)));
    }

    [Fact]
    public void Every_kind_has_its_own_name()
    {
        var kinds = EveryChange().Select(EventCodec.KindOf).ToList();

        Assert.Equal(kinds.Count, kinds.Distinct().Count());
        Assert.All(kinds, kind => Assert.Contains('.', kind));
    }

    [Fact]
    public void An_unknown_kind_is_an_integrity_problem_not_a_silent_skip()
    {
        var decoding = () => EventCodec.Decode("something.else", "{}");

        var failure = Assert.Throws<JournalIntegrityException>(decoding);
        Assert.Contains("something.else", failure.Message);
    }

    [Fact]
    public void A_change_the_store_cannot_name_is_refused_before_it_is_written()
    {
        var naming = () => EventCodec.KindOf(new UnknownChange());

        Assert.Throws<JournalIntegrityException>(naming);
    }

    [Fact]
    public void An_entry_id_is_stored_as_a_plain_string()
    {
        var entry = TheEntry();

        var json = EventCodec.Encode(new EntryLogged(entry));

        Assert.Contains(entry.Id.Value.ToString(), json);
        Assert.DoesNotContain("\"value\"", json);
    }

    [Fact]
    public void A_kind_of_day_is_stored_by_its_name()
    {
        var json = EventCodec.Encode(new DayKindChanged(Day, null, DayKind.Sick));

        Assert.Contains("\"Sick\"", json);
        Assert.DoesNotContain("\"before\"", json);
    }

    private sealed record UnknownChange : LedgerEvent;
}
