using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KeyBoopWin
{
    public sealed partial class MainWindow : Window
    {
        private SpeechRecognizer? _speechRecognizer;
        private MicrophonePreview? _micPreview;
        private bool _isPreviewing = false;

        public ObservableCollection<HistoryItem> SessionHistory { get; set; } = new();

        public MainWindow()
        {
            this.InitializeComponent();

            this.AppWindow?.Resize(new Windows.Graphics.SizeInt32(1000, 750));

            HistoryList.ItemsSource = SessionHistory;
            InitializeSpeechRecognizer();
            RegisterHotKeys();
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

                // ⚡ ПОДПИСЫВАЕМСЯ НА СОБЫТИЯ НОВОГО КЛАССА NoiseSuppressor
                _speechRecognizer.NoiseSuppressor.LearningProgressChanged += OnNoiseLearningProgress;
                _speechRecognizer.NoiseSuppressor.LearningCompleted += OnNoiseLearningCompleted;

                // ⚡ ВКЛЮЧАЕМ ШУМОПОДАВЛЕНИЕ ПО УМОЛЧАНИЮ
                _speechRecognizer.SetNoiseSuppression(true);
                if (NoiseSuppressionCheckBox != null)
                {
                    NoiseSuppressionCheckBox.IsChecked = true;
                }

                StatusText.Text = "✅ Модель загружена. Шумоподавление включено.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"❌ Ошибка: {ex.Message}. Проверь папку Models.";
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

                // ⚡ ПЕРЕДАЕМ SpeechRecognizer, чтобы тест использовал тот же NoiseSuppressor
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

        private void OnTextRecognized(object? sender, string text)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                RecognizedText.Text += text + " ";
            });
        }

        private void OnAudioLevelChanged(object? sender, float level)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                AudioLevelBar.Value = Math.Min(1.0, level * 2);
            });
        }

        // ⚡ ОБРАБОТЧИК ПРОГРЕССА ОБУЧЕНИЯ ШУМУ
        private void OnNoiseLearningProgress(object? sender, int progressPercent)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (progressPercent < 100)
                {
                    NoiseLearningBar.Visibility = Visibility.Visible;
                    NoiseLearningBar.Value = progressPercent;
                    int secondsLeft = (100 - progressPercent) / 50;
                    StatusText.Text = $"🔇 Обучение шуму: {progressPercent}% (молчите ~{secondsLeft} сек)";
                }
            });
        }

        // ⚡ ОБРАБОТЧИК ЗАВЕРШЕНИЯ ОБУЧЕНИЯ ШУМУ
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
            if (RecognizedText == null || string.IsNullOrWhiteSpace(RecognizedText.Text))
            {
                return;
            }

            var item = new HistoryItem
            {
                Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                Text = RecognizedText.Text.Trim()
            };

            SessionHistory.Add(item);
            RecognizedText.Text = "";
        }

        private void AddToHistory_Click(object sender, RoutedEventArgs e)
        {
            AddCurrentTextToHistory();
            StatusText.Text = "✅ Текст добавлен в историю.";
        }

        private void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            SessionHistory.Clear();
            StatusText.Text = "🗑️ История очищена.";
        }

        private async void CopyHistoryItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string text)
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage
                {
                    RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy
                };
                dataPackage.SetText(text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);

                StatusText.Text = "📋 Элемент истории скопирован!";
                await Task.Delay(2000);
                if (StatusText.Text == "📋 Элемент истории скопирован!")
                {
                    StatusText.Text = "✅ Готов к работе";
                }
            }
        }

        private async void CopyToClipboard_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(RecognizedText.Text))
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage
                {
                    RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy
                };
                dataPackage.SetText(RecognizedText.Text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);

                StatusText.Text = "📋 Текущий текст скопирован!";
                await Task.Delay(2000);
                if (StatusText.Text == "📋 Текущий текст скопирован!")
                {
                    StatusText.Text = "✅ Готов к работе";
                }
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
                catch (Exception ex)
                {
                    StatusText.Text = $"❌ Ошибка смены языка: {ex.Message}";
                }
            }
        }

        private void MicGainSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            GainValueText.Text = $"{e.NewValue:F1}x";

            if (_speechRecognizer != null)
            {
                _speechRecognizer.SetMicrophoneGain((float)e.NewValue);
            }
        }

        private void NoiseSuppression_Checked(object sender, RoutedEventArgs e)
        {
            if (_speechRecognizer != null)
            {
                _speechRecognizer.SetNoiseSuppression(true);
                // Статус обновится автоматически через событие OnNoiseLearningProgress
            }
        }

        private void NoiseSuppression_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_speechRecognizer != null)
            {
                _speechRecognizer.SetNoiseSuppression(false);
                NoiseLearningBar.Visibility = Visibility.Collapsed;
                StatusText.Text = "✅ Шумоподавление выключено";
            }
        }

        private void RegisterHotKeys() { }
        private void TestVoiceInput_Click(object sender, RoutedEventArgs e) => StatusText.Text = "🎤 Используйте панель записи.";
        private void TestKeyboard_Click(object sender, RoutedEventArgs e) => StatusText.Text = "⌨️ Перехват работает в фоне!";
        private void FixLastWord_Click(object sender, RoutedEventArgs e) => StatusText.Text = "🔄 Исправление автоматически.";
        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            if (App.Current is App app)
            {
                app.OpenSettings();
            }
        }
        private void OpenConverter_Click(object sender, RoutedEventArgs e)
        {
            var converterWindow = new ConverterWindow();
            converterWindow.Activate();
        }
        private void OpenTranslator_Click(object sender, RoutedEventArgs e)
        {
            var TranslatorWindow = new TranslatorWindow();
            TranslatorWindow.Activate();
        }

    }

    public class HistoryItem
    {
        public string Timestamp { get; set; } = "";
        public string Text { get; set; } = "";
    }
}