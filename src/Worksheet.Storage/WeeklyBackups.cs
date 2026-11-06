// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Globalization;

namespace Worksheet.Storage;

/// <summary>
/// One copy of the journal per week that has closed, kept in the journal's own folder. Four are kept,
/// which is a month to fall back on; the fifth week deletes the first. A copy is the file itself, so
/// putting it back where the journal sits is the whole of a restore.
/// </summary>
public sealed class WeeklyBackups(string folder, Action<string> copy, Func<DateTimeOffset> now)
{
    /// <summary>How many weekly copies stay on disk.</summary>
    public const int Kept = 4;

    /// <summary>A copy is named for the week it closes: which year it belongs to comes first, so the names sort.</summary>
    private const string Prefix = "ledger-";

    private const string Suffix = ".db";

    /// <summary>
    /// What a copy is written to before it has a week's name. A copy that stopped halfway — no room,
    /// or the process ending — is then not the week it was writing, and the next poll takes it again.
    /// </summary>
    private const string HalfWritten = ".part";

    /// <summary>
    /// Copies the journal when the week that has just closed has no copy yet, drops the oldest beyond
    /// the four kept, and answers with the file written — or with nothing when that week is done.
    /// </summary>
    public string? Take()
    {
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var target = Path.Combine(folder, Name(ClosedWeek(now())));
        var half = target + HalfWritten;

        // A copy an earlier attempt did not finish is not a copy: nothing can tell it from a week
        // that is done, so it goes before the week is taken again.
        File.Delete(half);

        if (File.Exists(target))
        {
            return null;
        }

        copy(half);
        File.Move(half, target);
        Keep();

        return target;
    }

    /// <summary>The week that has closed is the one holding the last Sunday: a week ends on a Sunday night.</summary>
    private static DateOnly ClosedWeek(DateTimeOffset moment)
    {
        var sinceMonday = ((int)moment.DayOfWeek + 6) % 7;

        return DateOnly.FromDateTime(moment.Date).AddDays(-(sinceMonday + 1));
    }

    /// <summary>ledger-2026-W44.db — the journal, as it stood when week 44 closed.</summary>
    private static string Name(DateOnly day) =>
        Prefix
        + ISOWeek.GetYear(day.ToDateTime(TimeOnly.MinValue)).ToString(CultureInfo.InvariantCulture)
        + "-W"
        + ISOWeek.GetWeekOfYear(day.ToDateTime(TimeOnly.MinValue)).ToString("00", CultureInfo.InvariantCulture)
        + Suffix;

    /// <summary>
    /// Drops every copy beyond the four newest. The names sort chronologically — year, then week
    /// number — so nothing has to read the files to know which is which.
    /// </summary>
    private void Keep()
    {
        foreach (var stale in Copied().SkipLast(Kept))
        {
            File.Delete(stale);
        }
    }

    private string[] Copied() =>
        [.. Directory.GetFiles(folder, Prefix + "*" + Suffix).Order(StringComparer.Ordinal)];
}
