// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace Worksheet.Core;

/// <summary>What the user typed, understood. Names are unresolved: they are matched against the ledger later.</summary>
public sealed record EntryCommand(
    DateOnly Date,
    string Project,
    string? Topic,
    TimeOnly Start,
    TimeSpan Duration,
    string? Note);

/// <summary>
/// The result of reading one line. A failure is a sentence, not an exception: user input is not
/// exceptional, and the caller shows the sentence next to the input.
/// </summary>
public readonly record struct EntryParse(EntryCommand? Command, string? Error)
{
    public bool Succeeded => Command is not null;

    public static EntryParse Ok(EntryCommand command) => new(command, null);

    public static EntryParse Fail(string error) => new(null, error);
}
