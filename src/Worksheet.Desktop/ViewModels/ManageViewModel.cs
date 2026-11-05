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
/// The catalogue as a person manages it. Each row carries its own name, so renaming is typing into
/// the row and pressing Enter; a name another row already answers to is a merge, and the panel says
/// so — with what would move — before the key is pressed. Every decision is one journal change, so
/// one Undo takes any of them back, a merge included.
/// </summary>
public sealed partial class ManageViewModel : ObservableObject, ICataloguePanel, IDisposable
{
    private readonly Ledger _ledger;
    private readonly Catalogue _catalogue;
    private readonly IDisposable _untilTheLedgerChanges;

    [ObservableProperty]
    private CatalogueRow? _selectedProject;

    [ObservableProperty]
    private string? _proposal;

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

    public bool HasProposal => !string.IsNullOrEmpty(Proposal);

    public bool HasMessage => !string.IsNullOrEmpty(Message);

    public bool CanUndo => _ledger.CanUndo;

    partial void OnMessageChanged(string? value) => OnPropertyChanged(nameof(HasMessage));

    partial void OnProposalChanged(string? value) => OnPropertyChanged(nameof(HasProposal));

    partial void OnSelectedProjectChanged(CatalogueRow? value) => RefreshTopics();

    public void Rename(CatalogueRow row, string name)
    {
        ArgumentNullException.ThrowIfNull(row);

        var twin = Twin(row, name);

        Report(
            row.IsProject
                ? _catalogue.RenameProject(row.Name, name)
                : _catalogue.RenameTopic(row.Project ?? string.Empty, row.Name, name),
            twin is null ? Renamed(row, name) : Merged(row, twin));
    }

    public void Archive(CatalogueRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        Report(
            row.IsProject
                ? _catalogue.RemoveProject(row.Name)
                : _catalogue.RemoveTopic(row.Project ?? string.Empty, row.Name),
            row.IsProject
                ? $"'{row.Name}' is out of the catalogue; its entries stay as they are."
                : $"'{row.Name}' is out of {row.Project}; its entries stay as they are.");
    }

    /// <summary>The sentence the panel shows while a name is being typed, so Enter holds no surprise.</summary>
    public void Drafting(CatalogueRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        Proposal = !row.IsChanged
            ? null
            : string.IsNullOrWhiteSpace(row.Draft)
                ? $"A {Word(row)} needs a name."
                : ForTyping(row, Twin(row, row.Draft));
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

    /// <summary>What Enter would do to this row, in one sentence.</summary>
    private static string ForTyping(CatalogueRow row, CatalogueRow? twin) =>
        twin is null
            ? $"Enter renames '{row.Name}' to '{row.Draft.Trim()}'."
            : $"Enter merges '{row.Name}' into '{twin.Name}' — {Moving(row)}.";

    private static string Renamed(CatalogueRow row, string name) =>
        $"The {Word(row)} is called '{Names.Trimmed(name)}' now.";

    private static string Merged(CatalogueRow row, CatalogueRow twin) =>
        $"'{row.Name}' merged into '{twin.Name}'; {Moved(row)}.";

    private static string Moving(CatalogueRow row) =>
        row.Entries == 0 ? "it holds no entries" : $"its {row.Holds} move there";

    private static string Moved(CatalogueRow row) =>
        row.Entries == 0 ? "it held no entries" : $"its {row.Holds} moved there";

    private static string Word(CatalogueRow row) => row.IsProject ? "project" : "topic";

    /// <summary>The row a typed name would land on, which makes the rename a merge.</summary>
    private CatalogueRow? Twin(CatalogueRow row, string name) =>
        (row.IsProject ? Projects : Topics).FirstOrDefault(other =>
            other != row
            && string.Equals(other.Name.Trim(), Names.Trimmed(name), StringComparison.OrdinalIgnoreCase));

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
        // so the choice above would not move and the list would keep the row that just went.
        RefreshTopics();

        Proposal = null;

        OnPropertyChanged(nameof(CanUndo));
    }

    private void RefreshTopics()
    {
        Topics.Clear();

        foreach (var row in TopicRows())
        {
            Topics.Add(row);
        }
    }

    private IEnumerable<CatalogueRow> ProjectRows() =>
        _ledger.State.Projects.Values
            .Where(project => !project.Archived)
            .OrderBy(project => project.Name, Dates.Names)
            .Select(project => Row(CatalogueKind.Project, project.Name, entry => entry.ProjectId == project.Id));

    private IEnumerable<CatalogueRow> TopicRows() =>
        SelectedProject is not { } chosen
            ? []
            : _ledger.State.Topics.Values
                .Where(topic => !topic.Archived && _ledger.State.ProjectName(topic.ProjectId) == chosen.Name)
                .OrderBy(topic => topic.Name, Dates.Names)
                .Select(topic => Row(CatalogueKind.Topic, topic.Name, entry => entry.TopicId == topic.Id, chosen.Name));

    private CatalogueRow Row(CatalogueKind kind, string name, Func<Entry, bool> holds, string? project = null)
    {
        var entries = _ledger.State.Entries.Values.Where(holds).ToArray();

        return new CatalogueRow(this, kind, name, entries.Length, Minutes(entries), project);
    }

    private static int Minutes(IEnumerable<Entry> entries) =>
        (int)entries.Aggregate(TimeSpan.Zero, (total, entry) => total + entry.Duration).TotalMinutes;
}
