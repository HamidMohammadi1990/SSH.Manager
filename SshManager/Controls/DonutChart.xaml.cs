using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SshManager.Controls;

public partial class DonutChart : UserControl
{
    public static readonly DependencyProperty SuccessValueProperty =
        DependencyProperty.Register(nameof(SuccessValue), typeof(double), typeof(DonutChart),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender, OnValuesChanged));

    public static readonly DependencyProperty FailedValueProperty =
        DependencyProperty.Register(nameof(FailedValue), typeof(double), typeof(DonutChart),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender, OnValuesChanged));

    public static readonly DependencyProperty SkippedValueProperty =
        DependencyProperty.Register(nameof(SkippedValue), typeof(double), typeof(DonutChart),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender, OnValuesChanged));

    public static readonly DependencyProperty CenterTitleProperty =
        DependencyProperty.Register(nameof(CenterTitle), typeof(string), typeof(DonutChart),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty CenterValueProperty =
        DependencyProperty.Register(nameof(CenterValue), typeof(string), typeof(DonutChart),
            new PropertyMetadata(string.Empty));

    public double SuccessValue
    {
        get => (double)GetValue(SuccessValueProperty);
        set => SetValue(SuccessValueProperty, value);
    }

    public double FailedValue
    {
        get => (double)GetValue(FailedValueProperty);
        set => SetValue(FailedValueProperty, value);
    }

    public double SkippedValue
    {
        get => (double)GetValue(SkippedValueProperty);
        set => SetValue(SkippedValueProperty, value);
    }

    public string CenterTitle
    {
        get => (string)GetValue(CenterTitleProperty);
        set => SetValue(CenterTitleProperty, value);
    }

    public string CenterValue
    {
        get => (string)GetValue(CenterValueProperty);
        set => SetValue(CenterValueProperty, value);
    }

    public DonutChart()
    {
        InitializeComponent();
        Loaded += (_, _) => Redraw();
        SizeChanged += (_, _) => Redraw();
    }

    private static void OnValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DonutChart chart)
            chart.Redraw();
    }

    private void Redraw()
    {
        ChartCanvas.Children.Clear();

        var size = Math.Min(ActualWidth, ActualHeight);
        if (size < 20)
            return;

        var total = SuccessValue + FailedValue + SkippedValue;
        if (total <= 0)
        {
            DrawEmptyRing(size);
            return;
        }

        var cx = size / 2;
        var cy = size / 2;
        var outer = size / 2 - 6;
        var inner = outer * 0.58;
        var start = -90d;

        AddSlice(cx, cy, inner, outer, ref start, SuccessValue / total * 360, GetBrush("SuccessBrush", "#4CAF50"));
        AddSlice(cx, cy, inner, outer, ref start, FailedValue / total * 360, GetBrush("ErrorBrush", "#F44336"));
        AddSlice(cx, cy, inner, outer, ref start, SkippedValue / total * 360, GetBrush("TextSecondaryBrush", "#9E9E9E"));
    }

    private void DrawEmptyRing(double size)
    {
        var cx = size / 2;
        var cy = size / 2;
        var outer = size / 2 - 6;
        var inner = outer * 0.58;
        ChartCanvas.Children.Add(CreateRingSlice(cx, cy, inner, outer, -90, 359.9, GetBrush("BorderBrush", "#3E3E3E")));
    }

    private void AddSlice(double cx, double cy, double inner, double outer, ref double startAngle, double sweep, Brush brush)
    {
        if (sweep <= 0.1)
            return;

        ChartCanvas.Children.Add(CreateRingSlice(cx, cy, inner, outer, startAngle, sweep, brush));
        startAngle += sweep;
    }

    private static Path CreateRingSlice(double cx, double cy, double inner, double outer, double startAngle, double sweep, Brush fill)
    {
        var start = DegreesToPoint(cx, cy, outer, startAngle);
        var end = DegreesToPoint(cx, cy, outer, startAngle + sweep);
        var innerEnd = DegreesToPoint(cx, cy, inner, startAngle + sweep);
        var innerStart = DegreesToPoint(cx, cy, inner, startAngle);
        var largeArc = sweep > 180;

        var figure = new PathFigure { StartPoint = start, IsClosed = true };
        figure.Segments.Add(new ArcSegment(end, new Size(outer, outer), 0, largeArc, SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(innerEnd, true));
        figure.Segments.Add(new ArcSegment(innerStart, new Size(inner, inner), 0, largeArc, SweepDirection.Counterclockwise, true));

        return new Path
        {
            Fill = fill,
            Data = new PathGeometry { Figures = { figure } }
        };
    }

    private static Point DegreesToPoint(double cx, double cy, double radius, double degrees)
    {
        var radians = degrees * Math.PI / 180d;
        return new Point(cx + radius * Math.Cos(radians), cy + radius * Math.Sin(radians));
    }

    private Brush GetBrush(string key, string fallbackHex)
    {
        if (Application.Current.TryFindResource(key) is Brush brush)
            return brush;

        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(fallbackHex)!);
    }
}
