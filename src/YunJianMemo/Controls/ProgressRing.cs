using System.Windows;
using System.Windows.Media;

namespace YunJianMemo.Controls;

public sealed class ProgressRing : FrameworkElement
{
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress), typeof(double), typeof(ProgressRing),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness), typeof(double), typeof(ProgressRing),
        new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush), typeof(Brush), typeof(ProgressRing),
        new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
            FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AccentBrushProperty = DependencyProperty.Register(
        nameof(AccentBrush), typeof(Brush), typeof(ProgressRing),
        new FrameworkPropertyMetadata(Brushes.MediumAquamarine, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public Brush TrackBrush
    {
        get => (Brush)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public Brush AccentBrush
    {
        get => (Brush)GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 2) return;

        var thickness = Math.Max(1, StrokeThickness);
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var radius = (size - thickness) / 2 - 6;
        if (radius <= 0) return;

        var trackPen = new Pen(TrackBrush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawEllipse(null, trackPen, center, radius, radius);

        var progress = Math.Clamp(Progress, 0, 1);
        if (progress <= 0.0005) return;

        var glowPen = new Pen(AccentBrush, thickness + 9)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        dc.PushOpacity(0.14);
        DrawArc(dc, center, radius, progress, glowPen);
        dc.Pop();

        var pen = new Pen(AccentBrush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        DrawArc(dc, center, radius, progress, pen);

        var angle = -Math.PI / 2 + progress * Math.PI * 2;
        var dot = new Point(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle));
        var dotBrush = AccentBrush;
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(55, 255, 255, 255)), null, dot, thickness * 0.62, thickness * 0.62);
        dc.DrawEllipse(dotBrush, null, dot, thickness * 0.36, thickness * 0.36);
    }

    private static void DrawArc(DrawingContext dc, Point center, double radius, double progress, Pen pen)
    {
        if (progress >= 0.9995)
        {
            dc.DrawEllipse(null, pen, center, radius, radius);
            return;
        }

        var startAngle = -Math.PI / 2;
        var endAngle = startAngle + progress * Math.PI * 2;
        var start = new Point(center.X + radius * Math.Cos(startAngle), center.Y + radius * Math.Sin(startAngle));
        var end = new Point(center.X + radius * Math.Cos(endAngle), center.Y + radius * Math.Sin(endAngle));

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(start, false, false);
            context.ArcTo(end, new Size(radius, radius), 0, progress > 0.5,
                SweepDirection.Clockwise, true, false);
        }
        geometry.Freeze();
        dc.DrawGeometry(null, pen, geometry);
    }
}
