using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using Windows.UI;
using WinRT.Interop;

namespace KeyBoopWin
{
    public sealed partial class OverlayWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const int SW_MAXIMIZE = 3;

        private TaskCompletionSource<bool>? _closeTcs;

        public OverlayWindow()
        {
            this.SystemBackdrop = null;

            this.InitializeComponent();

            var presenter = this.AppWindow.Presenter as OverlappedPresenter;
            if (presenter != null)
            {
                presenter.IsResizable = false;
                presenter.IsAlwaysOnTop = true;
                presenter.SetBorderAndTitleBar(false, false);
            }

            RootGrid.Background = new SolidColorBrush(Colors.Transparent);

            RootGrid.KeyDown += OnKeyDown;
        }

        // ⚡ ГЛАВНЫЙ МЕТОД: показывает окно со скриншотом на фоне и ждет Esc
        public async Task ShowTranslationWithBackgroundAsync(byte[] screenshotBytes, int width, int height)
        {
            _closeTcs = new TaskCompletionSource<bool>();

            // Устанавливаем скриншот как фон
            var bitmap = new BitmapImage();
            using (var stream = new InMemoryRandomAccessStream())
            {
                await stream.WriteAsync(screenshotBytes.AsBuffer());
                stream.Seek(0);
                await bitmap.SetSourceAsync(stream);
            }
            BackgroundImage.Source = bitmap;

            // ⚡ ПЕРЕМЕЩАЕМ ОКНО НА ТОТ МОНИТОР, КОТОРЫЙ ВЫБРАН В НАСТРОЙКАХ
            var settings = SettingsManager.Load();
            var displays = Microsoft.UI.Windowing.DisplayArea.FindAll();
            int targetIndex = settings.ScreenTranslatorMonitorIndex;

            if (displays.Count > 0)
            {
                if (targetIndex < 0 || targetIndex >= displays.Count)
                {
                    targetIndex = 0;
                }

                var targetDisplay = displays[targetIndex];
                var rect = new Windows.Graphics.RectInt32(targetDisplay.WorkArea.X, targetDisplay.WorkArea.Y, targetDisplay.WorkArea.Width, targetDisplay.WorkArea.Height);
                this.AppWindow.MoveAndResize(rect);
            }

            var hwnd = WindowNative.GetWindowHandle(this);

            ShowWindow(hwnd, SW_MAXIMIZE);
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_SHOWWINDOW | SWP_NOMOVE | SWP_NOSIZE);

            this.Activate();

            // Ждем, пока пользователь нажмет Esc
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