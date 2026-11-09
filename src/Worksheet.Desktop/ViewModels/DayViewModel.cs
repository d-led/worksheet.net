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
    private readonly Func<DateTimeOffset> _clock;
    private readonly IDisposable _untilTheLedgerChanges;

    /// <summary>
    /// The day the person picked, or nothing while the window follows the calendar. Today is not
    /// kept anywhere: a day nobody picked is read from the clock whenever it is asked for, so a
    /// window left open overnight is about today when it is looked at again, and a day somebody
    /// did pick stays where it was put.
    /// </summary>
    [ObservableProperty]
    private DateTime? _selectedDate;

    [ObservableProperty]
    private string _dayTitle = string.Empty;

    /// <summary>The time worked on the day, the way the heading writes it: a break is left out.</summary>
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

    public DayViewModel(LocalLedger local, Func<DateTimeOffset>? clock = null)
    {
        ArgumentNullException.ThrowIfNull(local);

        _local = local;
        _composer = new EntryComposer(local.Ledger);
        _calendar = new DayCalendar(local.Ledger);
        _clock = clock ?? (() => DateTimeOffset.Now);

        Refresh();

        _untilTheLedgerChanges = local.Ledger.Subscribe(new Watch(_ => Redraw()));
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

    /// <summary>What the clock says the day is now, asked every time it is needed.</summary>
    private DateOnly Today => DateOnly.FromDateTime(_clock().LocalDateTime);

    /// <summary>The day on screen: the one the person picked, or today while none was picked.</summary>
    private DateOnly Day => SelectedDate is { } picked ? DateOnly.FromDateTime(picked) : Today;

    /// <summary>What the window showed after the last refresh, to know whether drawing it again would differ.</summary>
    private string _shown = string.Empty;

    /// <summary>
    /// True while a row is writing a change of its own: the row shows that change itself, so the
    /// echo from the ledger must not rebuild the rows and take the focus out of the cell it came from.
    /// </summary>
    private bool _aRowIsWriting;

    /// <summary>The day on screen, as a plain date.</summary>
    public DateOnly Date => Day;

    /// <summary>
    /// Puts a day on screen. The day the clock says is not kept, so picking today picks nothing:
    /// that is what makes a window on today keep up with the calendar, and walking back onto today
    /// mean being on today again.
    /// </summary>
    public void Show(DateOnly day) =>
        SelectedDate = day == Today ? null : day.ToDateTime(TimeOnly.MinValue);

    /// <summary>
    /// The window is on screen again. It may have been running since yesterday, so what it would
    /// draw now is drawn: a page that was following the calendar is about the new day.
    /// </summary>
    public void ReviewToday() => Redraw();

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

    /// <summary>
    /// The line in the order it carries what it says: a name, a span, and the words after a "#".
    /// Only a line that cannot be an entry at all — one with no name and no time — is a note of its
    /// own, so a note never competes with an entry for the same keystrokes.
    /// </summary>
    [RelayCommand]
    private void Log()
    {
        var parse = EntryCommandParser.Parse(EntryText, Day, _local.Ledger.State.EndOfLast);

        if (parse.Succeeded)
        {
            LogEntry(parse.Command!);
            return;
        }

        if (NoteLine.Parse(EntryText) is { } note)
        {
            Note(note);
            return;
        }

        Report(parse.Error, isProblem: true);
    }

    private void LogEntry(EntryCommand command)
    {
        var composed = _composer.Compose(command);

        if (!composed.Created)
        {
            Report(string.Join(" ", composed.Problems), isProblem: true);
            return;
        }

        EntryText = string.Empty;
        Report(null, isProblem: false);
        Refresh();
    }

    /// <summary>
    /// A line that is only a note. Work is logged first and commented afterwards, so a note of its
    /// own belongs to the entry the day ended with — the same end "till" and "for" continue from.
    /// </summary>
    private void Note(NoteParse note)
    {
        var composed = note.Succeeded
            ? _composer.Note(Day, note.Note!)
            : ComposeResult.Refused(note.Error!);

        if (!composed.Created)
        {
            Report(string.Join(" ", composed.Problems), isProblem: true);
            return;
        }

        var noted = composed.Entry!;

        EntryText = string.Empty;
        Report($"Noted on {ClockText.Format(noted.Start)}–{ClockText.Format(noted.End)}: \"{noted.Note}\".", isProblem: false);
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
    /// Shows a file in the platform's file manager. The window supplies it, since what showing a
    /// file means belongs to the platform; nothing is opened when a test exports.
    /// </summary>
    public Action<string>? RevealFile { get; set; }

    /// <summary>The last export, while it is worth saying so; nothing after it is dismissed.</summary>
    [ObservableProperty]
    private ExportNoticeViewModel? _exported;

    public bool HasExported => Exported is not null;

    partial void OnExportedChanged(ExportNoticeViewModel? value) => OnPropertyChanged(nameof(HasExported));

    /// <summary>
    /// The range is the period the day on screen belongs to: that day, its week, its month, its
    /// quarter, its year. Building it here keeps the report and the page in one place, and the zip
    /// in one file.
    /// </summary>
    public ExportFile BuildExport(ExportPeriod period)
    {
        var report = RangeReport.Of(_local.Ledger.State, ExportRange.For(period, Day));
        var generatedAt = _clock();

        return new ExportFile(ExportBundle.ZipName(report, generatedAt), ExportBundle.Zip(report, generatedAt));
    }

    [RelayCommand]
    private Task ExportDayAsync() => Export(ExportPeriod.Day);

    [RelayCommand]
    private Task ExportWeekAsync() => Export(ExportPeriod.Week);

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
            ExportPeriod.Week => ExportWeekCommand,
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

        Exported = new ExportNoticeViewModel(path, RevealFile, DismissExport);
        Report(null, isProblem: false);
    }

    /// <summary>Puts the notice away; the export itself is written and stays where it went.</summary>
    private void DismissExport() => Exported = null;

    [RelayCommand]
    private void PreviousDay() => Show(Day.AddDays(-1));

    [RelayCommand]
    private void NextDay() => Show(Day.AddDays(1));

    /// <summary>
    /// Follows the calendar again, whatever day was picked. A page that was already following it is
    /// drawn again only if the clock moved under it, since that is the one thing a press can reveal.
    /// </summary>
    [RelayCommand]
    private void ShowToday()
    {
        if (SelectedDate is null)
        {
            Redraw();
            return;
        }

        SelectedDate = null;
    }

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

        foreach (var row in RowsOf(DayLayout.Of(_local.Ledger.State.EntriesOn(Day)), RowFor))
        {
            Rows.Add(row);
        }
    }

    /// <summary>
    /// The day as rows: every entry, and the hole the day leaves after it. One walk, so a hole can
    /// only ever sit between the two blocks that leave it.
    /// </summary>
    private IEnumerable<IDayRow> RowsOf(DayLayout layout, Func<Entry, EntryRowViewModel> entryRow)
    {
        var gaps = layout.Gaps.ToLookup(gap => gap.Start);
        var editable = !IsFrozen;

        foreach (var entry in layout.Entries)
        {
            yield return entryRow(entry);

            foreach (var gap in gaps[entry.Start.ToTimeSpan() + entry.Duration])
            {
                yield return new GapRowViewModel(gap, FillGap, RecordBreak, editable);
            }
        }
    }

    private EntryRowViewModel RowFor(Entry entry)
    {
        var row = new EntryRowViewModel(entry, Projects, () => _local.Ledger.State, Revise, Point, Retract, !IsFrozen);

        // A refused edit is the one case that rebuilds: the box holds the text that was refused,
        // and only a row built again from the journal shows what is stored instead.
        row.Rejected += (_, _) => Dispatcher.UIThread.Post(RefreshRows);

        return row;
    }

    /// <summary>
    /// Puts the holes back where the day now leaves them, leaving every entry row exactly as it is:
    /// an accepted correction comes from the cell someone is typing in, and only the holes beside it
    /// moved. So the hole rows are the only ones built again.
    /// </summary>
    private void RefreshGaps()
    {
        var wanted = WantedRows();

        for (var index = 0; index < Math.Min(wanted.Count, Rows.Count); index++)
        {
            Put(index, wanted[index]);
        }

        Trim(wanted.Count);

        foreach (var row in wanted.Skip(Rows.Count))
        {
            Rows.Add(row);
        }
    }

    /// <summary>The day's rows as they should read now, an entry keeping the row it already has.</summary>
    private List<IDayRow> WantedRows()
    {
        var kept = Rows.OfType<EntryRowViewModel>().ToDictionary(row => row.Id);

        return
        [
            .. RowsOf(
                DayLayout.Of(_local.Ledger.State.EntriesOn(Day)),
                entry => kept.GetValueOrDefault(entry.Id) ?? RowFor(entry)),
        ];
    }

    /// <summary>One row replaced only when it is not the row that belongs there.</summary>
    private void Put(int index, IDayRow row)
    {
        if (!ReferenceEquals(Rows[index], row))
        {
            Rows[index] = row;
        }
    }

    /// <summary>Drops what the day no longer has: a correction that closes a hole leaves one row too many.</summary>
    private void Trim(int count)
    {
        while (Rows.Count > count)
        {
            Rows.RemoveAt(Rows.Count - 1);
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
            .OrderBy(project => project.Name, Dates.Names)
            .Select(project => project.Name)
            .ToList();

    /// <summary>
    /// The day in words. English and day first, from <see cref="Dates"/>: the machine's own language
    /// would put a German weekday in front of the English word "Today".
    /// </summary>
    private string DescribeDay(DateOnly day) =>
        day == Today
            ? $"Today, {day.ToString("d MMMM", Dates.English)}"
            : day.ToString("dddd d MMMM", Dates.English);

    /// <summary>
    /// A correction of one row. On success the entry rows are left alone — only the holes beside the
    /// corrected block move — so the control that was being edited keeps focus.
    /// </summary>
    private Entry? Revise(Entry after)
    {
        if (!_local.Ledger.State.Entries.TryGetValue(after.Id, out var before))
        {
            return null;
        }

        var result = FromARow(() => _composer.Revise(before, after));

        if (!result.Created)
        {
            Report(string.Join(" ", result.Problems), isProblem: true);
            return null;
        }

        Report(null, isProblem: false);
        UpdateTotals();
        RefreshGaps();
        _shown = Signature();

        return result.Entry;
    }

    /// <summary>
    /// A project or topic typed in a row: the name is resolved, and one nobody used yet is opened.
    /// Every row offers the names of the day, so the list is refreshed once the row has its answer.
    /// </summary>
    private ComposeResult Point(Entry entry, string project, string? topic)
    {
        var pointed = FromARow(() => _composer.Point(entry, project, topic));

        if (pointed.Created)
        {
            RefreshProjects();
            UpdateTotals();
            _shown = Signature();
        }

        return pointed;
    }

    /// <summary>
    /// Runs a change a row asked for. That row has already corrected itself from the answer, so the
    /// ledger's echo must not rebuild the rows under the control being typed in — a rebuild takes
    /// the focus, and with it the rest of what was being written. What is on screen is the fold
    /// again once the row is done, so the next echo that differs is somebody else's and is shown.
    /// </summary>
    private T FromARow<T>(Func<T> change)
    {
        _aRowIsWriting = true;

        try
        {
            return change();
        }
        finally
        {
            _aRowIsWriting = false;
        }
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
        Total = Durations.Format(_local.Ledger.State.WorkedOn(Day));
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

        foreach (var suggestion in Offers())
        {
            Suggestions.Add(suggestion);
        }

        SuggestionCaption = _proposals.Field == EntryStep.Topic ? "TOPICS" : "PROJECTS";

        OnPropertyChanged(nameof(HasSuggestions));
    }

    /// <summary>One chip per offer, the one the arrows are on marked as the chosen one.</summary>
    private IEnumerable<SuggestionViewModel> Offers() =>
        _proposals.Names.Select((name, index) =>
            new SuggestionViewModel(name, index == _chosen, new RelayCommand(() => Take(name))));

    /// <summary>
    /// Says what to write next, and complains as soon as the line is complete enough to be wrong —
    /// an intersection is visible before Enter, not after it. A line that is already whole is told
    /// what it can still take: the note, which is the last thing a line carries.
    /// </summary>
    private void UpdateGuide()
    {
        var lastEnd = _local.Ledger.State.EndOfLast(Day);
        var step = EntryGuide.Next(EntryText, lastEnd);

        NextStep = step == EntryStep.Complete
            ? EntryGuide.Label(EntryStep.Complete)
            : $"next: {EntryGuide.Label(step, EntryText, lastEnd)}";

        var parse = EntryCommandParser.Parse(EntryText, Day, _local.Ledger.State.EndOfLast);
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
    /// Draws the page again when what it would show differs from what it shows. A change that cannot
    /// be seen here — an agent logging on another day, a project renamed, the night passing under a
    /// window that follows the calendar — would otherwise rebuild the rows and take the cell a person
    /// is typing in with it. A row's own correction is the same case: the row already shows it.
    /// </summary>
    private void Redraw()
    {
        if (_aRowIsWriting || Signature() == _shown)
        {
            return;
        }

        Refresh();
    }

    /// <summary>Says something to the person at the window: a start-up problem, or news from elsewhere.</summary>
    public void Say(string message, bool isProblem) => Report(message, isProblem);
}
