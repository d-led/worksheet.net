// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Worksheet.Core;

namespace Worksheet.Desktop.ViewModels;

/// <summary>Whether a row is a project or a topic of the chosen project.</summary>
public enum CatalogueKind
{
    Project,
    Topic,
}

/// <summary>What a row asks of the panel it sits in.</summary>
public interface ICataloguePanel
{
    /// <summary>
    /// Gives the row the typed name, or merges it into whatever already answers to it, and answers
    /// whether the name was taken.
    /// </summary>
    bool Rename(CatalogueRow row, string name);

    /// <summary>Takes the row out of the catalogue; what it holds keeps the name it had.</summary>
    void Archive(CatalogueRow row);

    /// <summary>Says what pressing Enter on this row would do, before anything is done.</summary>
    void Drafting(CatalogueRow row);
}

/// <summary>
/// One project or topic as the panel shows it: the name in a field of its own, what it holds, and the
/// decision to take it out. The name is edited where it stands — no second box, no dialog, no step to
/// confirm — so the panel has to say in a sentence what pressing Enter will do before it does it.
/// </summary>
public sealed partial class CatalogueRow : ObservableObject
{
    private readonly ICataloguePanel _panel;

    /// <summary>
    /// Set by the first decision taken on this row and cleared again when the panel would not take
    /// it. Enter settles the name, and leaving the field straight after would otherwise read the
    /// same keystroke as a second decision; a refused name, on the other hand, leaves the row able
    /// to take another.
    /// </summary>
    private bool _settled;

    [ObservableProperty]
    private string _draft;

    public CatalogueRow(
        ICataloguePanel panel,
        CatalogueKind kind,
        string name,
        int entries,
        int minutes,
        string? project = null)
    {
        _panel = panel;
        _draft = name;

        Kind = kind;
        Name = name;
        Entries = entries;
        Minutes = minutes;
        Project = project;
    }

    public CatalogueKind Kind { get; }

    /// <summary>The name as the journal has it, which Escape puts back.</summary>
    public string Name { get; }

    public int Entries { get; }

    public int Minutes { get; }

    /// <summary>The project a topic belongs to; nothing on a project.</summary>
    public string? Project { get; }

    public bool IsProject => Kind is CatalogueKind.Project;

    public bool IsTopic => Kind is CatalogueKind.Topic;

    /// <summary>What the row holds, in the words a person uses for it.</summary>
    public string Holds => Entries switch
    {
        0 => "no entries",
        1 => $"1 entry · {Durations.Format(TimeSpan.FromMinutes(Minutes))}",
        _ => $"{Entries} entries · {Durations.Format(TimeSpan.FromMinutes(Minutes))}",
    };

    /// <summary>Whether the field says something other than what the journal holds.</summary>
    public bool IsChanged => !string.Equals(Draft.Trim(), Name.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>Enter: the typed name becomes the row's, or joins the row that already has it.</summary>
    [RelayCommand]
    private void Commit()
    {
        if (_settled || !IsChanged)
        {
            return;
        }

        _settled = _panel.Rename(this, Draft);
    }

    /// <summary>Escape: the field goes back to the name in the journal, and nothing is written.</summary>
    [RelayCommand]
    private void Revert() => Draft = Name;

    [RelayCommand]
    private void Archive()
    {
        _settled = true;

        _panel.Archive(this);
    }

    partial void OnDraftChanged(string value) => _panel.Drafting(this);
}
