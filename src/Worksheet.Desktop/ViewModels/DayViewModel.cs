// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Worksheet.Core;
using Worksheet.Export;
using Worksheet.Storage;

namespace Worksheet.Desktop.ViewModels;

/// <summary>
/// The day on screen: which day, its rows, its total, and the two ways to change it — one line of
/// text, or a correction of a row. It never holds entries of its own; what is shown is always the
/// fold of the journal.
/// </summary>
public sealed partial class DayViewModel : ObservableObject, IDisposable
{
    private readonly LocalLedger _local;
    private readonly EntryComposer _composer;
    private readonly DayCalendar _calendar;
    private readonly DateOnly _today;
    private readonly Func<DateTimeOffset> _now;
    private readonly IDisposable _untilTheLedgerChanges;

    [ObservableProperty]
    private DateTime? _selectedDate;

    [ObservableProperty]
    private string _dayTitle = string.Empty;

    [ObservableProperty]
    private string _total = string.Empty;

    [ObservableProperty]
    private string _entryText = string.Empty;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _messageIsProblem;

    [ObservableProperty]
    private bool _canUndo;

    [ObservableProperty]
    private bool _canRedo;

    /// <summary>What to write next in the entry line, in words.</summary>
    [ObservableProperty]
    private string _nextStep = string.Empty;

    /// <summary>Why the line as typed would be refused, checked before Enter is pressed.</summary>
    [ObservableProperty]
    private string? _validation;

    /// <summary>True while the day takes no entries: its rows are then shown but not editable.</summary>
    [ObservableProperty]
    private bool _isFrozen;

    /// <summary>What the freezing means, said next to the status the day has instead.</summary>
    [ObservableProperty]
    private string _frozenNote = string.Empty;

    /// <summary>Whether the chips below the line name projects or the topics of one.</summary>
    [ObservableProperty]
    private string _suggestionCaption = string.Empty;

    private EntryProposals _proposals = EntryProposals.None;
    private int _chosen;

    public DayViewModel(LocalLedger local, DateOnly? today = null, Func<DateTimeOffset>? now = null)
    {
        ArgumentNullException.ThrowIfNull(local);

        _local = local;
        _composer = new EntryComposer(local.Ledger);
        _calendar = new DayCalendar(local.Ledger);
        _today = today ?? DateOnly.FromDateTime(DateTime.Today);
        _now = now ?? (() => DateTimeOffset.Now);
        _selectedDate = _today.ToDateTime(TimeOnly.MinValue);

        Refresh();

        _untilTheLedgerChanges = local.Ledger.Subscribe(new Watch(_ => Reload()));
    }

    public void Dispose() => _untilTheLedgerChanges.Dispose();

    /// <summary>Entries and the holes between them, in start order.</summary>
    public ObservableCollection<IDayRow> Rows { get; } = [];

    /// <summary>The five kinds of day, one of them chosen, pre-filled for a weekend.</summary>
    public ObservableCollection<DayKindOptionViewModel> Kinds { get; } = [];

    /// <summary>The screens the heading can open; the shell sets them, since it owns the windows.</summary>
    public WindowScreens? Screens { get; set; }

    /// <summary>The names the row editors offer; a name none of them has may still be typed.</summary>
    public ObservableCollection<string> Projects { get; } = [];

    public ObservableCollection<SuggestionViewModel> Suggestions { get; } = [];

    public bool HasMessage => !string.IsNullOrEmpty(Message);

    public bool IsEmpty => Rows.Count == 0;

    public bool HasSuggestions => Suggestions.Count > 0;

    public bool HasValidation => !string.IsNullOrEmpty(Validation);

    private DateOnly Day => DateOnly.FromDateTime(SelectedDate ?? _today.ToDateTime(TimeOnly.MinValue));

    /// <summary>What the window showed after the last refresh, to know whether a reload would differ.</summary>
    private string _shown = string.Empty;

    /// <summary>The day on screen, as a plain date.</summary>
    public DateOnly Date => Day;

    partial void OnSelectedDateChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(Date));
        Refresh();
    }

    partial void OnEntryTextChanged(string value)
    {
        UpdateSuggestions();
        UpdateGuide();
    }

    partial void OnValidationChanged(string? value) => OnPropertyChanged(nameof(HasValidation));

    partial void OnMessageChanged(string? value) => OnPropertyChanged(nameof(HasMessage));

    [RelayCommand]
    private void Log()
    {
        var parse = EntryCommandParser.Parse(EntryText, Day);

        if (!parse.Succeeded)
        {
            Report(parse.Error, isProblem: true);
            return;
        }

        var composed = _composer.Compose(parse.Command!);

        if (!composed.Created)
        {
            Report(string.Join(" ", composed.Problems), isProblem: true);
            return;
        }

        EntryText = string.Empty;
        Report(null, isProblem: false);
        Refresh();
    }

    [RelayCommand]
    private void Undo()
    {
        if (!_local.Ledger.TryUndo(out var reason))
        {
            Report(reason, isProblem: true);
            return;
        }

        Report("Undone.", isProblem: false);
        Refresh();
    }

    [RelayCommand]
    private void Redo()
    {
        if (!_local.Ledger.TryRedo(out var reason))
        {
            Report(reason, isProblem: true);
            return;
        }

        Report("Redone.", isProblem: false);
        Refresh();
    }

    /// <summary>
    /// Where an export is written. The window supplies the save dialog and answers the path it chose,
    /// or nothing when the person changed their mind; a test supplies a folder.
    /// </summary>
    public Func<ExportFile, Task<string?>>? SaveExport { get; set; }

    /// <summary>
    /// The range is the period the day on screen belongs to: that day, its month, its quarter, its
    /// year. Building it here keeps the report and the page in one place, and the zip in one file.
    /// </summary>
    public ExportFile BuildExport(ExportPeriod period)
    {
        var report = RangeReport.Of(_local.Ledger.State, ExportRange.For(period, Day));
        var generatedAt = _now();

        return new ExportFile(ExportBundle.ZipName(report, generatedAt), ExportBundle.Zip(report, generatedAt));
    }

    [RelayCommand]
    private Task ExportDayAsync() => Export(ExportPeriod.Day);

    [RelayCommand]
    private Task ExportMonthAsync() => Export(ExportPeriod.Month);

    [RelayCommand]
    private Task ExportQuarterAsync() => Export(ExportPeriod.Quarter);

    [RelayCommand]
    private Task ExportYearAsync() => Export(ExportPeriod.Year);

    /// <summary>The command for one range, so a menu is wired by the range it names rather than by position.</summary>
    public IAsyncRelayCommand CommandFor(ExportPeriod period) =>
        period switch
        {
            ExportPeriod.Day => ExportDayCommand,
            ExportPeriod.Month => ExportMonthCommand,
            ExportPeriod.Quarter => ExportQuarterCommand,
            _ => ExportYearCommand,
        };

    private async Task Export(ExportPeriod period)
    {
        if (SaveExport is not { } save)
        {
            Report("There is nowhere to save an export from this window.", isProblem: true);
            return;
        }

        var path = await save(BuildExport(period));

        if (path is null)
        {
            Report("Export cancelled.", isProblem: false);
            return;
        }

        Report($"Exported {Path.GetFileName(path)}.", isProblem: false);
    }

    [RelayCommand]
    private void PreviousDay() => SelectedDate = Day.AddDays(-1).ToDateTime(TimeOnly.MinValue);
    [RelayCommand]
    private void NextDay() => SelectedDate = Day.AddDays(1).ToDateTime(TimeOnly.MinValue);

    [RelayCommand]
    private void Today() => SelectedDate = _today.ToDateTime(TimeOnly.MinValue);

    private void Refresh()
    {
        RefreshKinds();
        RefreshProjects();
        RefreshRows();

        DayTitle = DescribeDay(Day);

        UpdateTotals();
        UpdateSuggestions();
        UpdateGuide();

        OnPropertyChanged(nameof(IsEmpty));

        _shown = Signature();
    }

    /// <summary>
    /// Everything this window shows that comes out of the fold: the day, its kind, the names the
    /// editors offer, and each entry as an editor would read it. Equal signatures mean a reload
    /// would rebuild the same rows.
    /// </summary>
    private string Signature()
    {
        var state = _local.Ledger.State;
        var layout = DayLayout.Of(state.EntriesOn(Day));

        return string.Join(
            '\n',
            new[]
            {
                Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                state.KindOf(Day).ToString(),
                string.Join('|', ProjectNames(state)),
            }.Concat(layout.Entries.Select(entry =>
                $"{entry.Id.Value} {entry.Start:HH:mm} {entry.Duration} {entry.ProjectId} {entry.TopicId} {entry.Note}")));
    }

    /// <summary>
    /// The five kinds of day with the day's own chosen. A Saturday or a Sunday starts as a
    /// weekend, and any of the five can be picked over it.
    /// </summary>
    private void RefreshKinds()
    {
        var kind = _local.Ledger.State.KindOf(Day);

        Kinds.Clear();

        foreach (var option in Enum.GetValues<DayKind>())
        {
            Kinds.Add(new DayKindOptionViewModel(option, option == kind, Declare));
        }

        IsFrozen = !DayKinds.TakesEntries(kind);
        FrozenNote = IsFrozen
            ? $"{DayKinds.LabelOf(kind)}: the timetable is frozen; only the status can change."
            : string.Empty;
    }

    /// <summary>Declares what the day is. Anything but a working day freezes the timetable.</summary>
    private void Declare(DayKind kind)
    {
        _calendar.Declare(Day, kind);
        Refresh();

        Report($"{DescribeDay(Day)} is {DayKinds.LabelOf(kind)}.", isProblem: false);
    }

    private void RefreshProjects()
    {
        Projects.Clear();

        foreach (var name in ProjectNames(_local.Ledger.State))
        {
            Projects.Add(name);
        }
    }

    private void RefreshRows()
    {
        Rows.Clear();

        var layout = DayLayout.Of(_local.Ledger.State.EntriesOn(Day));
        var gaps = layout.Gaps.ToLookup(gap => gap.Start);
        var editable = !IsFrozen;

        foreach (var entry in layout.Entries)
        {
            var row = new EntryRowViewModel(
                entry,
                [.. Projects],
                () => _local.Ledger.State,
                Revise,
                _composer.Point,
                Retract,
                editable);
            row.Rejected += (_, _) => Dispatcher.UIThread.Post(RefreshRows);

            Rows.Add(row);

            foreach (var gap in gaps[entry.Start.ToTimeSpan() + entry.Duration])
            {
                Rows.Add(new GapRowViewModel(gap, FillGap, RecordBreak, editable));
            }
        }
    }

    /// <summary>
    /// Puts a hole's times into the entry line, with the project — and the topic, when it had one —
    /// of the block that ended just before it, so the line is already valid and only needs confirming.
    /// </summary>
    private void FillGap(DaySlot gap)
    {
        var preceding = _local.Ledger.State.EntriesOn(Day)
            .LastOrDefault(entry => entry.Start.ToTimeSpan() + entry.Duration == gap.Start);

        EntryText = string.Join(
            ' ',
            new[] { LabelOf(preceding), $"{gap.StartsAt:HH:mm}-{gap.EndsAt:HH:mm}" }.Where(part => part.Length > 0));

        Report($"{gap.StartsAt:HH:mm}-{gap.EndsAt:HH:mm} filled in; add a project if there is none.", isProblem: false);
    }

    /// <summary>A block as the entry line writes it: the project, and its topic when it had one.</summary>
    private string LabelOf(Entry? entry)
    {
        if (entry is null)
        {
            return string.Empty;
        }

        var project = _local.Ledger.State.ProjectName(entry.ProjectId);
        var topic = _local.Ledger.State.TopicName(entry.TopicId);

        return topic is null ? project : $"{project} ({topic})";
    }

    /// <summary>
    /// Records a hole as a break: an ordinary entry in the project breaks go to, with the note
    /// already there so the comment is a word away from being written.
    /// </summary>
    private void RecordBreak(DaySlot gap)
    {
        var composed = _composer.Compose(
            new EntryCommand(Day, Break.Project, Topic: null, gap.StartsAt, gap.Duration, Break.Note));

        if (!composed.Created)
        {
            Report(string.Join(" ", composed.Problems), isProblem: true);
            return;
        }

        Report($"{gap.StartsAt:HH:mm}-{gap.EndsAt:HH:mm} recorded as a break; the note is yours to change.", isProblem: false);
        Refresh();
    }

    private static IEnumerable<string> ProjectNames(LedgerState state) =>
        state.Projects.Values
            .Where(project => !project.Archived)
            .OrderBy(project => project.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(project => project.Name)
            .ToList();

    private string DescribeDay(DateOnly day) =>
        day == _today
            ? $"Today, {day.ToString("d MMMM", CultureInfo.CurrentCulture)}"
            : day.ToString("dddd d MMMM", CultureInfo.CurrentCulture);

    /// <summary>
    /// A correction of one row. On success the rows are left alone, so the control that was being
    /// edited keeps focus; only the numbers beside it move.
    /// </summary>
    private Entry? Revise(Entry after)
    {
        if (!_local.Ledger.State.Entries.TryGetValue(after.Id, out var before))
        {
            return null;
        }

        var result = _composer.Revise(before, after);

        if (!result.Created)
        {
            Report(string.Join(" ", result.Problems), isProblem: true);
            return null;
        }

        Report(null, isProblem: false);
        UpdateTotals();

        return result.Entry;
    }

    private void Retract(EntryId id)
    {
        var result = _composer.Retract(id);

        if (!result.Created)
        {
            Report(string.Join(" ", result.Problems), isProblem: true);
            return;
        }

        Report("Retracted; undo brings it back.", isProblem: false);
        Refresh();
    }

    private void UpdateTotals()
    {
        Total = Durations.Format(_local.Ledger.State.TotalOn(Day));
        CanUndo = _local.Ledger.CanUndo;
        CanRedo = _local.Ledger.CanRedo;
    }

    /// <summary>
    /// Offers known names that continue what has been typed — the projects at first, then the
    /// topics of the project once its bracket is open.
    /// </summary>
    private void UpdateSuggestions()
    {
        _proposals = EntryCompletion.Proposals(_local.Ledger.State, EntryText);
        _chosen = 0;

        ShowSuggestions();
    }

    /// <summary>Moves to the next offer, wrapping round; the right arrow and the down arrow reach it.</summary>
    [RelayCommand]
    private void NextSuggestion() => ChooseSuggestion(_chosen + 1);

    /// <summary>Moves to the previous offer, wrapping round; the left arrow and the up arrow reach it.</summary>
    [RelayCommand]
    private void PreviousSuggestion() => ChooseSuggestion(_chosen - 1);

    /// <summary>Writes the chosen offer into the line; Tab reaches it.</summary>
    [RelayCommand]
    private void CompleteSuggestion()
    {
        if (_proposals.Any)
        {
            Take(_proposals.Names[_chosen]);
        }
    }

    private void ChooseSuggestion(int index)
    {
        if (!_proposals.Any)
        {
            return;
        }

        var count = _proposals.Names.Count;

        _chosen = ((index % count) + count) % count;

        ShowSuggestions();
    }

    private void Take(string name)
    {
        EntryText = EntryCompletion.Take(_local.Ledger.State, EntryText, name);
    }

    /// <summary>
    /// The chips are rebuilt rather than updated: six of them, and the templates then need no
    /// notification to say which one the arrows are on.
    /// </summary>
    private void ShowSuggestions()
    {
        Suggestions.Clear();

        for (var index = 0; index < _proposals.Names.Count; index++)
        {
            var name = _proposals.Names[index];

            Suggestions.Add(
                new SuggestionViewModel(name, index == _chosen, new RelayCommand(() => Take(name))));
        }

        SuggestionCaption = _proposals.Field == EntryStep.Topic ? "TOPICS" : "PROJECTS";

        OnPropertyChanged(nameof(HasSuggestions));
    }

    /// <summary>
    /// Says what to write next, and complains as soon as the line is complete enough to be wrong —
    /// an intersection is visible before Enter, not after it.
    /// </summary>
    private void UpdateGuide()
    {
        var step = EntryGuide.Next(EntryText);

        NextStep = step == EntryStep.Complete
            ? "complete"
            : $"next: {EntryGuide.Label(step)}";

        var parse = EntryCommandParser.Parse(EntryText, Day);
        Validation = parse.Succeeded && _composer.Preview(parse.Command!) is { Count: > 0 } conflicts
            ? string.Join("; ", conflicts)
            : null;
    }

    private void Report(string? message, bool isProblem)
    {
        MessageIsProblem = isProblem;
        Message = message;
    }

    /// <summary>
    /// Reads the fold again after something outside this view changed it — an agent through the
    /// MCP server, or the tray. What is on screen is always the fold, so it is re-read, not patched.
    /// </summary>
    /// <summary>
    /// Reads the fold again after a change landed. A change that cannot be seen here — an agent
    /// logging on another day, a project renamed — leaves the rows alone, because rebuilding them
    /// would take the cell a person is typing in with it.
    /// </summary>
    private void Reload()
    {
        if (Signature() == _shown)
        {
            return;
        }

        Refresh();
    }

    /// <summary>Says something to the person at the window: a start-up problem, or news from elsewhere.</summary>
    public void Say(string message, bool isProblem) => Report(message, isProblem);
}
