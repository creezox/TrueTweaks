using System;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace TrueTweaks.Controls
{
    /// <summary>
    /// Responsive UniformGrid that dynamically adjusts column count based on available container width.
    /// Provides smooth responsive Bento Grid layout (1 col on compact, 2-3 on desktop, 3-4 on widescreen).
    /// </summary>
    public class ResponsiveUniformGrid : UniformGrid
    {
        public static readonly DependencyProperty MinItemWidthProperty =
            DependencyProperty.Register(
                nameof(MinItemWidth),
                typeof(double),
                typeof(ResponsiveUniformGrid),
                new FrameworkPropertyMetadata(420.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public double MinItemWidth
        {
            get => (double)GetValue(MinItemWidthProperty);
            set => SetValue(MinItemWidthProperty, value);
        }

        protected override Size MeasureOverride(Size constraint)
        {
            if (!double.IsInfinity(constraint.Width) && constraint.Width > 0)
            {
                int calculatedColumns = (int)Math.Max(1, Math.Floor(constraint.Width / MinItemWidth));
                Columns = calculatedColumns;
            }
            return base.MeasureOverride(constraint);
        }
    }
}
