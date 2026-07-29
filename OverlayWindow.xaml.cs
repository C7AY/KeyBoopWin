using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinRT.Interop;

namespace KeyBoopWin
{
    public sealed partial class OverlayWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOACTIVATE = 0x0010;

        private TaskCompletionSource<bool>? _closeTcs;

        public OverlayWindow()
        {
            this.InitializeComponent();

            var presenter = this.AppWindow.Presenter as OverlappedPresenter;
            presenter?.SetBorderAndTitleBar(false, false);

            // ⚡ Делаем фон ПОЛНОСТЬЮ прозрачным
            RootGrid.Background = new SolidColorBrush(Colors.Transparent);

            RootGrid.KeyDown += OnKeyDown;
        }

        public async Task ShowAndHideAsync()
        {
            _closeTcs = new TaskCompletionSource<bool>();

            var displayArea = DisplayArea.Primary;

            // ⚡ Сначала устанавливаем размер
            var newSize = new Windows.Graphics.SizeInt32((int)displayArea.OuterBounds.Width, (int)displayArea.OuterBounds.Height);
            var newPosition = new Windows.Graphics.PointInt32((int)displayArea.OuterBounds.X, (int)displayArea.OuterBounds.Y);

            this.AppWindow.Resize(newSize);
            this.AppWindow.Move(newPosition);

            // ⚡ Показываем окно поверх всех
            var hwnd = WindowNative.GetWindowHandle(this);
            SetWindowPos(hwnd, HWND_TOPMOST,
                (int)displayArea.OuterBounds.X,
                (int)displayArea.OuterBounds.Y,
                (int)displayArea.OuterBounds.Width,
                (int)displayArea.OuterBounds.Height,
                SWP_SHOWWINDOW | SWP_NOACTIVATE);

            this.Activate();

            await _closeTcs.Task;

            this.AppWindow.Hide();
            ClearBoxes();
        }

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                _closeTcs?.TrySetResult(true);
                e.Handled = true;
            }
        }

        public void ClearBoxes()
        {
            if (RootCanvas != null)
            {
                RootCanvas.Children.Clear();
            }
        }

        public void AddTranslationBox(double x, double y, string text, string bgColor, string textColor, double fontSize)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(ColorFromString(bgColor)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 3, 6, 3),
                Child = new TextBlock
                {
                    Text = text,
                    Foreground = new SolidColorBrush(ColorFromString(textColor)),
                    FontSize = fontSize,
                    TextWrapping = TextWrapping.Wrap
                }
            };

            Canvas.SetLeft(border, x);
            Canvas.SetTop(border, y);
            RootCanvas.Children.Add(border);
        }

        private Windows.UI.Color ColorFromString(string hexColor)
        {
            hexColor = hexColor.TrimStart('#');
            if (hexColor.Length == 6) hexColor = "FF" + hexColor;

            byte a = byte.Parse(hexColor.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte r = byte.Parse(hexColor.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hexColor.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hexColor.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);

            return Windows.UI.Color.FromArgb(a, r, g, b);
        }
    }
}