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

            // ⚡ ВЫЗЫВАЕМ ПРАВИЛЬНЫЙ МЕТОД ПОЗИЦИОНИРОВАНИЯ
            PositionWindowOnSelectedMonitor();

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

        private void PositionWindowOnSelectedMonitor()
        {
            try
            {
                var settings = SettingsManager.Load();
                var displays = Microsoft.UI.Windowing.DisplayArea.FindAll();

                int targetIndex = settings.ScreenTranslatorMonitorIndex;

                // Защита от некорректного индекса
                if (displays.Count == 0 || targetIndex < 0 || targetIndex >= displays.Count)
                {
                    targetIndex = 0;
                }

                var targetDisplay = displays[targetIndex];
                var workArea = targetDisplay.WorkArea;

                int windowWidth = this.AppWindow.Size.Width > 0 ? this.AppWindow.Size.Width : 450;
                int windowHeight = this.AppWindow.Size.Height > 0 ? this.AppWindow.Size.Height : 400;

                int x = workArea.X + ((workArea.Width - windowWidth) / 2);
                int y = workArea.Y + ((workArea.Height - windowHeight) / 2);

                // ⚡ ИСПОЛЬЗУЕМ MoveAndResize - это НАМНОГО надежнее в WinUI 3
                var rect = new Windows.Graphics.RectInt32(x, y, windowWidth, windowHeight);
                this.AppWindow.MoveAndResize(rect);

                System.Diagnostics.Debug.WriteLine($"[ScreenTranslator] Окно перемещено на монитор {targetIndex} (X:{x}, Y:{y})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Ошибка позиционирования: {ex.Message}");
            }
        }

        public async Task TriggerScreenTranslationAsync()
        {
            System.Diagnostics.Debug.WriteLine("🎯 [Trigger] ВЫЗВАН TriggerScreenTranslationAsync");

            System.Diagnostics.Debug.WriteLine("📍 [Trigger] Вызов PositionWindowOnSelectedMonitor...");
            PositionWindowOnSelectedMonitor();
            System.Diagnostics.Debug.WriteLine("✅ [Trigger] PositionWindowOnSelectedMonitor завершен");

            System.Diagnostics.Debug.WriteLine(" Шаг 1: Делаем скриншот для выбора области...");

            if (_capturer == null)
            {
                System.Diagnostics.Debug.WriteLine("❌ _capturer равен NULL!");
                return;
            }

            var screenshot = _capturer.CaptureScreenBytes();
            if (screenshot.bytes == null || screenshot.bytes.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine("❌ Не удалось сделать скриншот");
                return;
            }

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
                System.Diagnostics.Debug.WriteLine(" Выбор отменён");
            }
        }
    }
}