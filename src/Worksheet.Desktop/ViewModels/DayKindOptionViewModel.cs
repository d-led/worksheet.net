// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using CommunityToolkit.Mvvm.ComponentModel;
using Worksheet.Core;

namespace Worksheet.Desktop.ViewModels;

/// <summary>
/// One of the five things a day can be, as a radio button. Checking it declares the day;
/// unchecking it — which a radio group does on its own — declares nothing. The kind's name travels
/// as the tag so that each kind can be given its own colour by the styles.
/// </summary>
public sealed partial class DayKindOptionViewModel : ObservableObject
{
    private readonly Action<DayKind> _choose;

    [ObservableProperty]
    private bool _isSelected;

    public DayKindOptionViewModel(DayKind kind, bool isSelected, Action<DayKind> choose)
    {
        ArgumentNullException.ThrowIfNull(choose);

        Kind = kind;
        _isSelected = isSelected;
        _choose = choose;
    }

    public DayKind Kind { get; }

    /// <summary>The kind as the enum spells it, which is what a style selector matches on.</summary>
    public string Name => Kind.ToString();

    public string Label => DayKinds.LabelOf(Kind);

    partial void OnIsSelectedChanged(bool value)
    {
        if (value)
        {
            _choose(Kind);
        }
    }
}
