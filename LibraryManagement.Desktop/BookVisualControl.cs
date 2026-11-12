using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace LibraryManagement.Desktop;

/// <summary>
/// Procedural 2.5D book seen from slightly above: perspective top cover (with title), spine strip,
/// and a front band showing boards + page block. Height = Book.Depth + thickness (set by the stack).
/// </summary>
public sealed class BookVisualControl : FrameworkElement
{
    public static readonly DependencyProperty BookProperty = DependencyProperty.Register(
        nameof(Book), typeof(BookItem), typeof(BookVisualControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public BookItem? Book { get => (BookItem?)GetValue(BookProperty); set => SetValue(BookProperty, value); }

    public BookVisualControl()
    {
        Effect = new DropShadowEffect { BlurRadius = 20, ShadowDepth = 6, Opacity = 0.55, Direction = 270 };
    }

    static Geometry Quad(double x1, double y1, double x2, double y2, double x3, double y3, double x4, double y4)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(x1, y1), true, true);
            c.LineTo(new Point(x2, y2), true, false); c.LineTo(new Point(x3, y3), true, false); c.LineTo(new Point(x4, y4), true, false);
        }
        g.Freeze(); return g;
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (Book is null || ActualWidth < 20 || ActualHeight < 20) return;
        double w = ActualWidth, H = ActualHeight, fh = Math.Min(Book.Depth, H - 8), t = H - fh;
        double ins = w * 0.05;
        var cover = Ui.C(Book.Cover); var acc = Ui.C(Book.Accent);
        var accSoft = new SolidColorBrush(Color.FromArgb(170, acc.R, acc.G, acc.B));
        var accPen = new Pen(accSoft, 1);

        // ---- top cover (perspective trapezoid)
        dc.DrawGeometry(new LinearGradientBrush(Light(cover, .24), Dark(cover, .14), 90), new Pen(new SolidColorBrush(acc), 1),
            Quad(ins, 0, w - ins, 0, w, fh, 0, fh));
        // soft sheen from the window light
        dc.DrawGeometry(new LinearGradientBrush(Color.FromArgb(70, 255, 255, 255), Colors.Transparent, 20),
            null, Quad(ins, 0, w * 0.6, 0, w * 0.45, fh, 0, fh));
        // spine strip on the left with gilded bands
        dc.DrawGeometry(new LinearGradientBrush(Dark(cover, .45), Dark(cover, .12), 0), null, Quad(ins, 0, ins + 18, 0, 18, fh, 0, fh));
        dc.DrawLine(accPen, new Point(ins + 5, 2), new Point(5, fh - 2));
        dc.DrawLine(accPen, new Point(ins + 12, 2), new Point(12, fh - 2));
        // inner gilt frame
        dc.DrawGeometry(null, new Pen(accSoft, 0.9), Quad(ins + 28, 10, w - ins - 12, 10, w - 14, fh - 10, 26, fh - 10));

        // title + author, flattened to sit on the perspective plane
        double cx = (w + 14) / 2, cy = fh / 2, flat = 0.56;
        dc.PushTransform(new MatrixTransform(new Matrix(1, 0, 0, flat, 0, cy * (1 - flat))));
        double ts = Math.Clamp(w * 0.085, 18, 30);
        var ft = new FormattedText(Book.Title, CultureInfo.GetCultureInfo("tr-TR"), FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Georgia"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            ts, new SolidColorBrush(acc), VisualTreeHelper.GetDpi(this).PixelsPerDip)
        { MaxTextWidth = w * 0.68, MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };
        dc.DrawText(ft, new Point(cx - ft.Width / 2, cy - ft.Height / 2 - 8));
        var fa = new FormattedText(Book.Author.ToUpper(CultureInfo.GetCultureInfo("tr-TR")), CultureInfo.GetCultureInfo("tr-TR"), FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 11, accSoft, VisualTreeHelper.GetDpi(this).PixelsPerDip)
        { MaxTextWidth = w * 0.6, MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };
        dc.DrawText(fa, new Point(cx - fa.Width / 2, cy + ts * 0.55));
        dc.Pop();

        // ---- front band: top board, page block, bottom board
        double tb = Math.Max(3, t * 0.24);
        dc.DrawRectangle(new LinearGradientBrush(Light(cover, .04), Dark(cover, .38), 90), null, new Rect(0, fh, w, tb));
        dc.DrawRectangle(new LinearGradientBrush(Ui.C("#F8F0DA"), Ui.C("#C9BA90"), 90), null, new Rect(5, fh + tb, w - 10, t - 2 * tb));
        var line = new Pen(new SolidColorBrush(Color.FromArgb(55, 90, 70, 30)), 0.6);
        for (double y = fh + tb + 2; y < fh + t - tb - 1; y += 2.6) dc.DrawLine(line, new Point(7, y), new Point(w - 7, y));
        dc.DrawRectangle(new LinearGradientBrush(Dark(cover, .3), Dark(cover, .6), 90), null, new Rect(0, fh + t - tb, w, tb));
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)), 0.8), new Point(0, fh + .5), new Point(w, fh + .5));
    }

    static Color Light(Color c, double t) => Color.FromRgb((byte)(c.R + (255 - c.R) * t), (byte)(c.G + (255 - c.G) * t), (byte)(c.B + (255 - c.B) * t));
    static Color Dark(Color c, double t) => Color.FromRgb((byte)(c.R * (1 - t)), (byte)(c.G * (1 - t)), (byte)(c.B * (1 - t)));
}
