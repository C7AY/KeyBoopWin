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
using Windows.Foundation; // ⚡ ВАЖНО: для Rect

namespace KeyBoopWin
{
    public class ScreenCaptureAndTranslate
    {
        private readonly ScreenTranslatorWindow _translatorWindow;
        private readonly OverlayWindow _overlayWindow;

        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] static extern IntPtr GetDesktopWindow();
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
        [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr hObject, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hObjectSource, int nXSrc, int nYSrc, int dwRop);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr hObject);
        const int SRCCOPY = 0x00CC0020;

        public ScreenCaptureAndTranslate(ScreenTranslatorWindow translatorWindow, OverlayWindow overlayWindow)
        {
            _translatorWindow = translatorWindow ?? throw new ArgumentNullException(nameof(translatorWindow));
            _overlayWindow = overlayWindow ?? throw new ArgumentNullException(nameof(overlayWindow));
        }

        public async Task CaptureAndTranslateScreenAsync()
        {
            try
            {
                _overlayWindow.ClearBoxes();
                System.Diagnostics.Debug.WriteLine("📸 Делаем скриншот...");
                using (Bitmap bmp = CaptureScreen())
                {
                    if (bmp == null) return;

                    using (var memoryStream = new MemoryStream())
                    {
                        bmp.Save(memoryStream, ImageFormat.Bmp);
                        memoryStream.Seek(0, SeekOrigin.Begin);

                        var decoder = await BitmapDecoder.CreateAsync(memoryStream.AsRandomAccessStream());
                        var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

                        if (softwareBitmap != null)
                        {
                            await PerformOCRAndTranslateAsync(softwareBitmap, 0, 0);
                            // ⚡ УБРАЛИ 6000: теперь ждёт нажатия Esc внутри OverlayWindow
                            await _overlayWindow.ShowAndHideAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка: {ex.Message}");
            }
        }

        // ⚡ Явно указываем Windows.Foundation.Rect в параметре
        public async Task CaptureAndTranslateAreaAsync(Windows.Foundation.Rect area)
        {
            try
            {
                _overlayWindow.ClearBoxes();
                System.Diagnostics.Debug.WriteLine($"🔄 Перевод области: {area}...");

                // ⚡ Делаем свежий скриншот (так как окно выбора уже закрыто)
                using (Bitmap fullBitmap = CaptureScreen())
                {
                    if (fullBitmap == null) return;

                    var drawingRect = new System.Drawing.Rectangle(
                        (int)area.X, (int)area.Y, (int)area.Width, (int)area.Height);

                    using (Bitmap areaBitmap = fullBitmap.Clone(drawingRect, fullBitmap.PixelFormat))
                    {
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
                                // ⚡ УБРАЛИ 6000: теперь ждёт нажатия Esc внутри OverlayWindow
                                await _overlayWindow.ShowAndHideAsync();
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

        private Bitmap CaptureScreen()
        {
            IntPtr hScreen = GetDesktopWindow();
            IntPtr hDC = GetDC(hScreen);
            IntPtr hMemDC = CreateCompatibleDC(hDC);

            var displayArea = Microsoft.UI.Windowing.DisplayArea.Primary;
            int width = (int)displayArea.OuterBounds.Width;
            int height = (int)displayArea.OuterBounds.Height;

            IntPtr hBitmap = CreateCompatibleBitmap(hDC, width, height);
            IntPtr hOld = SelectObject(hMemDC, hBitmap);
            BitBlt(hMemDC, 0, 0, width, height, hDC, 0, 0, SRCCOPY);
            SelectObject(hMemDC, hOld);

            DeleteDC(hMemDC);
            ReleaseDC(hScreen, hDC);

            Bitmap bmp = Image.FromHbitmap(hBitmap);
            DeleteObject(hBitmap);
            return bmp;
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
                using (Bitmap bmp = CaptureScreen())
                {
                    if (bmp == null) return (Array.Empty<byte>(), 0, 0);

                    using (var ms = new System.IO.MemoryStream())
                    {
                        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        return (ms.ToArray(), bmp.Width, bmp.Height);
                    }
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