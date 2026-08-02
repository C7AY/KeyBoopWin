using System;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;
using System.IO;

namespace KeyBoopWin
{
    public sealed partial class MainWindow : Window
    {
        private SpeechRecognizer? _speechRecognizer;
        private MicrophonePreview? _micPreview;
        private bool _isPreviewing = false;
        public ObservableCollection<HistoryItem> SessionHistory { get; set; } = new();

        private Microsoft.UI.Dispatching.DispatcherQueueTimer? _hotkeyTimer;
        private bool _wasKeyPressed = false;

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        public MainWindow()
        {
            this.InitializeComponent();
            this.AppWindow?.Resize(new Windows.Graphics.SizeInt32(1000, 750));

            HistoryList.ItemsSource = SessionHistory;
            InitializeSpeechRecognizer();

            RegisterHotkeyTimer();
            CenterWindowOnScreen();
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

        private void InitializeSpeechRecognizer()
        {
            try
            {
                _speechRecognizer = new SpeechRecognizer("ru");
                _speechRecognizer.TextRecognized += OnTextRecognized;
                _speechRecognizer.AudioLevelChanged += OnAudioLevelChanged;
                _speechRecognizer.NoiseSuppressor.LearningProgressChanged += OnNoiseLearningProgress;
                _speechRecognizer.NoiseSuppressor.LearningCompleted += OnNoiseLearningCompleted;
                _speechRecognizer.SetNoiseSuppression(true);

                if (NoiseSuppressionCheckBox != null) NoiseSuppressionCheckBox.IsChecked = true;
                StatusText.Text = "✅ Модель загружена. Шумоподавление включено.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"❌ Ошибка: {ex.Message}. Проверь папку Models.";
            }
        }

        public void UnloadSpeechModel()
        {
            if (_speechRecognizer != null)
            {
                _speechRecognizer.StopListening();
                _speechRecognizer.Dispose();
                _speechRecognizer = null;
                System.Diagnostics.Debug.WriteLine("🔇 Модель Vosk выгружена из памяти");
            }
        }

        public void LoadSpeechModel()
        {
            if (_speechRecognizer == null)
            {
                InitializeSpeechRecognizer();
                System.Diagnostics.Debug.WriteLine(" Модель Vosk загружена в память");
            }
        }

        private void StartVoice_Click(object sender, RoutedEventArgs e)
        {
            if (_speechRecognizer != null)
            {
                _speechRecognizer.StartListening();
                StartVoiceBtn.IsEnabled = false;
                StopVoiceBtn.IsEnabled = true;
                StatusText.Text = "🎤 Запись идет... Говорите.";
            }
        }

        private void StopVoice_Click(object sender, RoutedEventArgs e)
        {
            if (_speechRecognizer != null)
            {
                _speechRecognizer.StopListening();
                StartVoiceBtn.IsEnabled = true;
                StopVoiceBtn.IsEnabled = false;
                AddCurrentTextToHistory();
                StatusText.Text = "⏹️ Запись остановлена. Текст добавлен в историю.";
            }
        }

        private void PreviewMic_Click(object sender, RoutedEventArgs e)
        {
            if (!_isPreviewing)
            {
                _micPreview = new MicrophonePreview();
                _micPreview.AudioLevelChanged += OnAudioLevelChanged;
                _micPreview.StartPreview(_speechRecognizer);
                _isPreviewing = true;
                PreviewMicBtn.Content = "⏹️ Остановить тест";
                StatusText.Text = "🎧 Тест микрофона активен.";
            }
            else
            {
                _micPreview?.StopPreview();
                _micPreview?.Dispose();
                _isPreviewing = false;
                PreviewMicBtn.Content = "🎧 Тест микрофона";
                StatusText.Text = "Тест микрофона остановлен.";
                AudioLevelBar.Value = 0;
            }
        }

        private void OnTextRecognized(object? sender, string text) => DispatcherQueue.TryEnqueue(() => RecognizedText.Text += text + " ");
        private void OnAudioLevelChanged(object? sender, float level) => DispatcherQueue.TryEnqueue(() => AudioLevelBar.Value = Math.Min(1.0, level * 2));

        private void OnNoiseLearningProgress(object? sender, int progressPercent)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (progressPercent < 100)
                {
                    NoiseLearningBar.Visibility = Visibility.Visible;
                    NoiseLearningBar.Value = progressPercent;
                    StatusText.Text = $"🔇 Обучение шуму: {progressPercent}% (молчите ~{(100 - progressPercent) / 50} сек)";
                }
            });
        }

        private void OnNoiseLearningCompleted(object? sender, EventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                NoiseLearningBar.Visibility = Visibility.Collapsed;
                StatusText.Text = "✅ Шумоподавление активно!";
            });
        }

        private void AddCurrentTextToHistory()
        {
            if (RecognizedText == null || string.IsNullOrWhiteSpace(RecognizedText.Text)) return;

            var item = new HistoryItem { Timestamp = DateTime.Now.ToString("HH:mm:ss"), Text = RecognizedText.Text.Trim() };
            SessionHistory.Add(item);
            RecognizedText.Text = "";
        }

        private void AddToHistory_Click(object sender, RoutedEventArgs e) { AddCurrentTextToHistory(); StatusText.Text = "✅ Текст добавлен в историю."; }
        private void ClearHistory_Click(object sender, RoutedEventArgs e) { SessionHistory.Clear(); StatusText.Text = "🗑️ История очищена."; }

        private async void CopyHistoryItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string text)
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage { RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy };
                dataPackage.SetText(text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                StatusText.Text = "📋 Элемент истории скопирован!";
                await Task.Delay(2000);
                if (StatusText.Text == "📋 Элемент истории скопирован!") StatusText.Text = "✅ Готов к работе";
            }
        }

        private async void CopyToClipboard_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(RecognizedText.Text))
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage { RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy };
                dataPackage.SetText(RecognizedText.Text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                StatusText.Text = "📋 Текущий текст скопирован!";
                await Task.Delay(2000);
                if (StatusText.Text == "📋 Текущий текст скопирован!") StatusText.Text = "✅ Готов к работе";
            }
        }

        private void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageSelector.SelectedItem is ComboBoxItem selectedItem)
            {
                string lang = selectedItem.Tag?.ToString() ?? "ru";
                try
                {
                    AddCurrentTextToHistory();
                    _speechRecognizer?.ChangeLanguage(lang);
                    StatusText.Text = $"✅ Язык изменен на: {(lang == "ru" ? "Русский" : "English")}";
                }
                catch (Exception ex) { StatusText.Text = $"❌ Ошибка смены языка: {ex.Message}"; }
            }
        }

        private void MicGainSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            GainValueText.Text = $"{e.NewValue:F1}x";
            if (_speechRecognizer != null) _speechRecognizer.SetMicrophoneGain((float)e.NewValue);
        }

        private void NoiseSuppression_Checked(object sender, RoutedEventArgs e) { if (_speechRecognizer != null) _speechRecognizer.SetNoiseSuppression(true); }
        private void NoiseSuppression_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_speechRecognizer != null) { _speechRecognizer.SetNoiseSuppression(false); NoiseLearningBar.Visibility = Visibility.Collapsed; StatusText.Text = "✅ Шумоподавление выключено"; }
        }

        // ==========================================
        // ГЛОБАЛЬНЫЙ ПЕРЕХВАТ ВСЕХ ХОТКЕЕВ
        // ==========================================
        private void RegisterHotkeyTimer()
        {
            _hotkeyTimer = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().CreateTimer();
            _hotkeyTimer.Interval = TimeSpan.FromMilliseconds(100);
            _hotkeyTimer.Tick += HotkeyTimer_Tick;
            _hotkeyTimer.Start();
        }

        private void HotkeyTimer_Tick(object? sender, object e)
        {
            var settings = SettingsManager.Load();
            if (App.IsRecordingHotkey) return;

            // ⚡ ЗАДЕРЖКА: игнорируем хоткеи в течение 500 мс после их изменения
            if ((DateTime.Now - App.LastHotkeyChangeTime).TotalMilliseconds < 500) return;

            if (settings.IsSleepMode) return;

            // ⚡ ОПТИМИЗАЦИЯ: Вычисляем путь к иконке ОДИН РАЗ за тик таймера
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");

            // 1. Голосовой ввод (просто активируем главное окно)
            CheckHotkey(settings.EnableVoiceInputHotkey, settings.VoiceInputHotkeyModifiers, settings.VoiceInputHotkeyKey, () =>
            {
                this.Activate();
            });

            // 2. Переводчик (создаем, ставим иконку, активируем)
            CheckHotkey(settings.EnableTranslatorHotkey, settings.TranslatorHotkeyModifiers, settings.TranslatorHotkeyKey, () =>
            {
                var window = new TranslatorWindow();
                if (File.Exists(iconPath)) window.AppWindow.SetIcon(iconPath);
                window.Activate();
            });

            // 3. Конвертер регистров (создаем, ставим иконку, активируем)
            CheckHotkey(settings.EnableConverterHotkey, settings.ConverterHotkeyModifiers, settings.ConverterHotkeyKey, () =>
            {
                var window = new ConverterWindow();
                if (File.Exists(iconPath)) window.AppWindow.SetIcon(iconPath);
                window.Activate();
            });

        }

        private void CheckHotkey(bool isEnabled, VirtualKeyModifiers modifiers, VirtualKey key, Action action)
        {
            if (!isEnabled) return;

            uint vk = (uint)key;
            bool keyDown = (GetAsyncKeyState((int)vk) & 0x8000) != 0;
            bool ctrlPressed = (GetAsyncKeyState(0x11) & 0x8000) != 0;
            bool altPressed = (GetAsyncKeyState(0x12) & 0x8000) != 0;
            bool shiftPressed = (GetAsyncKeyState(0x10) & 0x8000) != 0;

            bool modifiersMatch = true;
            if (modifiers.HasFlag(VirtualKeyModifiers.Control)) modifiersMatch &= ctrlPressed; else modifiersMatch &= !ctrlPressed;
            if (modifiers.HasFlag(VirtualKeyModifiers.Menu)) modifiersMatch &= altPressed; else modifiersMatch &= !altPressed;
            if (modifiers.HasFlag(VirtualKeyModifiers.Shift)) modifiersMatch &= shiftPressed; else modifiersMatch &= !shiftPressed;

            if (keyDown && modifiersMatch && !_wasKeyPressed)
            {
                _wasKeyPressed = true;
                action();
            }
            else if (!keyDown)
            {
                _wasKeyPressed = false;
            }
        }

        // ⚡ ИСПРАВЛЕННАЯ СИГНАТУРА (без ?)
        private void OpenTranslator_Click(object sender, RoutedEventArgs e)
        {
            var window = new TranslatorWindow();
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(iconPath)) window.AppWindow.SetIcon(iconPath);
            window.Activate();
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            if (App.Current is App app) app.OpenSettings();
        }

        private void OpenConverter_Click(object sender, RoutedEventArgs e)
        {
            var window = new ConverterWindow();
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(iconPath)) window.AppWindow.SetIcon(iconPath);
            window.Activate();
        }
    }

    public class HistoryItem
    {
        public string Timestamp { get; set; } = "";
        public string Text { get; set; } = "";
    }


}