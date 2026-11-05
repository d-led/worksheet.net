// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Worksheet.UiTests;

/// <summary>
/// One frame of a window as it was really drawn — Skia laid the text out and painted the pixels — so
/// a test can keep the picture for the build's artefacts and ask what colour a pixel of it is. What
/// the control tree says and what the glass shows are two different claims; this is the second one.
/// </summary>
public sealed class Frame : IDisposable
{
    private readonly WriteableBitmap _drawn;
    private readonly byte[] _pixels;

    private Frame(WriteableBitmap drawn, byte[] pixels)
    {
        _drawn = drawn;
        _pixels = pixels;
        Size = drawn.PixelSize;
    }

    private PixelSize Size { get; }

    /// <summary>Draws the window and keeps the frame at that path; picture and pixels are the same frame.</summary>
    public static Frame Of(TopLevel window, string path)
    {
        ArgumentNullException.ThrowIfNull(window);

        var drawn = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException($"Nothing was drawn to capture in {path}.");

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);

        using (var file = File.Create(path))
        {
            drawn.Save(file, new PngBitmapEncoderOptions());
        }

        return new Frame(drawn, PixelsOf(drawn));
    }

    /// <summary>The colour at a point of the frame, counted from its top left corner.</summary>
    public Color At(int x, int y)
    {
        var pixel = _pixels.AsSpan((y * Size.Width + x) * 4, 4);

        // The frame is kept as the platform drew it: either the way round that spells RGBA, or the
        // one that spells BGRA, with the alpha last in both.
        return _drawn.Format == PixelFormat.Rgba8888
            ? Color.FromArgb(pixel[3], pixel[0], pixel[1], pixel[2])
            : Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
    }

    /// <summary>
    /// How far the ink of a stretch of the frame reaches: the width from the first pixel that differs
    /// from the surface behind it to the last. Words drawn in a cell make this wide, an empty cell
    /// narrow, whatever font the platform laid them out with.
    /// </summary>
    public int InkExtent(PixelRect area) => LastInked(area) - FirstInked(area);

    private int FirstInked(PixelRect area) => InkedColumns(area).DefaultIfEmpty(-1).First();

    private int LastInked(PixelRect area) => InkedColumns(area).DefaultIfEmpty(-1).Last();

    /// <summary>Which columns of the area hold a pixel that does not match the surface behind the ink.</summary>
    private IEnumerable<int> InkedColumns(PixelRect area)
    {
        var surface = Surface(area);

        for (var x = area.X; x < area.X + area.Width; x++)
        {
            if (Column(area, x).Any(pixel => FarApart(pixel, surface)))
            {
                yield return x;
            }
        }
    }

    private IEnumerable<Color> Column(PixelRect area, int x)
    {
        for (var y = area.Y; y < area.Y + area.Height; y++)
        {
            yield return At(x, y);
        }
    }

    /// <summary>The colour most of the area is: the surface the words are written on.</summary>
    private Color Surface(PixelRect area)
    {
        var counted = new Dictionary<Color, int>();

        for (var y = area.Y; y < area.Y + area.Height; y++)
        {
            for (var x = area.X; x < area.X + area.Width; x++)
            {
                var colour = At(x, y);

                counted[colour] = counted.GetValueOrDefault(colour) + 1;
            }
        }

        return counted.MaxBy(pair => pair.Value).Key;
    }

    /// <summary>Whether a pixel is far enough from the surface to be something drawn on it.</summary>
    private static bool FarApart(Color pixel, Color surface) =>
        Math.Abs(pixel.R - surface.R) + Math.Abs(pixel.G - surface.G) + Math.Abs(pixel.B - surface.B) > 96;

    private static byte[] PixelsOf(WriteableBitmap drawn)
    {
        var size = drawn.PixelSize;
        var stride = size.Width * 4;
        var pixels = new byte[stride * size.Height];
        var pinned = GCHandle.Alloc(pixels, GCHandleType.Pinned);

        try
        {
            drawn.CopyPixels(new PixelRect(0, 0, size.Width, size.Height), pinned.AddrOfPinnedObject(), pixels.Length, stride);
        }
        finally
        {
            pinned.Free();
        }

        return pixels;
    }

    public void Dispose() => _drawn.Dispose();
}
