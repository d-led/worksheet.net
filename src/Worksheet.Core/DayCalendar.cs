// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace Worksheet.Core;

/// <summary>
/// What kind of day each day of the ledger is. A day nobody has declared follows the calendar:
/// Saturdays and Sundays are weekends, the rest work. Declaring the kind the day already has by
/// default clears the declaration instead of storing one, so the journal keeps saying only what
/// was actually decided.
/// </summary>
public sealed class DayCalendar(Ledger ledger)
{
    public DayKind KindOf(DateOnly day) => ledger.State.KindOf(day);

    /// <summary>Declares what the day is; declaring what it already is changes nothing.</summary>
    public void Declare(DateOnly day, DayKind kind)
    {
        if (KindOf(day) == kind)
        {
            return;
        }

        var before = ledger.State.DeclaredKindOf(day);
        DayKind? after = kind == DayKinds.DefaultOf(day) ? null : kind;

        ledger.Commit([new DayKindChanged(day, before, after)]);
    }
}
