// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace Worksheet.Core;

/// <summary>
/// A note can be a line of its own: <c># the paper</c>. Work is logged quickly, and what it was
/// about is often remembered a moment later; the note then belongs to the stretch of the day that
/// ended last, the same end <c>till</c> and <c>for</c> continue from. The mark ends what a line can
/// read as a name or a time, so this is asked only for a line the entry grammar cannot read — the
/// note is the last thing a line carries, after the span it is about.
/// </summary>
public static class NoteLine
{
    public const string MissingNoteMessage =
        "The \"#\" asks for a note; write it after the mark, e.g. \"# the paper\".";

    public const string NothingToNoteMessage =
        "There is no entry on this day to note; log the work first.";

    /// <summary>
    /// The note the line is, or nothing when the line does not open with the mark. A caller reads a
    /// line as an entry first and asks this second, so the mark never takes a project or a time.
    /// </summary>
    public static NoteParse? Parse(string? text)
    {
        var line = (text ?? string.Empty).Trim();

        if (!line.StartsWith('#'))
        {
            return null;
        }

        var note = line[1..].Trim();

        return note.Length == 0 ? NoteParse.Fail(MissingNoteMessage) : NoteParse.Ok(note);
    }
}

/// <summary>
/// The result of reading a line that is only a note: the note, or one sentence saying what is
/// missing. Shaped like <see cref="EntryParse"/>, because the entry field answers both the same way.
/// </summary>
public readonly record struct NoteParse(string? Note, string? Error)
{
    public bool Succeeded => Note is not null;

    public static NoteParse Ok(string note) => new(note, null);

    public static NoteParse Fail(string error) => new(null, error);
}
