using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using TrueTweaks.ViewModels;

namespace TrueTweaks
{
    public partial class MainWindow : Window
    {
        private bool _isClosing;

        public MainWindow()
        {
            InitializeComponent();
            Opacity = 0;
            Loaded += MainWindow_Loaded;
            DataContextChanged += MainWindow_DataContextChanged;
            Closing += MainWindow_Closing;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
                var easeInOut = new CubicEase { EasingMode = EasingMode.EaseInOut };

                // 1. Fade in window smoothly
                BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = easeOut });

                // 2. Animate Radial Glow Aura
                SplashGlowAura.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(500)) { EasingFunction = easeOut });
                SplashGlowScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, new DoubleAnimation(0.75, 1.12, TimeSpan.FromMilliseconds(1150)) { EasingFunction = easeOut });
                SplashGlowScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, new DoubleAnimation(0.75, 1.12, TimeSpan.FromMilliseconds(1150)) { EasingFunction = easeOut });

                // 3. Animate Logo Pop-In & Center Stack Slide-Up
                SplashContentScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, new DoubleAnimation(0.92, 1.0, TimeSpan.FromMilliseconds(520)) { EasingFunction = easeOut });
                SplashContentScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, new DoubleAnimation(0.92, 1.0, TimeSpan.FromMilliseconds(520)) { EasingFunction = easeOut });
                SplashContentTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(520)) { EasingFunction = easeOut });

                SplashLogoBox.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(420)) { EasingFunction = easeOut });
                SplashLogoScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, new DoubleAnimation(0.72, 1.0, TimeSpan.FromMilliseconds(480)) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 } });
                SplashLogoScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, new DoubleAnimation(0.72, 1.0, TimeSpan.FromMilliseconds(480)) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 } });
                SplashLogoRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, new DoubleAnimation(-60, 0, TimeSpan.FromMilliseconds(520)) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 } });

                // 4. Animate Brand Title
                var titleFade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(420)) { BeginTime = TimeSpan.FromMilliseconds(110), EasingFunction = easeOut };
                var titleSlide = new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(420)) { BeginTime = TimeSpan.FromMilliseconds(110), EasingFunction = easeOut };
                SplashBrandTitle.BeginAnimation(OpacityProperty, titleFade);
                SplashTitleTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, titleSlide);

                // 5. Sweep minimal progress bar from 0 to 148px
                var progressAnim = new DoubleAnimation(0, 148, TimeSpan.FromMilliseconds(980))
                {
                    BeginTime = TimeSpan.FromMilliseconds(140),
                    EasingFunction = easeInOut
                };
                SplashProgressFill.BeginAnimation(WidthProperty, progressAnim);

                await Task.Delay(1220);

                if (_isClosing) return;

                // 6. Smoothly dissolve Splash Overlay with subtle scale zoom
                var exitFade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(360)) { EasingFunction = easeOut };
                SplashContentScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, 1.05, TimeSpan.FromMilliseconds(360)) { EasingFunction = easeOut });
                SplashContentScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, new DoubleAnimation(1.0, 1.05, TimeSpan.FromMilliseconds(360)) { EasingFunction = easeOut });

                exitFade.Completed += (_, _) =>
                {
                    StartupSplashOverlay.Visibility = Visibility.Collapsed;
                    StartupSplashOverlay.IsHitTestVisible = false;
                };
                StartupSplashOverlay.BeginAnimation(OpacityProperty, exitFade);
            }
            catch
            {
                StartupSplashOverlay.Visibility = Visibility.Collapsed;
                Opacity = 1;
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _isClosing = true;
            if (DataContext is MainViewModel vm)
            {
                vm.Logger.Logs.CollectionChanged -= Logs_CollectionChanged;
            }
        }

        private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is MainViewModel oldVm)
            {
                oldVm.Logger.Logs.CollectionChanged -= Logs_CollectionChanged;
            }

            if (e.NewValue is MainViewModel vm)
            {
                vm.Logger.Logs.CollectionChanged += Logs_CollectionChanged;
            }
        }

        private void Logs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
        }
    }
}