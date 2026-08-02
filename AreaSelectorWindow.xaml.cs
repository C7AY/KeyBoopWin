using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.Storage.Streams;
using WinRT.Interop;

namespace KeyBoopWin
{
    public sealed partial class AreaSelectorWindow : Window
    {
        private bool _isSelecting = false;
        private Point _startPoint;
        private Point _endPoint;
        private readonly TaskCompletionSource<Windows.Foundation.Rect?> _selectionTask = new();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const int SW_MAXIMIZE = 3;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TOPMOST = 0x00000008;

        //  Сохраняем скриншот как свойство
        public byte[]? ScreenshotBytes { get; private set; }
        public int ScreenshotWidth { get; private set; }
        public int ScreenshotHeight { get; private set; }

        public AreaSelectorWindow()
        {
            this.SystemBackdrop = null;

            this.InitializeComponent();

            var presenter = this.AppWindow.Presenter as OverlappedPresenter;
            if (presenter != null)
            {
                presenter.SetBorderAndTitleBar(false, false);
            }

            RootGrid.Background = new SolidColorBrush(Colors.Transparent);

            RootGrid.PointerPressed += OnPointerPressed;
            RootGrid.PointerMoved += OnPointerMoved;
            RootGrid.PointerReleased += OnPointerReleased;
            RootGrid.KeyDown += OnKeyDown;
        }

        public async Task<Windows.Foundation.Rect?> ShowSelectionWithScreenshotAsync(byte[] screenshotBytes, int width, int height)
        {
            // Сохраняем скриншот
            ScreenshotBytes = screenshotBytes;
            ScreenshotWidth = width;
            ScreenshotHeight = height;

            var bitmap = new BitmapImage();
            using (var stream = new InMemoryRandomAccessStream())
            {
                await stream.WriteAsync(screenshotBytes.AsBuffer());
                stream.Seek(0);
                await bitmap.SetSourceAsync(stream);
            }
            BackgroundImage.Source = bitmap;

            // ⚡ ПРИНУДИТЕЛЬНО ПЕРЕМЕЩАЕМ ОКНО НА ОСНОВНОЙ МОНИТОР (координаты 0,0)
            var displays = Microsoft.UI.Windowing.DisplayArea.FindAll();
            if (displays.Count > 0)
            {
                var mainDisplay = displays[0]; // Всегда берем первый (основной) монитор
                var rect = new Windows.Graphics.RectInt32(mainDisplay.WorkArea.X, mainDisplay.WorkArea.Y, mainDisplay.WorkArea.Width, mainDisplay.WorkArea.Height);
                this.AppWindow.MoveAndResize(rect);
            }

            var hwnd = WindowNative.GetWindowHandle(this);

            ShowWindow(hwnd, SW_MAXIMIZE);

            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED | WS_EX_TOPMOST);

            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_SHOWWINDOW | SWP_NOMOVE | SWP_NOSIZE);

            this.Activate();

            var result = await _selectionTask.Task;
            this.Close();
            return result;
        }

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                _selectionTask.TrySetResult(null);
                this.Close();
                e.Handled = true;
            }
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _isSelecting = true;
            _startPoint = e.GetCurrentPoint(RootGrid).Position;

            SelectionRect.Visibility = Visibility.Visible;
            Canvas.SetLeft(SelectionRect, _startPoint.X);
            Canvas.SetTop(SelectionRect, _startPoint.Y);
            SelectionRect.Width = 0;
            SelectionRect.Height = 0;
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isSelecting) return;

            _endPoint = e.GetCurrentPoint(RootGrid).Position;

            double x = Math.Min(_startPoint.X, _endPoint.X);
            double y = Math.Min(_startPoint.Y, _endPoint.Y);
            double width = Math.Abs(_endPoint.X - _startPoint.X);
            double height = Math.Abs(_endPoint.Y - _startPoint.Y);

            Canvas.SetLeft(SelectionRect, x);
            Canvas.SetTop(SelectionRect, y);
            SelectionRect.Width = width;
            SelectionRect.Height = height;
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_isSelecting) return;
            _isSelecting = false;

            _endPoint = e.GetCurrentPoint(RootGrid).Position;

            double x = Math.Min(_startPoint.X, _endPoint.X);
            double y = Math.Min(_startPoint.Y, _endPoint.Y);
            double width = Math.Abs(_endPoint.X - _startPoint.X);
            double height = Math.Abs(_endPoint.Y - _startPoint.Y);

            if (width < 50 || height < 50)
            {
                _selectionTask.TrySetResult(null);
                return;
            }

            var rect = new Windows.Foundation.Rect(x, y, width, height);
            _selectionTask.TrySetResult(rect);
        }
    }
}