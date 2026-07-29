using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
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
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOACTIVATE = 0x0010;

        public AreaSelectorWindow()
        {
            this.InitializeComponent();

            var presenter = this.AppWindow.Presenter as OverlappedPresenter;
            if (presenter != null)
            {
                presenter.SetBorderAndTitleBar(false, false);
            }

            // ⚡ Делаем окно прозрачным
            RootGrid.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

            RootGrid.PointerPressed += OnPointerPressed;
            RootGrid.PointerMoved += OnPointerMoved;
            RootGrid.PointerReleased += OnPointerReleased;
            RootGrid.KeyDown += OnKeyDown;
        }

        public async Task<Windows.Foundation.Rect?> ShowSelectionWithScreenshotAsync(byte[] screenshotBytes, int width, int height)
        {
            var bitmap = new BitmapImage();
            using (var stream = new InMemoryRandomAccessStream())
            {
                await stream.WriteAsync(screenshotBytes.AsBuffer());
                stream.Seek(0);
                await bitmap.SetSourceAsync(stream);
            }
            BackgroundImage.Source = bitmap;

            //  Получаем размеры ВСЕГО экрана (всех мониторов)
            var displayArea = DisplayArea.Primary;

            // ⚡ Сначала устанавливаем размер и позицию, ПОТОМ показываем окно
            var newSize = new Windows.Graphics.SizeInt32((int)displayArea.OuterBounds.Width, (int)displayArea.OuterBounds.Height);
            var newPosition = new Windows.Graphics.PointInt32((int)displayArea.OuterBounds.X, (int)displayArea.OuterBounds.Y);

            this.AppWindow.Resize(newSize);
            this.AppWindow.Move(newPosition);

            // ⚡ Делаем окно поверх всех и показываем
            var hwnd = WindowNative.GetWindowHandle(this);
            SetWindowPos(hwnd, HWND_TOPMOST,
                (int)displayArea.OuterBounds.X,
                (int)displayArea.OuterBounds.Y,
                (int)displayArea.OuterBounds.Width,
                (int)displayArea.OuterBounds.Height,
                SWP_SHOWWINDOW | SWP_NOACTIVATE);

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