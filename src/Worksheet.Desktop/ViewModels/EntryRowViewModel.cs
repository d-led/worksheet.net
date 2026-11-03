// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Worksheet.Core;

namespace Worksheet.Desktop.ViewModels;

/// <summary>
/// One row of the day. It shows absolute times — a start and an end — because that is what a day
/// looks like; the length is derived and only the entry language accepts it. Every edit is a
/// correction of the whole entry, and a refused correction puts the editors back.
/// </summary>
public sealed partial class EntryRowViewModel : ObservableObject
{
    private readonly Func<LedgerState> _state;
    private readonly Func<Entry, Entry?> _revise;
    private bool _loading;

    [ObservableProperty]
    private string _startText = string.Empty;

    [ObservableProperty]
    private string _endText = string.Empty;

    [ObservableProperty]
    private ProjectOption? _project;

    [ObservableProperty]
    private string? _topic;

    [ObservableProperty]
    private string _note = string.Empty;

    public EntryRowViewModel(
        Entry entry,
        ObservableCollection<ProjectOption> projects,
        Func<LedgerState> state,
        Func<Entry, Entry?> revise,
        Action<EntryId> retract,
        bool editable)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(revise);
        ArgumentNullException.ThrowIfNull(retract);

        Id = entry.Id;
        Snapshot = entry;
        Projects = projects;
        _state = state;
        _revise = revise;
        IsEditable = editable;
        RetractCommand = new RelayCommand(() => retract(Id));

        Load(entry);
    }

    public EntryId Id { get; }

    /// <summary>False on a frozen day: the row is shown, and nothing in it can be changed.</summary>
    public bool IsEditable { get; }

    /// <summary>The last accepted version; a correction is always described against this.</summary>
    public Entry Snapshot { get; private set; }

    public ObservableCollection<ProjectOption> Projects { get; }

    public ObservableCollection<string> Topics { get; } = [];

    public IRelayCommand RetractCommand { get; }

    /// <summary>Raised when an edit could not be accepted; the day then rebuilds the row.</summary>
    public event EventHandler? Rejected;

    /// <summary>Both clock fields commit on focus loss, so nothing is half-typed while leaving one.</summary>
    partial void OnStartTextChanged(string value) => RequestRevise();

    partial void OnEndTextChanged(string value) => RequestRevise();

    partial void OnProjectChanged(ProjectOption? value)
    {
        if (_loading)
        {
            return;
        }

        RefreshTopics(selectedTopicId: null);
        RequestRevise();
    }

    partial void OnTopicChanged(string? value) => RequestRevise();

    partial void OnNoteChanged(string value) => RequestRevise();

    private void Load(Entry entry)
    {
        _loading = true;

        StartText = ClockText.Format(entry.Start);
        EndText = ClockText.Format(entry.End);
        Project = Projects.FirstOrDefault(option => option.Id == entry.ProjectId);
        RefreshTopics(entry.TopicId);
        Note = entry.Note ?? string.Empty;

        _loading = false;
    }

    private void RefreshTopics(Guid? selectedTopicId)
    {
        var names = TopicNamesFor(Project?.Id);

        Topics.Clear();

        foreach (var name in names)
        {
            Topics.Add(name);
        }

        Topic = SelectedTopicName(selectedTopicId ?? Snapshot.TopicId, names);
    }

    private IReadOnlyList<string> TopicNamesFor(Guid? projectId) =>
        projectId is null
            ? []
            : _state().Topics.Values
                .Where(topic => topic.ProjectId == projectId && !topic.Archived)
                .Select(topic => topic.Name)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

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

        if (!TryReadShape(out var start, out var end) || Project is not { } project)
        {
            return false;
        }

        entry = Snapshot with
        {
            Start = start,
            Duration = end.ToTimeSpan() - start.ToTimeSpan(),
            ProjectId = project.Id,
            TopicId = ResolveTopicId(project.Id, Topic),
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

    private Guid? ResolveTopicId(Guid projectId, string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : _state().MatchTopic(projectId, name).Value?.Id;

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
        }

        if (SynchronizationContext.Current is { } context)
        {
            context.Post(_ => Tell(), null);
            return;
        }

        Tell();
    }
}
