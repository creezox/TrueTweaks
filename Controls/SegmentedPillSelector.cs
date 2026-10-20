using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using TrueTweaks.Helpers;
using TrueTweaks.Models;

namespace TrueTweaks.Controls
{
    /// <summary>
    /// High-precision Segmented Pill Selector with 60 FPS spring-animated sliding cursor
    /// for mutually exclusive registry/kernel presets (e.g., Win32PrioritySeparation, SystemResponsiveness).
    /// </summary>
    [TemplatePart(Name = "PART_TrackGrid", Type = typeof(Grid))]
    [TemplatePart(Name = "PART_Indicator", Type = typeof(Border))]
    [TemplatePart(Name = "PART_OptionsPanel", Type = typeof(UniformGrid))]
    public class SegmentedPillSelector : Control
    {
        private Grid? _trackGrid;
        private Border? _indicator;
        private TranslateTransform? _indicatorTranslate;
        private UniformGrid? _optionsPanel;
        private readonly List<Button> _optionButtons = new();

        private static readonly SolidColorBrush OffIndicatorBg = CreateFrozenBrush(0xFF, 0x1A, 0x1E, 0x29);
        private static readonly SolidColorBrush OffIndicatorBorder = CreateFrozenBrush(0xFF, 0x2B, 0x31, 0x42);

        private static readonly SolidColorBrush ActiveNeonBg = CreateFrozenBrush(0x24, 0x00, 0xFF, 0x66);
        private static readonly SolidColorBrush ActiveNeonBorder = CreateFrozenBrush(0x99, 0x00, 0xFF, 0x66);

        private static readonly SolidColorBrush TextActiveNeon = CreateFrozenBrush(0xFF, 0x00, 0xFF, 0x66);
        private static readonly SolidColorBrush TextActiveNeutral = CreateFrozenBrush(0xFF, 0xF3, 0xF5, 0xF8);
        private static readonly SolidColorBrush TextInactive = CreateFrozenBrush(0xFF, 0x94, 0x9B, 0xA6);

        private static SolidColorBrush CreateFrozenBrush(byte a, byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
            brush.Freeze();
            return brush;
        }

        static SegmentedPillSelector()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(SegmentedPillSelector),
                new FrameworkPropertyMetadata(typeof(SegmentedPillSelector)));
        }

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(
                nameof(ItemsSource),
                typeof(IEnumerable),
                typeof(SegmentedPillSelector),
                new FrameworkPropertyMetadata(null, OnItemsSourceChanged));

        public IEnumerable? ItemsSource
        {
            get => (IEnumerable?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public static readonly DependencyProperty SelectedIndexProperty =
            DependencyProperty.Register(
                nameof(SelectedIndex),
                typeof(int),
                typeof(SegmentedPillSelector),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedIndexChanged));

        public int SelectedIndex
        {
            get => (int)GetValue(SelectedIndexProperty);
            set => SetValue(SelectedIndexProperty, value);
        }

        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register(
                nameof(Command),
                typeof(ICommand),
                typeof(SegmentedPillSelector),
                new PropertyMetadata(null));

        public ICommand? Command
        {
            get => (ICommand?)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public static readonly DependencyProperty CommandParameterProperty =
            DependencyProperty.Register(
                nameof(CommandParameter),
                typeof(object),
                typeof(SegmentedPillSelector),
                new PropertyMetadata(null));

        public object? CommandParameter
        {
            get => GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (_trackGrid != null)
            {
                _trackGrid.SizeChanged -= OnTrackSizeChanged;
            }

            _trackGrid = GetTemplateChild("PART_TrackGrid") as Grid;
            _indicator = GetTemplateChild("PART_Indicator") as Border;
            _optionsPanel = GetTemplateChild("PART_OptionsPanel") as UniformGrid;

            if (_indicator != null)
            {
                _indicatorTranslate = new TranslateTransform(0, 0);
                _indicator.RenderTransform = _indicatorTranslate;
            }

            if (_trackGrid != null)
            {
                _trackGrid.SizeChanged += OnTrackSizeChanged;
            }

            RebuildOptions();
            UpdateSelectionVisuals(animate: false);
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SegmentedPillSelector selector)
            {
                selector.RebuildOptions();
                selector.UpdateSelectionVisuals(animate: false);
            }
        }

        private static void OnSelectedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SegmentedPillSelector selector)
            {
                selector.UpdateSelectionVisuals(animate: true);
            }
        }

        private void OnTrackSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateSelectionVisuals(animate: false);
        }

        private void RebuildOptions()
        {
            if (_optionsPanel == null) return;

            _optionsPanel.Children.Clear();
            _optionButtons.Clear();

            if (ItemsSource == null) return;

            var items = new List<object>();
            foreach (var item in ItemsSource)
            {
                items.Add(item);
            }

            int count = items.Count;
            if (count == 0) return;

            _optionsPanel.Columns = count;
            _optionsPanel.Rows = 1;

            for (int i = 0; i < count; i++)
            {
                int capturedIndex = i;
                var item = items[i];
                string label = item is TweakPresetOption opt ? opt.Label : item.ToString() ?? string.Empty;

                var btn = new Button
                {
                    Content = label,
                    Tag = item,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    Focusable = false,
                    FontFamily = new FontFamily("Cascadia Mono, JetBrains Mono, Consolas, Segoe UI"),
                    FontSize = 11.0,
                    FontWeight = FontWeights.SemiBold,
                    Padding = new Thickness(6, 4, 6, 4),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center
                };

                // Clean template with zero default chrome
                var template = new ControlTemplate(typeof(Button));
                var borderFactory = new FrameworkElementFactory(typeof(Border));
                borderFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
                borderFactory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(PaddingProperty));

                var textFactory = new FrameworkElementFactory(typeof(TextBlock));
                textFactory.SetValue(TextBlock.TextProperty, new TemplateBindingExtension(ContentControl.ContentProperty));
                textFactory.SetValue(TextBlock.ForegroundProperty, new TemplateBindingExtension(ForegroundProperty));
                textFactory.SetValue(TextBlock.FontWeightProperty, new TemplateBindingExtension(FontWeightProperty));
                textFactory.SetValue(TextBlock.FontSizeProperty, new TemplateBindingExtension(FontSizeProperty));
                textFactory.SetValue(TextBlock.FontFamilyProperty, new TemplateBindingExtension(FontFamilyProperty));
                textFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
                textFactory.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
                textFactory.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);

                borderFactory.AppendChild(textFactory);
                template.VisualTree = borderFactory;
                btn.Template = template;

                btn.Click += (_, _) =>
                {
                    if (SelectedIndex != capturedIndex)
                    {
                        SelectedIndex = capturedIndex;
                    }
                    if (Command != null && Command.CanExecute(item))
                    {
                        Command.Execute(item);
                    }
                };

                _optionButtons.Add(btn);
                _optionsPanel.Children.Add(btn);
            }
        }

        private void UpdateSelectionVisuals(bool animate)
        {
            if (_trackGrid == null || _indicator == null || _indicatorTranslate == null) return;

            int count = _optionButtons.Count;
            if (count == 0)
            {
                _indicator.Visibility = Visibility.Collapsed;
                return;
            }

            _indicator.Visibility = Visibility.Visible;
            int idx = Math.Clamp(SelectedIndex, 0, count - 1);

            double totalWidth = _trackGrid.ActualWidth;
            if (totalWidth <= 1) return;

            double segmentWidth = totalWidth / count;
            double targetX = idx * segmentWidth;

            _indicator.Width = Math.Max(0, segmentWidth);

            // Style indicator based on Default (0) vs Active Optimization (> 0)
            if (idx == 0)
            {
                _indicator.Background = OffIndicatorBg;
                _indicator.BorderBrush = OffIndicatorBorder;
            }
            else
            {
                _indicator.Background = ActiveNeonBg;
                _indicator.BorderBrush = ActiveNeonBorder;
            }

            if (animate)
            {
                CompositionMotion.AnimateDoubleWithSpring(_indicatorTranslate, TranslateTransform.XProperty, targetX, 240);
            }
            else
            {
                _indicatorTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                _indicatorTranslate.X = targetX;
            }

            for (int i = 0; i < count; i++)
            {
                var btn = _optionButtons[i];
                if (i == idx)
                {
                    btn.Foreground = idx > 0 ? TextActiveNeon : TextActiveNeutral;
                    btn.FontWeight = FontWeights.Bold;
                }
                else
                {
                    btn.Foreground = TextInactive;
                    btn.FontWeight = FontWeights.Medium;
                }
            }
        }
    }
}
