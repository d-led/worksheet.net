// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace Worksheet.UiTests;

/// <summary>
/// Where the frames a test draws are kept. The folder sits beside the build's other artefacts, so a
/// failing or interesting run can be looked at rather than described; a machine may point it
/// somewhere else with <c>WORKSHEET_UI_SHOTS</c>, which is what a build server does.
/// </summary>
internal static class Screens
{
    public static string PathFor(string testClass, string name) =>
        Path.Combine(Folder, testClass, $"{name}.png");

    public static string Folder =>
        Environment.GetEnvironmentVariable("WORKSHEET_UI_SHOTS") is { Length: > 0 } chosen
            ? chosen
            : Path.Combine(RepositoryRoot, "artifacts", "ui-tests");

    /// <summary>The folder the solution is in, found by walking up from the test binary.</summary>
    private static string RepositoryRoot =>
        Walk(Path.GetDirectoryName(typeof(Screens).Assembly.Location)!) ?? Path.GetTempPath();

    private static string? Walk(string folder)
    {
        for (var found = folder; found is not null; found = Path.GetDirectoryName(found))
        {
            if (File.Exists(Path.Combine(found, "Worksheet.slnx")))
            {
                return found;
            }
        }

        return null;
    }
}
