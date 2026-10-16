using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;

namespace ProcessImageJob;

public static class ImageHelper
{
    // The font file shipped with the app (Fonts/DejaVuSans.ttf), so it also works in a
    // Linux container, which has no fonts installed
    private static readonly FontCollection BundledFonts = new();
    private static readonly FontFamily? BundledFamily = LoadBundledFont();

    public static Stream AddTextToImage(
        Stream imageStream,
        params (string text, (float x, float y) position, int fontSize, string colorHex)[] texts)
    {
        using var image = Image.Load(imageStream);

        image.Mutate(img =>
        {
            foreach (var (text, (x, y), fontSize, colorHex) in texts)
            {
                var font = ResolveFont(fontSize);
                var color = Color.ParseHex(colorHex);
                var options = new RichTextOptions(font)
                {
                    Origin = new PointF(x, y),
                    WrappingLength = image.Width - 10
                };

                // Measure how big the text will be...
                var bounds = TextMeasurer.MeasureBounds(text, options);

                // ...and draw a dark, see-through box behind it (with some padding)
                const float padding = 10;
                var box = new RectangleF(
                    bounds.X - padding,
                    bounds.Y - padding,
                    bounds.Width + padding * 2,
                    bounds.Height + padding * 2);
                img.Fill(Color.Black.WithAlpha(0.6f), box);

                // Draw the text on top of the box
                img.DrawText(options, text, color);
            }
        });

        var memoryStream = new MemoryStream();
        image.SaveAsPng(memoryStream);
        memoryStream.Position = 0;
        return memoryStream;
    }

    private static FontFamily? LoadBundledFont()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fonts", "DejaVuSans.ttf");
        return File.Exists(path) ? BundledFonts.Add(path) : null;
    }

    private static Font ResolveFont(int fontSize)
    {
        // 1. Prefer the bundled font: same look locally and in Azure
        if (BundledFamily.HasValue)
        {
            return BundledFamily.Value.CreateFont(fontSize);
        }

        // 2. Otherwise try fonts installed on the machine
        foreach (var name in new[] { "Verdana", "DejaVu Sans", "Liberation Sans", "Arial" })
        {
            if (SystemFonts.TryGet(name, out var family))
            {
                return family.CreateFont(fontSize);
            }
        }

        return SystemFonts.Families.First().CreateFont(fontSize);
    }
}