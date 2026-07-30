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
        private HttpClient? _httpClient;
        public string _overlayBgColor = "#CCFFFFFF";
        public string _overlayTextColor = "#000000";
        public double _overlayFontSize = 16;

        private OverlayWindow? _overlayWindow;
        private ScreenCaptureAndTranslate? _capturer;

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
            System.Diagnostics.Debug.WriteLine("📸 Шаг 1: Делаем скриншот для выбора области...");

            if (_capturer == null) return;

            var screenshot = _capturer.CaptureScreenBytes();
            if (screenshot.bytes == null || screenshot.bytes.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine("❌ Не удалось сделать скриншот");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"✅ Скриншот готов: {screenshot.bytes.Length} байт, {screenshot.width}x{screenshot.height}");

            System.Diagnostics.Debug.WriteLine("🖱️ Шаг 2: Открываем окно выбора области...");
            var selectorWindow = new AreaSelectorWindow();
            var selectedArea = await selectorWindow.ShowSelectionWithScreenshotAsync(
                screenshot.bytes, screenshot.width, screenshot.height);

            if (selectedArea.HasValue)
            {
                System.Diagnostics.Debug.WriteLine($"✅ Шаг 3: Область выбрана: {selectedArea.Value}");

                if (_overlayWindow != null)
                {
                    System.Diagnostics.Debug.WriteLine("🖼️ Шаг 4: Открываем окно перевода с фоном...");

                    var showOverlayTask = _overlayWindow.ShowTranslationWithBackgroundAsync(
                        screenshot.bytes, screenshot.width, screenshot.height);

                    System.Diagnostics.Debug.WriteLine("🔄 Шаг 5: Запускаем распознавание и перевод...");
                    await _capturer.TranslateAreaAsync(selectedArea.Value);

                    System.Diagnostics.Debug.WriteLine("⏳ Шаг 6: Ждем нажатия Esc...");
                    await showOverlayTask;

                    System.Diagnostics.Debug.WriteLine("✅ Готово! Окно закрыто.");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("❌ Выбор отменён");
            }
        }
    }
}