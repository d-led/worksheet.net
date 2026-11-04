// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using CommunityToolkit.Mvvm.Input;
using Worksheet.Core;

namespace Worksheet.Desktop.ViewModels;

/// <summary>
/// A hole in the day, shown between the entries that leave it. It carries the two things that can
/// be done with a hole: put its times into the entry line, or take it as a break.
/// </summary>
public sealed class GapRowViewModel : IDayRow
{
    public GapRowViewModel(DaySlot gap, Action<DaySlot> fill, Action<DaySlot> takeBreak, bool editable)
    {
        ArgumentNullException.ThrowIfNull(gap);
        ArgumentNullException.ThrowIfNull(fill);
        ArgumentNullException.ThrowIfNull(takeBreak);

        Slot = gap;
        Span = $"{gap.StartsAt:HH:mm} – {gap.EndsAt:HH:mm}";
        Length = Durations.Format(gap.Duration);
        IsEditable = editable;
        FillCommand = new RelayCommand(() => fill(gap));
        BreakCommand = new RelayCommand(() => takeBreak(gap));
    }

    public DaySlot Slot { get; }

    public string Span { get; }

    public string Length { get; }

    /// <summary>False on a frozen day, which is why its two actions are out of reach.</summary>
    public bool IsEditable { get; }

    public IRelayCommand FillCommand { get; }

    public IRelayCommand BreakCommand { get; }
}
