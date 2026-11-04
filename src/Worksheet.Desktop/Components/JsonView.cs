// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Worksheet.Desktop.ViewModels;

namespace Worksheet.Desktop.Components;

/// <summary>
/// A body of JSON, written out with an indent and coloured by what each piece is: the names of
/// members, the text, the numbers, the words and the punctuation. It can be selected and copied, so
/// what an audit finds can leave the window.
/// </summary>
public sealed class JsonView : SelectableTextBlock
{
    public static readonly StyledProperty<string> BodyProperty =
        AvaloniaProperty.Register<JsonView, string>(nameof(Body), string.Empty);

    /// <summary>The colours of the five kinds of piece, as resources with a fallback so a window
    /// without them still reads.</summary>
    private static readonly Dictionary<JsonTone, (string Key, string Fallback)> Colours = new()
    {
        [JsonTone.Name] = ("JsonNameBrush", "#1F6FEB"),
        [JsonTone.Text] = ("JsonTextBrush", "#0F766E"),
        [JsonTone.Number] = ("JsonNumberBrush", "#9A3412"),
        [JsonTone.Keyword] = ("JsonKeywordBrush", "#7C3AED"),
        [JsonTone.Punctuation] = ("JsonPunctuationBrush", "#4B5563"),
    };

    public JsonView()
    {
        FontFamily = new FontFamily("Menlo, Monaco, Cascadia Mono, Consolas, DejaVu Sans Mono, monospace");
        FontSize = 13;
        TextWrapping = TextWrapping.NoWrap;
    }

    /// <summary>The stored body as it is; the view writes it out and colours it.</summary>
    public string Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == BodyProperty)
        {
            Show(Body);
        }
    }

    private void Show(string body)
    {
        Inlines?.Clear();

        foreach (var run in JsonText.Runs(body))
        {
            Inlines?.Add(new Run(run.Text) { Foreground = Brush(run.Tone) });
        }
    }

    /// <summary>The colour of one kind of piece; the one in the window's resources when it is there.</summary>
    private IBrush Brush(JsonTone tone)
    {
        var (key, fallback) = Colours[tone];

        return this.TryFindResource(key, out var found) && found is IBrush brush
            ? brush
            : new SolidColorBrush(Color.Parse(fallback));
    }
}
