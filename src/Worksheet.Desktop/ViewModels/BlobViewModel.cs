// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Globalization;
using Worksheet.Core;

namespace Worksheet.Desktop.ViewModels;

/// <summary>
/// One stored change, entire: what it was written as, when it landed and from where, above the body
/// itself — indented, coloured, and selectable, since a person reading an audit usually wants to
/// carry a piece of it somewhere else.
/// </summary>
public sealed record BlobViewModel(AuditRow Row)
{
    public string Title => $"Version {Row.Version} · {Row.Kind}";

    public string Subtitle =>
        $"{Row.When} · written by {Row.Origin.ToLower(CultureInfo.InvariantCulture)}";

    public string Body => Row.Body;
}
