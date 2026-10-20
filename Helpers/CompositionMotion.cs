using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TrueTweaks.Helpers
{
    /// <summary>
    /// Physics-based natural spring easing function equivalent to WinUI 3 SpringVector3NaturalMotionAnimation.
    /// Models an under-damped harmonic oscillator: f(t) = 1 - e^(-damping * t) * cos(frequency * t).
    /// </summary>
    public class NaturalSpringEase : EasingFunctionBase
    {
        public double Damping { get; set; } = 7.5;
        public double Frequency { get; set; } = 11.0;

        public NaturalSpringEase()
        {
            EasingMode = EasingMode.EaseOut;
        }

        protected override double EaseInCore(double normalizedTime)
        {
            // Invert so EaseOut produces natural spring settle
            double t = 1.0 - normalizedTime;
            double envelope = Math.Exp(-Damping * t);
            double oscillation = Math.Cos(Frequency * t);
            return 1.0 - (1.0 - envelope * oscillation);
        }

        protected override Freezable CreateInstanceCore() => new NaturalSpringEase
        {
            Damping = Damping,
            Frequency = Frequency,
            EasingMode = EasingMode
        };
    }

    /// <summary>
    /// GPU-accelerated 60 FPS micro-interaction engine for TrueTweaks v2.0.0.
    /// Provides attached behaviors for Bento Card hover elevation, Specular Border lighting,
    /// Page Navigation transitions, and Spring physics tactile controls.
    /// </summary>
    public static class CompositionMotion
    {
        // Frozen brushes for zero-allocation hover transitions (Obsidian Neutral v2.0.0)
        public static readonly SolidColorBrush CardRestBackgroundBrush;
        public static readonly SolidColorBrush CardHoverBackgroundBrush;
        public static readonly SolidColorBrush CardRestBorderBrush;
        public static readonly LinearGradientBrush CardHoverSpecularBorderBrush;

        private static readonly KeySpline CyberPrecisionSpline = new(0.16, 1.0, 0.3, 1.0);
        private static readonly Duration HoverDuration = new(TimeSpan.FromMilliseconds(160));
        private static readonly Duration PageTransitionDuration = new(TimeSpan.FromMilliseconds(180));

        static CompositionMotion()
        {
            CardRestBackgroundBrush = new SolidColorBrush(Color.FromRgb(0x10, 0x12, 0x18));
            CardRestBackgroundBrush.Freeze();

            CardHoverBackgroundBrush = new SolidColorBrush(Color.FromRgb(0x15, 0x18, 0x21));
            CardHoverBackgroundBrush.Freeze();

            CardRestBorderBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x22, 0x2D));
            CardRestBorderBrush.Freeze();

            CardHoverSpecularBorderBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            CardHoverSpecularBorderBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0x2E, 0x34, 0x46), 0.0));
            CardHoverSpecularBorderBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0x18, 0x1B, 0x24), 1.0));
            CardHoverSpecularBorderBrush.Freeze();
        }

        // =========================================================================
        // 1. BENTO CARD HOVER: -2px Y Elevation + Specular Top Border (160ms)
        // =========================================================================
        public static readonly DependencyProperty EnableCardHoverProperty =
            DependencyProperty.RegisterAttached(
                "EnableCardHover",
                typeof(bool),
                typeof(CompositionMotion),
                new PropertyMetadata(false, OnEnableCardHoverChanged));

        public static bool GetEnableCardHover(DependencyObject obj) => (bool)obj.GetValue(EnableCardHoverProperty);
        public static void SetEnableCardHover(DependencyObject obj, bool value) => obj.SetValue(EnableCardHoverProperty, value);

        private static void OnEnableCardHoverChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FrameworkElement element) return;

            if ((bool)e.NewValue)
            {
                EnsureTranslateTransform(element);
                element.MouseEnter += OnCardMouseEnter;
                element.MouseLeave += OnCardMouseLeave;
            }
            else
            {
                element.MouseEnter -= OnCardMouseEnter;
                element.MouseLeave -= OnCardMouseLeave;
            }
        }

        private static void OnCardMouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is not FrameworkElement element) return;

            var translate = EnsureTranslateTransform(element);
            AnimateDoubleWithSpline(translate, TranslateTransform.YProperty, -2.0, HoverDuration);

            if (element is Border border)
            {
                border.Background = CardHoverBackgroundBrush;
                border.BorderBrush = CardHoverSpecularBorderBrush;
            }
        }

        private static void OnCardMouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is not FrameworkElement element) return;

            var translate = EnsureTranslateTransform(element);
            AnimateDoubleWithSpline(translate, TranslateTransform.YProperty, 0.0, HoverDuration);

            if (element is Border border)
            {
                border.Background = CardRestBackgroundBrush;
                border.BorderBrush = CardRestBorderBrush;
            }
        }

        // =========================================================================
        // 2. PAGE NAVIGATION TRANSITION: Opacity (0->1) + Y Slide (12px->0) (180ms)
        // =========================================================================
        public static readonly DependencyProperty EnablePageTransitionProperty =
            DependencyProperty.RegisterAttached(
                "EnablePageTransition",
                typeof(bool),
                typeof(CompositionMotion),
                new PropertyMetadata(false, OnEnablePageTransitionChanged));

        public static bool GetEnablePageTransition(DependencyObject obj) => (bool)obj.GetValue(EnablePageTransitionProperty);
        public static void SetEnablePageTransition(DependencyObject obj, bool value) => obj.SetValue(EnablePageTransitionProperty, value);

        private static void OnEnablePageTransitionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FrameworkElement element) return;

            if ((bool)e.NewValue)
            {
                element.Loaded += OnPageLoaded;
            }
            else
            {
                element.Loaded -= OnPageLoaded;
            }
        }

        private static void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element) return;

            var translate = EnsureTranslateTransform(element);

            var opacityAnim = new DoubleAnimationUsingKeyFrames { Duration = PageTransitionDuration };
            opacityAnim.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            opacityAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(PageTransitionDuration.TimeSpan), CyberPrecisionSpline));

            var slideAnim = new DoubleAnimationUsingKeyFrames { Duration = PageTransitionDuration };
            slideAnim.KeyFrames.Add(new DiscreteDoubleKeyFrame(12.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            slideAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(PageTransitionDuration.TimeSpan), CyberPrecisionSpline));

            element.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            translate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
        }

        // =========================================================================
        // 3. CLICK SCALE MICRO-INTERACTION (Spring Physics on CTA Buttons)
        // =========================================================================
        public static readonly DependencyProperty EnableClickScaleProperty =
            DependencyProperty.RegisterAttached(
                "EnableClickScale",
                typeof(bool),
                typeof(CompositionMotion),
                new PropertyMetadata(false, OnEnableClickScaleChanged));

        public static bool GetEnableClickScale(DependencyObject obj) => (bool)obj.GetValue(EnableClickScaleProperty);
        public static void SetEnableClickScale(DependencyObject obj, bool value) => obj.SetValue(EnableClickScaleProperty, value);

        private static void OnEnableClickScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not UIElement element) return;

            if ((bool)e.NewValue)
            {
                EnsureScaleTransform(element);
                element.PreviewMouseLeftButtonDown += OnElementMouseDown;
                element.PreviewMouseLeftButtonUp += OnElementMouseUp;
                element.MouseLeave += OnElementMouseLeave;
            }
            else
            {
                element.PreviewMouseLeftButtonDown -= OnElementMouseDown;
                element.PreviewMouseLeftButtonUp -= OnElementMouseUp;
                element.MouseLeave -= OnElementMouseLeave;
            }
        }

        private static void OnElementMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not UIElement element) return;
            var scale = EnsureScaleTransform(element);
            var downAnim = new DoubleAnimation(0.965, TimeSpan.FromMilliseconds(75))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, downAnim);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, downAnim);
        }

        private static void OnElementMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not UIElement element) return;
            RestoreSpringScale(element);
        }

        private static void OnElementMouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is not UIElement element) return;
            RestoreSpringScale(element);
        }

        private static void RestoreSpringScale(UIElement element)
        {
            var scale = EnsureScaleTransform(element);
            var springUp = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new NaturalSpringEase { Damping = 6.5, Frequency = 12.0, EasingMode = EasingMode.EaseOut }
            };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, springUp);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, springUp);
        }

        // =========================================================================
        // 4. HELPER UTILITIES FOR SPRING / SPLINE MOTION
        // =========================================================================
        public static void AnimateDoubleWithSpline(Animatable target, DependencyProperty dp, double toValue, Duration duration)
        {
            var anim = new DoubleAnimationUsingKeyFrames { Duration = duration };
            anim.KeyFrames.Add(new SplineDoubleKeyFrame(toValue, KeyTime.FromTimeSpan(duration.TimeSpan), CyberPrecisionSpline));
            target.BeginAnimation(dp, anim);
        }

        public static void AnimateDoubleWithSpring(Animatable target, DependencyProperty dp, double toValue, int durationMs = 220)
        {
            var anim = new DoubleAnimation(toValue, TimeSpan.FromMilliseconds(durationMs))
            {
                EasingFunction = new NaturalSpringEase { Damping = 7.5, Frequency = 10.5, EasingMode = EasingMode.EaseOut }
            };
            target.BeginAnimation(dp, anim);
        }

        private static TranslateTransform EnsureTranslateTransform(UIElement element)
        {
            if (element.RenderTransform is TranslateTransform tt)
                return tt;

            if (element.RenderTransform is TransformGroup group)
            {
                foreach (var child in group.Children)
                {
                    if (child is TranslateTransform existing)
                        return existing;
                }
                var added = new TranslateTransform(0, 0);
                group.Children.Add(added);
                return added;
            }

            var newTransform = new TranslateTransform(0, 0);
            element.RenderTransform = newTransform;
            return newTransform;
        }

        private static ScaleTransform EnsureScaleTransform(UIElement element)
        {
            element.RenderTransformOrigin = new Point(0.5, 0.5);

            if (element.RenderTransform is ScaleTransform st)
                return st;

            if (element.RenderTransform is TransformGroup group)
            {
                foreach (var child in group.Children)
                {
                    if (child is ScaleTransform existing)
                        return existing;
                }
                var added = new ScaleTransform(1.0, 1.0);
                group.Children.Add(added);
                return added;
            }

            var newGroup = new TransformGroup();
            if (element.RenderTransform != null && element.RenderTransform != Transform.Identity)
            {
                newGroup.Children.Add(element.RenderTransform);
            }
            var newScale = new ScaleTransform(1.0, 1.0);
            newGroup.Children.Add(newScale);
            element.RenderTransform = newGroup;
            return newScale;
        }
    }
}
