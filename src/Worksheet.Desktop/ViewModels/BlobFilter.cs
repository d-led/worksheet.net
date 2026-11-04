// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Worksheet.Core;

namespace Worksheet.Desktop.ViewModels;

/// <summary>
/// A free-text filter over the journal: the filter is split at the spaces, and a blob is kept only
/// when every piece of it appears somewhere in what the blob holds — its kind, its origin, its
/// version, its time and the stored body. An empty filter keeps everything, because that is what a
/// person who has typed nothing is asking for.
/// </summary>
public static class BlobFilter
{
    /// <summary>The pieces of a filter, with the empty ones a run of spaces leaves behind dropped.</summary>
    public static string[] Tokens(string? filter) =>
        [.. (filter ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)];

    /// <summary>Whether every piece of the filter is somewhere in the blob.</summary>
    public static bool Keeps(JournalBlob blob, IEnumerable<string> tokens)
    {
        ArgumentNullException.ThrowIfNull(blob);
        ArgumentNullException.ThrowIfNull(tokens);

        var text = blob.Searchable;

        return tokens.All(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    public static IEnumerable<JournalBlob> Apply(IEnumerable<JournalBlob> blobs, string? filter)
    {
        ArgumentNullException.ThrowIfNull(blobs);

        var tokens = Tokens(filter);

        return blobs.Where(blob => Keeps(blob, tokens));
    }
}
