using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Globalization;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Windows.Foundation;

namespace KeyBoopWin
{
    public class ScreenCaptureAndTranslate
    {
        private readonly ScreenTranslatorWindow _translatorWindow;
        private readonly OverlayWindow _overlayWindow;

        public ScreenCaptureAndTranslate(ScreenTranslatorWindow translatorWindow, OverlayWindow overlayWindow)
        {
            _translatorWindow = translatorWindow ?? throw new ArgumentNullException(nameof(translatorWindow));
            _overlayWindow = overlayWindow ?? throw new ArgumentNullException(nameof(overlayWindow));
        }

        public async Task TranslateAreaAsync(Windows.Foundation.Rect area)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🔄 Перевод области: {area}...");

                using (Bitmap? fullBitmap = CaptureScreen())
                {
                    if (fullBitmap == null) return;

                    var drawingRect = new System.Drawing.Rectangle(
                        (int)area.X, (int)area.Y, (int)area.Width, (int)area.Height);

                    using (Bitmap? areaBitmap = fullBitmap.Clone(drawingRect, fullBitmap.PixelFormat))
                    {
                        if (areaBitmap == null) return;

                        using (var memoryStream = new System.IO.MemoryStream())
                        {
                            areaBitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Bmp);
                            memoryStream.Seek(0, System.IO.SeekOrigin.Begin);

                            var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(
                                memoryStream.AsRandomAccessStream());
                            var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

                            if (softwareBitmap != null)
                            {
                                await PerformOCRAndTranslateAsync(softwareBitmap, area.X, area.Y);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка: {ex.Message}");
            }
        }

        private Bitmap? CaptureScreen()
        {
            try
            {
                // 1. Загружаем сохраненный индекс монитора из настроек
                var settings = SettingsManager.Load();
                int monitorIndex = settings.ScreenTranslatorMonitorIndex;

                var screens = System.Windows.Forms.Screen.AllScreens;
                System.Windows.Forms.Screen targetScreen;

                // 2. Выбираем нужный экран безопасно
                if (monitorIndex >= 0 && monitorIndex < screens.Length)
                {
                    targetScreen = screens[monitorIndex];
                }
                else
                {
                    targetScreen = System.Windows.Forms.Screen.PrimaryScreen;
                }

                if (targetScreen == null) return null;

                // 3. Делаем скриншот конкретно выбранного монитора (с учетом его координат X и Y, если они идут вразнобой)
                Bitmap bmp = new Bitmap(targetScreen.Bounds.Width, targetScreen.Bounds.Height);

                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(targetScreen.Bounds.X, targetScreen.Bounds.Y, 0, 0, targetScreen.Bounds.Size);
                }

                return bmp;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка создания скриншота: {ex.Message}");
                return null;
            }
        }

        private async Task PerformOCRAndTranslateAsync(SoftwareBitmap softwareBitmap, double offsetX, double offsetY)
        {
            var language = new Language("en-US");
            if (!OcrEngine.IsLanguageSupported(language)) return;

            var ocrEngine = OcrEngine.TryCreateFromLanguage(language);
            if (ocrEngine == null) return;

            if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 || softwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
            {
                softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            }

            var ocrResult = await ocrEngine.RecognizeAsync(softwareBitmap);
            if (ocrResult == null || ocrResult.Lines == null) return;

            foreach (var line in ocrResult.Lines)
            {
                if (line == null) continue;

                string text = line.Text?.Trim() ?? "";
                if (text.Length < 3) continue;
                if (!IsMostlyEnglish(text)) continue;

                var firstWord = line.Words?.FirstOrDefault();
                if (firstWord == null) continue;

                double x = offsetX + firstWord.BoundingRect.X;
                double y = offsetY + firstWord.BoundingRect.Y;

                System.Diagnostics.Debug.WriteLine($"📝 Найдено: '{text}' на ({x}, {y})");

                string translated = await TranslateTextAsync(text);
                if (string.IsNullOrWhiteSpace(translated) || translated == "[Ошибка]") continue;

                _overlayWindow.AddTranslationBox(
                    x, y, translated,
                    _translatorWindow._overlayBgColor,
                    _translatorWindow._overlayTextColor,
                    _translatorWindow._overlayFontSize);
            }
        }

        private bool IsMostlyEnglish(string text)
        {
            int englishCount = text.Count(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'));
            int totalCount = text.Count(char.IsLetterOrDigit);

            return totalCount > 0 && ((double)englishCount / totalCount) > 0.6;
        }

        private async Task<string> TranslateTextAsync(string text)
        {
            string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=ru&dt=t&q={Uri.EscapeDataString(text)}";
            using var client = new System.Net.Http.HttpClient();

            try
            {
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();

                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var sentences = doc.RootElement[0];
                string result = "";
                foreach (var sentence in sentences.EnumerateArray())
                {
                    if (sentence.GetArrayLength() > 0) result += sentence[0].GetString() ?? "";
                }
                return result;
            }
            catch
            {
                return "[Ошибка]";
            }
        }

        public (byte[] bytes, int width, int height) CaptureScreenBytes()
        {
            try
            {
                using var bmp = CaptureScreen();
                if (bmp == null) return (Array.Empty<byte>(), 0, 0);

                using (var ms = new System.IO.MemoryStream())
                {
                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    return (ms.ToArray(), bmp.Width, bmp.Height);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка скриншота: {ex.Message}");
                return (Array.Empty<byte>(), 0, 0);
            }
        }
    }
}