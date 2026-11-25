// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace Worksheet.Core;

/// <summary>One project's share of a day: its name, and what its stretches on that day add up to.</summary>
public sealed record ProjectTotal(string Project, TimeSpan Length);
