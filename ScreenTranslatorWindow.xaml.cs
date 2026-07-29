using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace KeyBoopWin
{
    public sealed partial class ScreenTranslatorWindow : Window
    {
        private HttpClient _httpClient;
        public string _overlayBgColor = "#CCFFFFFF";
        public string _overlayTextColor = "#000000";
        public double _overlayFontSize = 16;

        private OverlayWindow _overlayWindow;
        private ScreenCaptureAndTranslate _capturer;

        public ScreenTranslatorWindow()
        {
            this.InitializeComponent();
            this.AppWindow.Resize(new Windows.Graphics.SizeInt32(450, 400));
            CenterWindowOnScreen();

            _httpClient = new HttpClient();
            this.Closed += ScreenTranslatorWindow_Closed;

            _overlayWindow = new OverlayWindow();
            _capturer = new ScreenCaptureAndTranslate(this, _overlayWindow);
        }

        private void ScreenTranslatorWindow_Closed(object sender, WindowEventArgs args)
        {
            _httpClient?.Dispose();
            _overlayWindow?.Close();
        }

        private void CenterWindowOnScreen()
        {
            var displayArea = DisplayArea.GetFromWindowId(this.AppWindow.Id, DisplayAreaFallback.Nearest);
            if (displayArea is not null)
            {
                var centeredPosition = this.AppWindow.Position;
                centeredPosition.X = ((displayArea.WorkArea.Width - this.AppWindow.Size.Width) / 2);
                centeredPosition.Y = ((displayArea.WorkArea.Height - this.AppWindow.Size.Height) / 2);
                this.AppWindow.Move(centeredPosition);
            }
        }

        public async Task TriggerScreenTranslationAsync()
        {
            System.Diagnostics.Debug.WriteLine("📸 Делаем скриншот для выбора области...");

            //  Сначала делаем скриншот всего экрана
            var screenshot = _capturer.CaptureScreenBytes();
            if (screenshot.bytes == null || screenshot.bytes.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine("❌ Не удалось сделать скриншот");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"✅ Скриншот готов: {screenshot.bytes.Length} байт, {screenshot.width}x{screenshot.height}");

            // ⚡ Показываем окно выбора области со скриншотом как фоном
            var selectorWindow = new AreaSelectorWindow();
            var selectedArea = await selectorWindow.ShowSelectionWithScreenshotAsync(
                screenshot.bytes, screenshot.width, screenshot.height);

            if (selectedArea.HasValue)
            {
                System.Diagnostics.Debug.WriteLine($"✅ Область выбрана: {selectedArea.Value}");
                await _capturer.CaptureAndTranslateAreaAsync(selectedArea.Value);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("❌ Выбор отменён");
            }
        }
    }
}