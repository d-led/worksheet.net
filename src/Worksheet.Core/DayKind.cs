// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace Worksheet.Core;

/// <summary>
/// What a day is. Only a working day takes entries; every other kind freezes the timetable
/// without touching what was already recorded.
/// </summary>
public enum DayKind
{
    Working,
    Flex,
    Sick,
    Holiday,
    Weekend,
}

/// <summary>The words and the rules that go with each kind of day.</summary>
public static class DayKinds
{
    private static readonly string[] Labels =
    [
        "Working day",
        "Flex day",
        "Sick day",
        "Holiday",
        "Weekend",
    ];

    public static string LabelOf(DayKind kind) => Labels[(int)kind];

    /// <summary>Whether a day of this kind can be worked on, and therefore edited.</summary>
    public static bool TakesEntries(DayKind kind) => kind == DayKind.Working;

    /// <summary>What a day is when nobody has said otherwise: weekends rest, the rest work.</summary>
    public static DayKind DefaultOf(DateOnly day) =>
        day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? DayKind.Weekend : DayKind.Working;
}

/// <summary>
/// The project a hole is recorded under when it is taken as a break, and the note it is recorded
/// with, so that the row is annotated and the comment is there to be changed.
/// </summary>
public static class Break
{
    public const string Project = "Break";

    public const string Note = "break";

    /// <summary>Why a break may not carry a topic: what came before or after it is not a topic.</summary>
    public const string RefusedTopic = "A break has no topic; leave the brackets out.";

    /// <summary>Whether a project is where breaks go, which is the one project without topics.</summary>
    public static bool IsBreak(string? projectName) =>
        string.Equals(projectName?.Trim(), Break.Project, StringComparison.OrdinalIgnoreCase);
}
