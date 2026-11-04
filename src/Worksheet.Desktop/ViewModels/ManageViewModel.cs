// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Worksheet.Core;

namespace Worksheet.Desktop.ViewModels;

/// <summary>One project or topic as the panel shows it: its name, and how many entries it holds.</summary>
public sealed record CatalogueRow(string Name, int Entries)
{
    public string Label => Entries switch
    {
        0 => Name,
        1 => $"{Name} (1 entry)",
        _ => $"{Name} ({Entries} entries)",
    };
}

/// <summary>
/// The catalogue as a person manages it: what is in use, what each thing holds, and the four
/// decisions — rename a project, drop it with somewhere for its hours to go, rename a topic, drop a
/// topic. Every decision is one journal change, so undo takes any of them back in one step.
/// </summary>
public sealed partial class ManageViewModel : ObservableObject, IDisposable
{
    private readonly Ledger _ledger;
    private readonly Catalogue _catalogue;
    private readonly IDisposable _untilTheLedgerChanges;

    [ObservableProperty]
    private CatalogueRow? _selectedProject;

    [ObservableProperty]
    private CatalogueRow? _selectedTopic;

    [ObservableProperty]
    private string _newProjectName = string.Empty;

    [ObservableProperty]
    private string _newTopicName = string.Empty;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _messageIsProblem;

    public ManageViewModel(Ledger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);

        _ledger = ledger;
        _catalogue = new Catalogue(ledger);

        Refresh();

        _untilTheLedgerChanges = ledger.Subscribe(new Watch(_ => Refresh()));
    }

    public void Dispose() => _untilTheLedgerChanges.Dispose();

    /// <summary>Every project in use, with what it holds.</summary>
    public ObservableCollection<CatalogueRow> Projects { get; } = [];

    /// <summary>The topics of the chosen project.</summary>
    public ObservableCollection<CatalogueRow> Topics { get; } = [];

    public bool HasMessage => !string.IsNullOrEmpty(Message);

    public bool CanUndo => _ledger.CanUndo;

    partial void OnMessageChanged(string? value) => OnPropertyChanged(nameof(HasMessage));

    partial void OnSelectedProjectChanged(CatalogueRow? value) => RefreshTopics();

    [RelayCommand]
    private void RenameProject()
    {
        if (SelectedProject is not { } project)
        {
            Refuse("Choose a project first.");
            return;
        }

        Report(_catalogue.RenameProject(project.Name, NewProjectName), $"The project is called '{NewProjectName.Trim()}' now.");
    }

    [RelayCommand]
    private void RemoveProject()
    {
        if (SelectedProject is not { } project)
        {
            Refuse("Choose a project first.");
            return;
        }

        Report(
            _catalogue.RemoveProject(project.Name),
            $"'{project.Name}' is out of the catalogue; its entries stay as they are.");
    }

    [RelayCommand]
    private void RenameTopic()
    {
        if (SelectedProject is not { } project || SelectedTopic is not { } topic)
        {
            Refuse("Choose a topic first.");
            return;
        }

        Report(
            _catalogue.RenameTopic(project.Name, topic.Name, NewTopicName),
            $"The topic is called '{NewTopicName.Trim()}' now.");
    }

    [RelayCommand]
    private void RemoveTopic()
    {
        if (SelectedProject is not { } project || SelectedTopic is not { } topic)
        {
            Refuse("Choose a topic first.");
            return;
        }

        Report(
            _catalogue.RemoveTopic(project.Name, topic.Name),
            $"'{topic.Name}' is out of {project.Name}; its entries stay as they are.");
    }

    [RelayCommand]
    private void Undo()
    {
        if (!_ledger.TryUndo(out var reason))
        {
            Refuse(reason);
            return;
        }

        Say("Undone.");
    }

    private void Report(CatalogueResult result, string done)
    {
        if (!result.Accepted)
        {
            Refuse(string.Join(" ", result.Problems));
            return;
        }

        Say(done);
    }

    private void Say(string message)
    {
        MessageIsProblem = false;
        Message = message;

        Refresh();
    }

    private void Refuse(string? reason)
    {
        MessageIsProblem = true;
        Message = reason;
    }

    /// <summary>Reads the catalogue again, keeping the choice on the same name where it still exists.</summary>
    private void Refresh()
    {
        var project = SelectedProject?.Name;

        Projects.Clear();

        foreach (var row in ProjectRows())
        {
            Projects.Add(row);
        }

        SelectedProject = Projects.FirstOrDefault(row => row.Name == project) ?? Projects.FirstOrDefault();

        // The topics are re-read here as well: dropping a topic leaves its project's count unchanged,
        // so the selection above would not move and the list would keep the topic that just went.
        RefreshTopics();

        OnPropertyChanged(nameof(CanUndo));
    }

    private void RefreshTopics()
    {
        var topic = SelectedTopic?.Name;

        Topics.Clear();

        foreach (var row in TopicRows())
        {
            Topics.Add(row);
        }

        SelectedTopic = Topics.FirstOrDefault(row => row.Name == topic);
    }

    private IEnumerable<CatalogueRow> ProjectRows() =>
        _ledger.State.Projects.Values
            .Where(project => !project.Archived)
            .OrderBy(project => project.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(project => new CatalogueRow(project.Name, CountOf(entry => entry.ProjectId == project.Id)));

    private IEnumerable<CatalogueRow> TopicRows() =>
        SelectedProject is not { } project
            ? []
            : _ledger.State.Topics.Values
                .Where(topic => !topic.Archived && _ledger.State.ProjectName(topic.ProjectId) == project.Name)
                .OrderBy(topic => topic.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(topic => new CatalogueRow(topic.Name, CountOf(entry => entry.TopicId == topic.Id)));

    private int CountOf(Func<Entry, bool> holds) => _ledger.State.Entries.Values.Count(holds);
}
