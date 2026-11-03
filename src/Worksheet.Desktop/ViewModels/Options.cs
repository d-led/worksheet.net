// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using CommunityToolkit.Mvvm.Input;

namespace Worksheet.Desktop.ViewModels;

/// <summary>A project as a picker shows it: an identity and a label.</summary>
public sealed record ProjectOption(Guid Id, string Label);

/// <summary>
/// A known name offered while typing. It carries its own command so the template needs no relative
/// binding to reach the window's data context, and knows whether the arrow keys are on it.
/// </summary>
public sealed class SuggestionViewModel(string text, bool isSelected, IRelayCommand use)
{
    public string Text { get; } = text;

    public bool IsSelected { get; } = isSelected;

    public IRelayCommand Use { get; } = use;
}
