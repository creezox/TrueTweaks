using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TrueTweaks.Views
{
    public partial class TweaksView : UserControl
    {
        public TweaksView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            PreviewKeyDown += OnPreviewKeyDown;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.PreviewKeyDown -= Window_PreviewKeyDown;
                window.PreviewKeyDown += Window_PreviewKeyDown;
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!IsVisible) return;
            if (e.Key == Key.K && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                SearchInputBox.Focus();
                SearchInputBox.SelectAll();
                e.Handled = true;
            }
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.K && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                SearchInputBox.Focus();
                SearchInputBox.SelectAll();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && SearchInputBox.IsKeyboardFocused)
            {
                SearchInputBox.Text = string.Empty;
                Keyboard.ClearFocus();
                e.Handled = true;
            }
        }
    }
}
