using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms.VisualStyles;
using Windows.Graphics.Imaging;
using Windows.Media.Core;
using Windows.Media.Ocr;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;
using Windows.Storage.Streams;
using Windows.Storage.Streams;

namespace KeyBoopWin
{
    public class ScreenAudioService : IDisposable
    {
        private MediaPlayer? _player;
        private OcrEngine? _ocrEngine;

        public ScreenAudioService()
        {
            _player = new MediaPlayer();
            InitOcr();
        }

        private void InitOcr()
        {
            try
            {
                var ruLanguage = new Windows.Globalization.Language("ru");
                _ocrEngine = OcrEngine.IsLanguageSupported(ruLanguage)
                    ? OcrEngine.TryCreateFromLanguage(ruLanguage)
                    : OcrEngine.TryCreateFromUserProfileLanguages();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка OCR: {ex.Message}");
            }
        }

        public async Task TriggerScreenAudioAsync()
        {
            try
            {
                var settings = SettingsManager.Load();
                if (settings.IsSleepMode) return;

                // 1. Делаем мгновенный скриншот
                var screenshot = CapturePrimaryScreen();
                if (screenshot.bytes == null || screenshot.bytes.Length == 0) return;

                // 2. Выделяем область
                var selectorWindow = new AreaSelectorWindow();
                var selectedArea = await selectorWindow.ShowSelectionWithScreenshotAsync(
                    screenshot.bytes, screenshot.width, screenshot.height);

                if (!selectedArea.HasValue) return;

                // 3. OCR Текста
                string recognizedText = await RecognizeTextFromCropAsync(
                    screenshot.bytes, screenshot.width, screenshot.height, selectedArea.Value);

                if (string.IsNullOrWhiteSpace(recognizedText)) return;

                Debug.WriteLine($"🔊 Распознано: \"{recognizedText}\"");

                // 4. Озвучиваем через универсальный метод (Piper / Edge TTS)
                await SpeakTextAsync(recognizedText, settings.SelectedPiperVoice);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка ScreenAudioService: {ex.Message}");
            }
        }

        public async Task SpeakTextAsync(string text, string voiceName)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            // 1. Если это модель Piper (содержит разделители моделей)
            if (voiceName.Contains("piper") || voiceName.Contains("-medium") || voiceName.Contains("_"))
            {
                await SpeakWithPiperAsync(text, voiceName);
            }
            // 2. Если в имени явно указан префикс edge:
            else if (voiceName.StartsWith("edge:"))
            {
                string realVoice = voiceName.Replace("edge:", ""); // Например: ru-RU-SvetlanaNeural
                await SpeakWithEdgeTtsAsync(text, realVoice);
            }
            // 3. Во всех остальных случаях используем системный синтез Windows (SAPI)
            else
            {
                await SpeakWithEdgeTtsAsync(text, voiceName);
            }
        }

        private async Task SpeakWithPiperAsync(string text, string voiceName)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string piperFolderPath = Path.Combine(baseDir, "Models", "piper");
                string piperExe = Path.Combine(piperFolderPath, "piper.exe");
                string modelPath = Path.Combine(piperFolderPath, "models", $"{voiceName}.onnx");
                string outputPath = Path.Combine(piperFolderPath, "output.wav");

                if (!File.Exists(piperExe) || !File.Exists(modelPath))
                {
                    Debug.WriteLine("❌ Не найден piper.exe или модель!");
                    return;
                }

                // 1. Очищаем старый аудиофайл
                if (File.Exists(outputPath))
                {
                    try { File.Delete(outputPath); } catch { }
                }

                // 2. Нормализация текста (замена мусора OCR)
                string cleanText = text
                    .Replace("\r", " ")
                    .Replace("\n", " ")
                    .Replace("—", "-")
                    .Replace("–", "-")
                    .Replace(" E ", " в ")
                    .Trim();

                cleanText = Regex.Replace(cleanText, @"\s+", " ");

                if (string.IsNullOrWhiteSpace(cleanText))
                {
                    Debug.WriteLine("⚠️ Текст пуст после очистки.");
                    return;
                }

                Debug.WriteLine($"🚀 Запуск Piper. Голос: {voiceName}. Текст: \"{cleanText}\"");

                // 3. Запускаем piper.exe напрямую с указанием рабочей папки для espeak-ng-data
                var processInfo = new ProcessStartInfo
                {
                    FileName = piperExe,
                    Arguments = $"--model \"{modelPath}\" --output-file \"{outputPath}\"",
                    WorkingDirectory = piperFolderPath, // Важно для фонемных таблиц!
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = new Process { StartInfo = processInfo })
                {
                    process.Start();

                    var errorTask = process.StandardError.ReadToEndAsync();
                    var outputTask = process.StandardOutput.ReadToEndAsync();

                    // Передаем чистый UTF-8 без BOM
                    using (var writer = new StreamWriter(process.StandardInput.BaseStream, new System.Text.UTF8Encoding(false)))
                    {
                        await writer.WriteAsync(cleanText);
                        await writer.FlushAsync();
                    }

                    bool exited = await Task.Run(() => process.WaitForExit(7000));

                    if (!exited)
                    {
                        Debug.WriteLine("⚠️ Piper превысил таймаут.");
                        process.Kill();
                    }
                    else
                    {
                        string errors = await errorTask;
                        if (!string.IsNullOrWhiteSpace(errors))
                        {
                            Debug.WriteLine($"ℹ️ Лог Piper:\n{errors}");
                        }
                    }
                }

                // 4. Проверяем файл и озвучиваем через SoundPlayer
                if (File.Exists(outputPath))
                {
                    FileInfo fi = new FileInfo(outputPath);
                    Debug.WriteLine($"📁 Файл output.wav создан. Размер: {fi.Length} байт.");

                    if (fi.Length > 0)
                    {
                        Debug.WriteLine("🔊 Старт воспроизведения Piper...");

                        await Task.Run(() =>
                        {
                            using (var player = new System.Media.SoundPlayer(outputPath))
                            {
                                player.PlaySync();
                            }
                        });

                        Debug.WriteLine("✅ Воспроизведение завершено.");
                    }
                    else
                    {
                        Debug.WriteLine("⚠️ Файл output.wav пустой (0 байт).");
                    }
                }
                else
                {
                    Debug.WriteLine("❌ Файл output.wav не создан.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка Piper TTS: {ex.Message}");
            }
        }

        private async Task SpeakWithEdgeTtsAsync(string text, string voiceName)
        {
            try
            {
                string cleanText = text.Replace("\r", " ").Replace("\n", " ").Trim();
                if (string.IsNullOrEmpty(cleanText)) return;

                Debug.WriteLine($"🌐 Запуск системного синтеза речи Windows. Запрошенный голос: {voiceName}");

                using (var synthesizer = new SpeechSynthesizer())
                {
                    // Пытаемся подобрать голос под желаемый язык или имя
                    try
                    {
                        var voices = SpeechSynthesizer.AllVoices;

                        // Ищем совпадение по ID или DisplayName
                        var selectedVoice = voices.FirstOrDefault(v =>
                            v.Id.Contains(voiceName, StringComparison.OrdinalIgnoreCase) ||
                            v.DisplayName.Contains(voiceName, StringComparison.OrdinalIgnoreCase));

                        // Если не нашли конкретный, пытаемся найти любой русский голос в системе
                        if (selectedVoice == null && voiceName.StartsWith("ru", StringComparison.OrdinalIgnoreCase))
                        {
                            selectedVoice = voices.FirstOrDefault(v => v.Language.StartsWith("ru", StringComparison.OrdinalIgnoreCase));
                        }

                        if (selectedVoice != null)
                        {
                            synthesizer.Voice = selectedVoice;
                            Debug.WriteLine($"✔️ Выбран системный голос: {selectedVoice.DisplayName} ({selectedVoice.Language})");
                        }
                        else
                        {
                            Debug.WriteLine($"⚠️ Голос '{voiceName}' не найден, используется системный голос по умолчанию.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"⚠️ Не удалось настроить голос, используется дефолтный: {ex.Message}");
                    }

                    // Синтезируем текст в поток
                    SpeechSynthesisStream stream = await synthesizer.SynthesizeTextToStreamAsync(cleanText);

                    if (_player != null && stream != null)
                    {
                        Debug.WriteLine("🔊 Воспроизведение через MediaPlayer...");
                        _player.Pause();
                        _player.Source = Windows.Media.Core.MediaSource.CreateFromStream(stream, stream.ContentType);
                        _player.Play();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка системного синтеза речи: {ex.Message}");
            }
        }

        private (byte[] bytes, int width, int height) CapturePrimaryScreen()
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

                if (targetScreen == null) return (Array.Empty<byte>(), 0, 0);

                // 3. Делаем скриншот выбранного монитора
                using (Bitmap bmp = new Bitmap(targetScreen.Bounds.Width, targetScreen.Bounds.Height, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(targetScreen.Bounds.X, targetScreen.Bounds.Y, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
                    }

                    using (MemoryStream ms = new MemoryStream())
                    {
                        bmp.Save(ms, ImageFormat.Png);
                        return (ms.ToArray(), targetScreen.Bounds.Width, targetScreen.Bounds.Height);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка создания скриншота для диктора: {ex.Message}");
                return (Array.Empty<byte>(), 0, 0);
            }
        }

        private async Task<string> RecognizeTextFromCropAsync(byte[] imageBytes, int fullWidth, int fullHeight, Windows.Foundation.Rect cropArea)
        {
            if (_ocrEngine == null) return string.Empty;

            using (var stream = new InMemoryRandomAccessStream())
            {
                using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
                {
                    writer.WriteBytes(imageBytes);
                    await writer.StoreAsync();
                }

                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                BitmapTransform transform = new BitmapTransform
                {
                    Bounds = new BitmapBounds
                    {
                        X = (uint)Math.Max(0, cropArea.X),
                        Y = (uint)Math.Max(0, cropArea.Y),
                        Width = (uint)Math.Min(fullWidth - cropArea.X, cropArea.Width),
                        Height = (uint)Math.Min(fullHeight - cropArea.Y, cropArea.Height)
                    }
                };

                SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    transform,
                    ExifOrientationMode.IgnoreExifOrientation,
                    ColorManagementMode.DoNotColorManage);

                OcrResult ocrResult = await _ocrEngine.RecognizeAsync(softwareBitmap);
                return ocrResult.Text;
            }
        }

        public void Dispose()
        {
            _player?.Dispose();
        }
    }
}