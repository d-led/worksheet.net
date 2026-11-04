// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SukiUI.Controls;
using Worksheet.Core;
using Worksheet.Desktop;
using Worksheet.Desktop.Components;
using Worksheet.Desktop.ViewModels;
using Worksheet.Desktop.Views;
using Worksheet.Storage;

// Pictures for the documentation, rendered from the real application rather than drawn by hand.
//
//     dotnet run --project tools/Worksheet.Screenshot -- docs/screenshots/day.png
//     dotnet run --project tools/Worksheet.Screenshot -- docs/screenshots/frozen-day.png sick --only status
//     dotnet run --project tools/Worksheet.Screenshot -- docs/screenshots/completion.png --typing "Client portal (" --only entry
//     dotnet run --project tools/Worksheet.Screenshot -- docs/screenshots/break.png --only break
//
// `--only` narrows what is drawn: `entry` is the line and its offers, `status` is the day's status
// with one frozen row beside it, `break` is a hole before and after taking it as a break. Without
// it, the whole window.

var output = args.Length > 0 ? args[0] : "docs/screenshots/day.png";
var only = ValueAfter("--only") ?? "window";
var kind = ValueAfter("--kind") is { } named ? Enum.Parse<DayKind>(named, ignoreCase: true) : DayKind.Working;
var typing = ValueAfter("--typing");
var dataDirectory = Path.Combine(Path.GetTempPath(), "worksheet-screenshot", Guid.NewGuid().ToString("N"));

AppBuilder.Configure<App>()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .UseSkia()
    .SetupWithoutStarting();

using var local = LocalLedger.Open(dataDirectory, SnapshotPolicy.Disabled);

// A fixed Monday: the picture must not change with the day it is rendered on, and a weekend
// would freeze the timetable it is meant to show.
var today = new DateOnly(2026, 11, 2);

foreach (var line in only switch { "break" => TheHole(), "status" => TheFrozenDay(), _ => TheDay() })
{
    Compose(local, line, today);
}

var day = new DayViewModel(local, today)
{
    EntryText = typing ?? string.Empty,
};

var frames = only switch
{
    "entry" => [Capture(EntryOnly(day))],
    "status" => [Capture(StatusAndOneRow(local, today))],
    "break" => BreakBeforeAndAfter(local, day),
    _ => [Capture(FullWindow(day))],
};

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
Stack(frames).Save(output, new PngBitmapEncoderOptions());

Console.WriteLine($"Wrote {output} (total {day.Total})");
Directory.Delete(dataDirectory, recursive: true);

// The day the documentation shows: eight hours over three projects, a 45 minute break, and one
// hole left in it.
static IEnumerable<string> TheDay()
{
    yield return "Client portal (login) 08:30-11:00";
    yield return "Client portal (reports) 11:00-12:30";
    yield return "Break 12:30 45m";
    yield return "Mobile app (checkout) 13:30-15:00";
    yield return "Internal (onboarding) 15:00 2h30m";
}

// Two blocks with nothing between them, for the picture of a hole.
static IEnumerable<string> TheHole()
{
    yield return "Client portal (login) 09:00-10:00";
    yield return "Mobile app (checkout) 11:00-12:00";
}

// One block, and a day that does not work: enough to show that the timetable stays and freezes.
static IEnumerable<string> TheFrozenDay()
{
    yield return "Client portal (login) 09:00-11:00";
}

static void Compose(LocalLedger local, string line, DateOnly day)
{
    var parse = EntryCommandParser.Parse(line, day);

    if (!parse.Succeeded)
    {
        throw new InvalidOperationException($"The example day is not valid: {parse.Error}");
    }

    var composed = new EntryComposer(local.Ledger).Compose(parse.Command!);

    if (!composed.Created)
    {
        throw new InvalidOperationException(string.Join(" ", composed.Problems));
    }
}

// The whole application, at the size the documentation shows.
static Window FullWindow(DayViewModel day) =>
    new MainWindow
    {
        DataContext = day,
        Width = 1000,
        Height = 620,
        IsTitleBarVisible = false,
    };

// Only the entry line and the offers under it, on the same surface the window paints.
static Window EntryOnly(DayViewModel day) =>
    Surface(day, 132, new QuickEntry());

// The status of the day, with one row of the timetable frozen beside it.
static Window StatusAndOneRow(LocalLedger local, DateOnly today)
{
    new DayCalendar(local.Ledger).Declare(today, DayKind.Sick);

    var frozen = new DayViewModel(local, today);

    return Surface(
        frozen,
        150,
        new DayStatus(),
        new DayTable { Margin = new Thickness(0, 10, 0, 0) });
}

// The hole as it is, and the same table after its Break button was pressed: the two states of one
// conversion, in one picture.
Bitmap[] BreakBeforeAndAfter(LocalLedger local, DayViewModel day)
{
    var table = new DayTable();
    var window = Surface(day, 172, table);
    var before = Capture(window);

    var hole = day.Rows.OfType<GapRowViewModel>().Single();
    hole.BreakCommand.Execute(null);
    Dispatcher.UIThread.RunJobs();

    return [before, Capture(window)];
}

// A window that paints the application's own surface, so the pieces look like they do in use.
static Window Surface(DayViewModel day, double height, params Control[] content)
{
    var stack = new StackPanel { Spacing = 0 };

    foreach (var control in content)
    {
        control.DataContext = day;
        stack.Children.Add(control);
    }

    return new SukiWindow
    {
        DataContext = day,
        Width = 1000,
        Height = height,
        IsTitleBarVisible = false,
        Content = new Border { Classes = { "page" }, Padding = new Thickness(20, 16), Child = stack },
    };
}

static Bitmap Capture(Window window)
{
    window.Show();
    Dispatcher.UIThread.RunJobs();

    return window.CaptureRenderedFrame()
        ?? throw new InvalidOperationException("The headless platform rendered no frame.");
}

// One picture from several, so that before and after are seen together.
static Bitmap Stack(Bitmap[] frames)
{
    if (frames.Length == 1)
    {
        return frames[0];
    }

    var width = frames.Max(frame => frame.PixelSize.Width);
    var height = frames.Sum(frame => frame.PixelSize.Height) + 12 * (frames.Length - 1);
    var picture = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));

    using (var context = picture.CreateDrawingContext())
    {
        double top = 0;

        foreach (var frame in frames)
        {
            context.DrawImage(frame, new Rect(0, top, frame.PixelSize.Width, frame.PixelSize.Height));
            top += frame.PixelSize.Height + 12;
        }
    }

    return picture;
}

// The value after a flag, e.g. --only entry, or nothing when the flag is absent.
string? ValueAfter(string flag)
{
    var index = Array.IndexOf(args, flag);

    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
