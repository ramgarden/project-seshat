using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using ProjectSeshat.App.ViewModels;

namespace ProjectSeshat.App.Controls;

/// <summary>
/// Renders a rotatable, zoomable 3D galactic sky-map from a set of <see cref="SkyPoint"/>.
/// Drag to rotate, scroll to zoom. Data changes invalidate the render automatically.
/// </summary>
public sealed class AtlasSkyMapControl : Control
{
    public static readonly StyledProperty<IReadOnlyList<SkyPoint>?> PointsProperty =
        AvaloniaProperty.Register<AtlasSkyMapControl, IReadOnlyList<SkyPoint>?>(nameof(Points));

    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.FromRgb(6, 13, 22));
    private static readonly IBrush GridBrush = new SolidColorBrush(Color.FromArgb(40, 90, 140, 170));
    private static readonly IBrush SystemBrush = new SolidColorBrush(Color.FromRgb(54, 197, 240));
    private static readonly IBrush RegionBrush = new SolidColorBrush(Color.FromRgb(127, 207, 176));
    private static readonly IBrush CurrentBrush = new SolidColorBrush(Color.FromRgb(255, 210, 77));
    private static readonly IBrush NextBrush = new SolidColorBrush(Color.FromRgb(255, 90, 90));

    private double _rotationX = -0.45;
    private double _rotationY = 0.6;
    private double _scale = 1.0;
    private bool _dragging;
    private Point _lastPointer;

    public AtlasSkyMapControl()
    {
        ClipToBounds = true;
    }

    public IReadOnlyList<SkyPoint>? Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    static AtlasSkyMapControl()
    {
        AffectsRender<AtlasSkyMapControl>(PointsProperty);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _lastPointer = e.GetPosition(this);
        _dragging = true;
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging)
        {
            return;
        }

        var position = e.GetPosition(this);
        var dx = position.X - _lastPointer.X;
        var dy = position.Y - _lastPointer.Y;
        _lastPointer = position;

        _rotationY += dx * 0.008;
        _rotationX += dy * 0.008;
        _rotationX = Math.Clamp(_rotationX, -Math.PI / 2, Math.PI / 2);
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        _scale *= e.Delta.Y > 0 ? 1.15 : 1 / 1.15;
        _scale = Math.Clamp(_scale, 0.3, 10);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds.Size;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        context.FillRectangle(BackgroundBrush, new Rect(bounds));

        var points = Points;
        if (points is null || points.Count == 0)
        {
            DrawEmptyHint(context, bounds);
            return;
        }

        var center = new Point(bounds.Width / 2, bounds.Height / 2);
        var maxExtent = points.Max(p => Math.Max(Math.Abs(p.X), Math.Max(Math.Abs(p.Y), Math.Abs(p.Z))));
        maxExtent = Math.Max(1, maxExtent);
        var scale = Math.Min(bounds.Width, bounds.Height) * 0.82 / 2 / maxExtent * _scale;

        var cosX = Math.Cos(_rotationX);
        var sinX = Math.Sin(_rotationX);
        var cosY = Math.Cos(_rotationY);
        var sinY = Math.Sin(_rotationY);

        // Project everything once, then draw far-to-near for a depth cue.
        var projected = new List<(SkyPoint Point, double X, double Y, double Z)>(points.Count);
        foreach (var point in points)
        {
            var rotatedY = point.X * cosY - point.Z * sinY;
            var rotatedZ2 = point.X * sinY + point.Z * cosY;
            var rotated = point.Y * cosX - rotatedZ2 * sinX;
            var rotatedZ = point.Y * sinX + rotatedZ2 * cosX;

            var x = center.X + rotatedY * scale;
            var y = center.Y - rotated * scale;
            projected.Add((point, x, y, rotatedZ));
        }

        projected.Sort((a, b) => a.Z.CompareTo(b.Z));

        DrawAxes(context, center, scale);

        foreach (var (point, x, y, z) in projected)
        {
            var nearness = Math.Clamp((maxExtent + z) / (2 * maxExtent), 0.0, 1.0);
            DrawPoint(context, point, new Point(x, y), nearness, center);
        }
    }

    private static void DrawEmptyHint(DrawingContext context, Size bounds)
    {
        var brush = new SolidColorBrush(Color.FromRgb(131, 184, 213));
        var typeface = new Typeface("Segoe UI", weight: FontWeight.Normal);
        var formatted = new FormattedText(
            "Import journal files to build the galactic sky-map.",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface,
            14,
            brush);
        context.DrawText(formatted, new Point((bounds.Width - formatted.Width) / 2, (bounds.Height - formatted.Height) / 2));
    }

    private static void DrawAxes(DrawingContext context, Point center, double scale)
    {
        var pen = new Pen(GridBrush, 1);
        var extent = scale * 1.2;
        context.DrawLine(pen, new Point(center.X - extent, center.Y), new Point(center.X + extent, center.Y));
        context.DrawLine(pen, new Point(center.X, center.Y - extent), new Point(center.X, center.Y + extent));
    }

    private static void DrawPoint(DrawingContext context, SkyPoint point, Point position, double nearness, Point center)
    {
        double baseRadius;
        IBrush fill;
        switch (point.Kind)
        {
            case SkyPointKind.Region:
                fill = RegionBrush;
                baseRadius = 7;
                break;
            case SkyPointKind.Current:
                fill = CurrentBrush;
                baseRadius = 4.5;
                break;
            case SkyPointKind.Next:
                fill = NextBrush;
                baseRadius = 5;
                break;
            default:
                fill = SystemBrush;
                baseRadius = 2.2;
                break;
        }

        var opacity = 0.35 + 0.65 * nearness;
        var radius = baseRadius * (0.6 + 0.9 * nearness) * point.Intensity;
        var brush = new SolidColorBrush((fill as SolidColorBrush)!.Color, opacity);

        // Soft glow behind small points so they read on dark backgrounds.
        context.DrawEllipse(brush, null, position, radius * 2.6, radius * 2.6);
        context.DrawEllipse(brush, null, position, radius, radius);

        if (point.Kind is SkyPointKind.Current or SkyPointKind.Next)
        {
            var ring = new Pen(fill, 1.2);
            context.DrawEllipse(null, ring, position, radius + 3, radius + 3);
            context.DrawLine(new Pen(fill, 1), position, center);
        }
    }
}
