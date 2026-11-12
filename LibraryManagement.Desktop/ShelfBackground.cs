using System.Windows;
using System.Windows.Media;

namespace LibraryManagement.Desktop;

/// <summary>Procedurally drawn warm bookshelf wall (no image assets). Apply a BlurEffect in XAML.</summary>
public sealed class ShelfBackground : FrameworkElement
{
    public int Seed { get; set; } = 1;
    static readonly string[] Palette =
        { "#5B3A29", "#7A4B2A", "#3E4F3A", "#2F3E55", "#6E2C2C", "#8C6A3C", "#4A3A52", "#A0764A", "#2B2B2B", "#9A8A5A" };

    protected override void OnRender(DrawingContext dc)
    {
        double W = ActualWidth, H = ActualHeight;
        dc.DrawRectangle(new LinearGradientBrush(Ui.C("#3A2414"), Ui.C("#1A0F08"), 90), null, new Rect(0, 0, W, H));
        var rnd = new Random(Seed);
        double shelfH = 170;
        for (double y = 20; y < H; y += shelfH)
        {
            double x = 0;
            while (x < W)
            {
                double bw = rnd.Next(16, 40), bh = rnd.Next(100, 150);
                if (rnd.NextDouble() < 0.07) { x += rnd.Next(20, 60); continue; }
                var c = Ui.C(Palette[rnd.Next(Palette.Length)]);
                var br = new LinearGradientBrush(Lighten(c, .12), Darken(c, .25), 0);
                dc.DrawRectangle(br, null, new Rect(x, y + shelfH - 16 - bh, bw, bh));
                dc.DrawRectangle(Ui.B("#88D9B76A"), null, new Rect(x + 3, y + shelfH - 16 - bh + 12, bw - 6, 2));
                x += bw + 1;
            }
            dc.DrawRectangle(new LinearGradientBrush(Ui.C("#8A5A32"), Ui.C("#3B2210"), 90), null, new Rect(0, y + shelfH - 16, W, 16));
        }
        // warm light pools
        foreach (var (px, py, r) in new[] { (0.15, 0.1, 380.0), (0.55, 0.05, 460.0), (0.92, 0.2, 360.0) })
            dc.DrawEllipse(new RadialGradientBrush(Ui.C("#88FFC878"), Colors.Transparent), null, new Point(W * px, H * py), r, r);
    }

    static Color Lighten(Color c, double t) => Color.FromRgb((byte)(c.R + (255 - c.R) * t), (byte)(c.G + (255 - c.G) * t), (byte)(c.B + (255 - c.B) * t));
    static Color Darken(Color c, double t) => Color.FromRgb((byte)(c.R * (1 - t)), (byte)(c.G * (1 - t)), (byte)(c.B * (1 - t)));
}

