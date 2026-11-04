// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace Worksheet.Export;

/// <summary>
/// What one export consists of: the data and the page, together in one file to keep. Small enough
/// that the zip is built in memory and handed over whole.
/// </summary>
public static class ExportBundle
{
    public const string JsonExtension = ".json";

    public const string HtmlExtension = ".html";

    public const string ZipExtension = ".zip";

    /// <summary>
    /// The name a person sees in the save dialog: when the export was taken, then the range it
    /// covers, so two exports of the same month cannot land on one another.
    /// </summary>
    public static string ZipName(RangeReport report, DateTimeOffset generatedAt) =>
        $"{Moment(generatedAt)}-{report.Range.FileStem}{ZipExtension}";

    /// <summary>A moment written so that it sorts and so that no platform refuses it as a name.</summary>
    private static string Moment(DateTimeOffset moment) =>
        moment.ToString("yyyy-MM-dd-HHmm", CultureInfo.InvariantCulture);

    public static byte[] Zip(RangeReport report, DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(report);

        using var buffer = new MemoryStream();

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(archive, report.Range.FileStem + JsonExtension, ReportJson.Of(report, generatedAt));
            Add(archive, report.Range.FileStem + HtmlExtension, ReportHtml.Of(report, generatedAt));
        }

        return buffer.ToArray();
    }

    private static void Add(ZipArchive archive, string name, string content)
    {
        using var stream = archive.CreateEntry(name, CompressionLevel.Optimal).Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        writer.Write(content);
    }
}
