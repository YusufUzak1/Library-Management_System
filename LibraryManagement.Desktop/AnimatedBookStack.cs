using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LibraryManagement.Desktop;

/// <summary>
/// Each book sits on the previous one: its front-bottom edge lands on the previous book's front-top edge,
/// so the stack grows by the book thickness only and the top covers overlap in perspective.
/// </summary>
public sealed class AnimatedBookStack : Canvas
{
    readonly Dictionary<BookItem, BookVisualControl> _map = new();
    public double Floor { get; set; } = 540;
    public double MaxStack { get; set; } = 380;   // max summed thickness

    static DoubleAnimation Ease(double to, int ms) =>
        new(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };

    public void Sync(IEnumerable<BookItem> books)
    {
        var list = books.ToList();
        foreach (var gone in _map.Keys.Where(k => !list.Contains(k)).ToList())
        {
            var v = _map[gone]; _map.Remove(gone);
            var g = (TransformGroup)v.RenderTransform;
            ((TranslateTransform)g.Children[0]).BeginAnimation(TranslateTransform.XProperty, Ease(220, 380));
            ((RotateTransform)g.Children[1]).BeginAnimation(RotateTransform.AngleProperty, Ease(14, 380));
            var fade = Ease(0, 340); fade.Completed += (_, _) => Children.Remove(v);
            v.BeginAnimation(OpacityProperty, fade);
        }

        double total = list.Sum(b => b.Thickness), f = total > MaxStack ? MaxStack / total : 1, cum = 0;
        for (int i = 0; i < list.Count; i++)
        {
            var b = list[i];
            double t = b.Thickness * f; cum += t;
            double y = Floor - cum - b.Depth, h = b.Depth + t;
            double left = (Width - b.Width) / 2 + ((i * 37) % 5 - 2) * 5;

            if (_map.TryGetValue(b, out var existing))
            {
                SetZIndex(existing, i); existing.Height = h;
                existing.BeginAnimation(TopProperty, Ease(y, 450));
                existing.BeginAnimation(LeftProperty, Ease(left, 450));
                continue;
            }
            var v = new BookVisualControl { Book = b, Width = b.Width, Height = h, Opacity = 0, RenderTransformOrigin = new Point(0.5, 1) };
            var move = new TranslateTransform(0, -420);
            var rot = new RotateTransform(i % 2 == 0 ? -12 : 10);
            v.RenderTransform = new TransformGroup { Children = { move, rot } };
            SetLeft(v, left); SetTop(v, y); SetZIndex(v, i);
            Children.Add(v); _map[b] = v;

            move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(820))
            { EasingFunction = new BounceEase { Bounces = 2, Bounciness = 5, EasingMode = EasingMode.EaseOut } });
            rot.BeginAnimation(RotateTransform.AngleProperty, Ease((i % 3 - 1) * 1.1, 800));
            v.BeginAnimation(OpacityProperty, Ease(1, 320));
        }
    }
}
