using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;

namespace ProcessImageJob;

public static class ImageHelper
{
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

                var bounds = TextMeasurer.MeasureBounds(text, options);

                const float padding = 10;
                var box = new RectangleF(
                    bounds.X - padding,
                    bounds.Y - padding,
                    bounds.Width + padding * 2,
                    bounds.Height + padding * 2);
                img.Fill(Color.Black.WithAlpha(0.6f), box);

                img.DrawText(options, text, color);
            }
        });

        var memoryStream = new MemoryStream();
        image.SaveAsPng(memoryStream);
        memoryStream.Position = 0;
        return memoryStream;
    }

    private static Font ResolveFont(int fontSize)
    {
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