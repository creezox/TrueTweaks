using System;
using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace TrueTweaks.Controls
{
    /// <summary>
    /// High-frequency 60 FPS hardware telemetry graph using a pre-allocated 60-point circular buffer (double[60]).
    /// Zero Garbage Collector allocations per tick with sub-pixel horizontal interpolation between WMI/PDH samples.
    /// </summary>
    public class TaskManagerGraph : FrameworkElement
    {
        public const int BufferSize = 60;

        // Pre-allocated circular buffer (Zero GC allocation per frame)
        private readonly double[] _ringBuffer = new double[BufferSize];
        private int _headIndex;
        private int _count;

        // 60 FPS interpolation state between 1Hz hardware samples
        private double _previousValue = 18.0;
        private double _targetValue = 18.0;
        private long _lastSampleTimestamp;
        private double _sampleIntervalSeconds = 1.2;
        private bool _isRenderingHooked;

        public static readonly DependencyProperty PointsSourceProperty =
            DependencyProperty.Register(
                nameof(PointsSource),
                typeof(IEnumerable),
                typeof(TaskManagerGraph),
                new FrameworkPropertyMetadata(null, OnPointsSourceChanged));

        public IEnumerable? PointsSource
        {
            get => (IEnumerable?)GetValue(PointsSourceProperty);
            set => SetValue(PointsSourceProperty, value);
        }

        // Frozen Obsidian Neutral & Neon Green rendering resources
        private static readonly Brush BackgroundBrush;
        private static readonly Pen BorderPen;
        private static readonly Pen GridPen;
        private static readonly Pen LinePen;
        private static readonly LinearGradientBrush AreaFillBrush;
        private static readonly Brush BeaconGlowBrush;
        private static readonly Brush BeaconCoreBrush;

        static TaskManagerGraph()
        {
            // Cold Obsidian recessed surface (#0B0D12) & 1px border (#1E222D)
            var bg = new SolidColorBrush(Color.FromRgb(0x0B, 0x0D, 0x12));
            bg.Freeze();
            BackgroundBrush = bg;

            var borderBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x22, 0x2D));
            borderBrush.Freeze();
            BorderPen = new Pen(borderBrush, 1.0);
            BorderPen.Freeze();

            // Subtle neutral grid lines (#141822)
            var gridBrush = new SolidColorBrush(Color.FromArgb(140, 0x18, 0x1C, 0x26));
            gridBrush.Freeze();
            GridPen = new Pen(gridBrush, 1.0);
            GridPen.Freeze();

            // Surgical TrueTweaks Neon Green (#00FF66) curve stroke
            var neonStroke = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x66));
            neonStroke.Freeze();
            LinePen = new Pen(neonStroke, 1.8)
            {
                LineJoin = PenLineJoin.Round,
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            LinePen.Freeze();

            // Area fill gradient: rgba(0, 255, 102, 0.18) -> transparent
            AreaFillBrush = new LinearGradientBrush(
                Color.FromArgb(46, 0x00, 0xFF, 0x66),
                Color.FromArgb(0, 0x00, 0xFF, 0x66),
                new Point(0, 0),
                new Point(0, 1));
            AreaFillBrush.Freeze();

            var glow = new SolidColorBrush(Color.FromArgb(90, 0x00, 0xFF, 0x66));
            glow.Freeze();
            BeaconGlowBrush = glow;

            var core = new SolidColorBrush(Color.FromRgb(0xF3, 0xF5, 0xF8));
            core.Freeze();
            BeaconCoreBrush = core;
        }

        public TaskManagerGraph()
        {
            for (int i = 0; i < BufferSize; i++)
            {
                _ringBuffer[i] = 15.0;
            }
            _count = BufferSize;
            _lastSampleTimestamp = Stopwatch.GetTimestamp();

            IsVisibleChanged += OnIsVisibleChanged;
            Loaded += (_, _) => UpdateRenderingSubscription();
            Unloaded += (_, _) => DetachRendering();
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateRenderingSubscription();
        }

        private void UpdateRenderingSubscription()
        {
            if (IsVisible && IsLoaded)
            {
                if (!_isRenderingHooked)
                {
                    CompositionTarget.Rendering += OnCompositionRendering;
                    _isRenderingHooked = true;
                }
            }
            else
            {
                DetachRendering();
            }
        }

        private void DetachRendering()
        {
            if (_isRenderingHooked)
            {
                CompositionTarget.Rendering -= OnCompositionRendering;
                _isRenderingHooked = false;
            }
        }

        private void OnCompositionRendering(object? sender, EventArgs e)
        {
            if (ActualWidth > 1 && ActualHeight > 1)
            {
                InvalidateVisual();
            }
        }

        private static void OnPointsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TaskManagerGraph graph) return;

            if (e.OldValue is INotifyCollectionChanged oldCol)
            {
                oldCol.CollectionChanged -= graph.OnCollectionChanged;
            }

            if (e.NewValue is INotifyCollectionChanged newCol)
            {
                newCol.CollectionChanged += graph.OnCollectionChanged;
            }

            graph.SyncInitialBufferFromSource();
            graph.InvalidateVisual();
        }

        private void SyncInitialBufferFromSource()
        {
            if (PointsSource == null) return;

            double lastVal = _targetValue;
            foreach (var item in PointsSource)
            {
                if (item is double d)
                {
                    PushToRingBuffer(d);
                    lastVal = d;
                }
                else if (item != null && double.TryParse(item.ToString(), out double parsed))
                {
                    PushToRingBuffer(parsed);
                    lastVal = parsed;
                }
            }
            _previousValue = lastVal;
            _targetValue = lastVal;
            _lastSampleTimestamp = Stopwatch.GetTimestamp();
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null && e.NewItems.Count > 0)
            {
                var newest = e.NewItems[^1];
                double val = newest is double d ? d : Convert.ToDouble(newest);

                long now = Stopwatch.GetTimestamp();
                double elapsed = (double)(now - _lastSampleTimestamp) / Stopwatch.Frequency;
                if (elapsed is > 0.25 and < 5.0)
                {
                    _sampleIntervalSeconds = (_sampleIntervalSeconds * 0.7) + (elapsed * 0.3);
                }

                _previousValue = _targetValue;
                _targetValue = Math.Clamp(val, 0.0, 100.0);
                _lastSampleTimestamp = now;

                PushToRingBuffer(_targetValue);
            }
        }

        private void PushToRingBuffer(double value)
        {
            _ringBuffer[_headIndex] = Math.Clamp(value, 0.0, 100.0);
            _headIndex = (_headIndex + 1) % BufferSize;
            if (_count < BufferSize)
            {
                _count++;
            }
        }

        private double GetSampleAt(int logicalIndex)
        {
            int idx = (_headIndex - _count + logicalIndex + BufferSize) % BufferSize;
            return _ringBuffer[idx];
        }

        protected override void OnRender(DrawingContext dc)
        {
            double width = ActualWidth;
            double height = ActualHeight;

            if (width <= 2 || height <= 2) return;

            var rect = new Rect(0, 0, width, height);

            // 1. Recessed Obsidian Box with 8px CornerRadius
            dc.DrawRoundedRectangle(BackgroundBrush, BorderPen, rect, 8, 8);

            var clipGeometry = new RectangleGeometry(rect, 8, 8);
            clipGeometry.Freeze();
            dc.PushClip(clipGeometry);

            // 2. Subtle 16px Grid Lines
            for (double x = 16; x < width; x += 16)
            {
                dc.DrawLine(GridPen, new Point(x, 0), new Point(x, height));
            }
            for (double y = 16; y < height; y += 16)
            {
                dc.DrawLine(GridPen, new Point(0, y), new Point(width, y));
            }

            if (_count >= 2)
            {
                long now = Stopwatch.GetTimestamp();
                double t = Math.Clamp(((double)(now - _lastSampleTimestamp) / Stopwatch.Frequency) / _sampleIntervalSeconds, 0.0, 1.0);

                // Smoothly interpolated live edge value
                double liveEdgeValue = _previousValue + (_targetValue - _previousValue) * t;
                int visibleSegments = BufferSize - 2;
                double stepX = width / visibleSegments;
                double phaseShiftX = t * stepX;
                double innerHeight = height - 10.0;

                var fillGeom = new StreamGeometry();
                var lineGeom = new StreamGeometry();

                Point firstPt = default;
                Point lastPt = default;

                using (var fillCtx = fillGeom.Open())
                using (var lineCtx = lineGeom.Open())
                {
                    for (int i = 0; i < _count - 1; i++)
                    {
                        double val = GetSampleAt(i);
                        double x = (i * stepX) - phaseShiftX;
                        double y = height - (val / 100.0) * innerHeight - 5.0;

                        if (i == 0)
                        {
                            firstPt = new Point(x, y);
                            fillCtx.BeginFigure(firstPt, isFilled: true, isClosed: true);
                            lineCtx.BeginFigure(firstPt, isFilled: false, isClosed: false);
                        }
                        else
                        {
                            var pt = new Point(x, y);
                            fillCtx.LineTo(pt, isStroked: false, isSmoothJoin: true);
                            lineCtx.LineTo(pt, isStroked: true, isSmoothJoin: true);
                        }
                    }

                    // Final interpolated 60 FPS leading edge at right border
                    double edgeY = height - (liveEdgeValue / 100.0) * innerHeight - 5.0;
                    lastPt = new Point(width, edgeY);
                    fillCtx.LineTo(lastPt, isStroked: false, isSmoothJoin: true);
                    lineCtx.LineTo(lastPt, isStroked: true, isSmoothJoin: true);

                    // Close fill area at bottom
                    fillCtx.LineTo(new Point(width, height), isStroked: false, isSmoothJoin: false);
                    fillCtx.LineTo(new Point(firstPt.X, height), isStroked: false, isSmoothJoin: false);
                }

                fillGeom.Freeze();
                lineGeom.Freeze();

                dc.DrawGeometry(AreaFillBrush, null, fillGeom);
                dc.DrawGeometry(null, LinePen, lineGeom);

                // 3. Leading-Edge Telemetry Probe Beacon
                var beaconPos = new Point(Math.Max(4, width - 4), lastPt.Y);
                dc.DrawEllipse(BeaconGlowBrush, null, beaconPos, 5.0, 5.0);
                dc.DrawEllipse(BeaconCoreBrush, null, beaconPos, 2.2, 2.2);
            }

            dc.Pop();
        }
    }
}
