// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Worksheet.Core;

namespace Worksheet.Desktop.ViewModels;

/// <summary>
/// One row of the day. It shows absolute times — a start and an end — because that is what a day
/// looks like; the length is derived and only the entry language accepts it. The project and its
/// topic are typed rather than chosen from a fixed list: a name nobody used yet is opened by the
/// same rules a line uses. Every edit is a correction of the whole entry, and a refused one puts
/// the editors back.
/// </summary>
public sealed partial class EntryRowViewModel : ObservableObject, IDayRow
{
    private readonly Func<LedgerState> _state;
    private readonly Func<Entry, Entry?> _revise;
    private readonly Func<Entry, string, string?, ComposeResult> _point;
    private bool _loading;

    [ObservableProperty]
    private string _startText = string.Empty;

    [ObservableProperty]
    private string _endText = string.Empty;

    [ObservableProperty]
    private string _projectText = string.Empty;

    [ObservableProperty]
    private string _topicText = string.Empty;

    /// <summary>
    /// Whether the topic offers are shown. A break is a break, not a topic of one, so its topic
    /// box is closed rather than inviting a name the ledger would refuse.
    /// </summary>
    [ObservableProperty]
    private bool _topicPicker = true;

    [ObservableProperty]
    private string _note = string.Empty;

    /// <summary>The row's tint: its project's pastel, so a day can be read down its left edge.</summary>
    [ObservableProperty]
    private IBrush? _tint;

    /// <summary>True on a break: the one row that is not work, and is washed rather than striped.</summary>
    [ObservableProperty]
    private bool _isBreak;

    public EntryRowViewModel(
        Entry entry,
        IReadOnlyList<string> projects,
        Func<LedgerState> state,
        Func<Entry, Entry?> revise,
        Func<Entry, string, string?, ComposeResult> point,
        Action<EntryId> retract,
        bool editable)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(revise);
        ArgumentNullException.ThrowIfNull(point);
        ArgumentNullException.ThrowIfNull(retract);

        Id = entry.Id;
        Snapshot = entry;
        Projects = projects;
        _state = state;
        _revise = revise;
        _point = point;
        IsEditable = editable;
        RetractCommand = new RelayCommand(() => retract(Id));

        Load(entry);
    }

    public EntryId Id { get; }

    /// <summary>False on a frozen day: the row is shown, and nothing in it can be changed.</summary>
    public bool IsEditable { get; }

    /// <summary>The last accepted version; a correction is always described against this.</summary>
    public Entry Snapshot { get; private set; }

    /// <summary>The names the project box offers; what is typed may be a name none of them has.</summary>
    public IReadOnlyList<string> Projects { get; }

    public ObservableCollection<string> Topics { get; } = [];

    public IRelayCommand RetractCommand { get; }

    /// <summary>Raised when an edit could not be accepted; the day then rebuilds the row.</summary>
    public event EventHandler? Rejected;

    /// <summary>Both clock fields commit on focus loss, so nothing is half-typed while leaving one.</summary>
    partial void OnStartTextChanged(string value) => RequestRevise();

    partial void OnEndTextChanged(string value) => RequestRevise();

    partial void OnProjectTextChanged(string value) => RequestPoint();

    partial void OnTopicTextChanged(string value) => RequestPoint();

    partial void OnNoteChanged(string value) => RequestRevise();

    private void Load(Entry entry)
    {
        _loading = true;

        StartText = ClockText.Format(entry.Start);
        EndText = ClockText.Format(entry.End);
        ProjectText = _state().ProjectName(entry.ProjectId);
        RefreshTopics(entry.TopicId);
        Note = entry.Note ?? string.Empty;
        Tint = Tints.Of(ProjectText) is { } tint ? new SolidColorBrush(Color.Parse(tint)) : null;
        IsBreak = Break.IsBreak(ProjectText);

        _loading = false;
    }

    /// <summary>
    /// The offers for the topic box: the topics of the project the row names, without the ones
    /// that were taken out of use. The box still takes a name nobody used yet — that is how a
    /// topic is made.
    /// </summary>
    private void RefreshTopics(Guid? selectedTopicId)
    {
        var names = TopicsOf(ProjectText);

        TopicPicker = !Break.IsBreak(ProjectText);
        Topics.Clear();

        foreach (var name in names)
        {
            Topics.Add(name);
        }

        TopicText = SelectedTopicName(selectedTopicId, names) ?? string.Empty;
    }

    private IReadOnlyList<string> TopicsOf(string projectName) =>
        _state().MatchProject(projectName).Value is { Archived: false } project
            ? _state().Topics.Values
                .Where(topic => topic.ProjectId == project.Id && !topic.Archived)
                .Select(topic => topic.Name)
                .OrderBy(name => name, Dates.Names)
                .ToList()
            : [];

    private string? SelectedTopicName(Guid? topicId, IReadOnlyList<string> available)
    {
        var name = topicId is { } id ? _state().TopicName(id) : null;

        return name is not null && available.Contains(name) ? name : null;
    }

    private void RequestRevise()
    {
        if (_loading)
        {
            return;
        }

        if (ReadEditors() is { } after && Accept(after))
        {
            return;
        }

        Refuse();
    }

    /// <summary>
    /// A project or topic name was typed and left. The name is resolved the way a line resolves it,
    /// which may open it; a name the ledger will not take puts the row back as it was.
    /// </summary>
    private void RequestPoint()
    {
        if (_loading)
        {
            return;
        }

        var pointed = _point(Snapshot, ProjectText, TopicText);

        if (!pointed.Created)
        {
            Refuse();
            return;
        }

        Snapshot = pointed.Entry!;
        Load(Snapshot);
    }

    /// <summary>The entry the editors describe, or nothing while they are incomplete or impossible.</summary>
    private Entry? ReadEditors() => TryReadEditors(out var entry) ? entry : null;

    /// <summary>
    /// Whether the day took the correction. Nothing to change counts as taken: the editors already
    /// describe the entry.
    /// </summary>
    private bool Accept(Entry after)
    {
        if (after == Snapshot)
        {
            return true;
        }

        if (_revise(after) is not { } accepted)
        {
            return false;
        }

        Snapshot = accepted;
        return true;
    }

    private void Refuse()
    {
        PutTheEditorsBack(Snapshot);
        Rejected?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Reads the editors into an entry; false while they are incomplete or impossible.</summary>
    private bool TryReadEditors(out Entry entry)
    {
        entry = Snapshot;

        if (!TryReadShape(out var start, out var end) || ProjectIdOf(ProjectText) is not { } projectId)
        {
            return false;
        }

        if (!TryReadTopic(projectId, out var topicId))
        {
            return false;
        }

        entry = Snapshot with
        {
            Start = start,
            Duration = end.ToTimeSpan() - start.ToTimeSpan(),
            ProjectId = projectId,
            TopicId = topicId,
            Note = TypedNote(),
        };

        return true;
    }

    /// <summary>Both clocks read, and the end is not before the start.</summary>
    private bool TryReadShape(out TimeOnly start, out TimeOnly end)
    {
        end = default;

        return ClockText.TryParse(StartText, out start)
            && ClockText.TryParse(EndText, out end)
            && end >= start;
    }

    private string? TypedNote() => string.IsNullOrWhiteSpace(Note) ? null : Note.Trim();

    /// <summary>The project the typed name points at, or nothing while it is not a name the ledger knows.</summary>
    private Guid? ProjectIdOf(string name) => _state().MatchProject(name).Value?.Id;

    /// <summary>
    /// Whether the typed topic name says something the ledger knows. No name at all means no topic;
    /// a name that points at nothing is not silently dropped — the row is put back instead.
    /// </summary>
    private bool TryReadTopic(Guid projectId, out Guid? topicId)
    {
        var typed = !string.IsNullOrWhiteSpace(TopicText);

        topicId = typed ? _state().MatchTopic(projectId, TopicText).Value?.Id : null;

        return !typed || topicId is not null;
    }

    /// <summary>
    /// Puts the fields back to what the day holds. The values are corrected immediately, so this
    /// object is never left inconsistent; telling the controls is deferred, because it happens
    /// inside the binding's own write and would otherwise be overwritten by it. With no
    /// synchronisation context — a test, or any non-UI caller — there is no control to tell.
    /// </summary>
    private void PutTheEditorsBack(Entry entry)
    {
        Load(entry);

        void Tell() 
        {
            OnPropertyChanged(nameof(StartText));
            OnPropertyChanged(nameof(EndText));
            OnPropertyChanged(nameof(ProjectText));
            OnPropertyChanged(nameof(TopicText));
            OnPropertyChanged(nameof(Note));
        }

        if (SynchronizationContext.Current is { } context)
        {
            context.Post(_ => Tell(), null);
            return;
        }

        Tell();
    }
}
