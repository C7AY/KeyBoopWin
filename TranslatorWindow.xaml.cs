using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Windowing;

namespace KeyBoopWin
{
    public sealed partial class TranslatorWindow : Window
    {
        private HttpClient _httpClient;

        // ⚡ Флаг текущего направления перевода (по умолчанию RU -> EN)
        private bool _isRuToEn = true;

        public TranslatorWindow()
        {
            this.InitializeComponent();
            this.AppWindow.Resize(new Windows.Graphics.SizeInt32(900, 600));
            CenterWindowOnScreen();

            _httpClient = new HttpClient();
            this.Closed += TranslatorWindow_Closed;

            UpdateDirectionLabel();
        }

        private void TranslatorWindow_Closed(object sender, WindowEventArgs args)
        {
            _httpClient?.Dispose();
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

        private async void Translate_Click(object sender, RoutedEventArgs e)
        {
            string inputText = InputText.Text.Trim();
            if (string.IsNullOrEmpty(inputText))
            {
                ShowError("Введите текст для перевода");
                return;
            }

            // ⚡ Определяем языки на основе флага
            string sourceLang = _isRuToEn ? "ru" : "en";
            string targetLang = _isRuToEn ? "en" : "ru";

            try
            {
                OutputText.Text = "🔄 Перевод...";
                string translatedText = await TranslateWithGoogleAsync(inputText, sourceLang, targetLang);
                OutputText.Text = translatedText;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка перевода: {ex.Message}");
                ShowError($"Не удалось перевести текст:\n{ex.Message}");
                OutputText.Text = "";
            }
        }

        private async Task<string> TranslateWithGoogleAsync(string text, string from, string to)
        {
            string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={from}&tl={to}&dt=t&q={Uri.EscapeDataString(text)}";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();

            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                JsonElement root = doc.RootElement;
                JsonElement sentences = root[0];

                string translatedText = "";
                foreach (JsonElement sentence in sentences.EnumerateArray())
                {
                    if (sentence.GetArrayLength() > 0)
                    {
                        translatedText += sentence[0].GetString() ?? "";
                    }
                }

                return translatedText;
            }
        }

        private void SwapLanguages_Click(object sender, RoutedEventArgs e)
        {
            // 1. Меняем направление
            _isRuToEn = !_isRuToEn;
            UpdateDirectionLabel();

            // 2. Меняем местами тексты
            var tempText = InputText.Text;
            InputText.Text = OutputText.Text;
            OutputText.Text = tempText;

            System.Diagnostics.Debug.WriteLine($"🔄 Направление изменено на: {(_isRuToEn ? "RU->EN" : "EN->RU")}");
        }

        // ⚡ Обновляем текст индикатора
        private void UpdateDirectionLabel()
        {
            if (_isRuToEn)
                DirectionLabel.Text = "ru Русский ➔ en English";
            else
                DirectionLabel.Text = "en English ➔ ru Русский";
        }

        private void CopyInput_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(InputText.Text))
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dataPackage.SetText(InputText.Text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                ShowNotification("Исходный текст скопирован");
            }
        }

        private void CopyTranslation_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(OutputText.Text))
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dataPackage.SetText(OutputText.Text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                ShowNotification("Перевод скопирован в буфер обмена");
            }
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            InputText.Text = "";
            OutputText.Text = "";
            InputText.Focus(FocusState.Programmatic);
        }

        private void ShowError(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "❌ Ошибка",
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };
            _ = dialog.ShowAsync();
        }

        private void ShowNotification(string message)
        {
            System.Diagnostics.Debug.WriteLine($"📢 {message}");
        }
    }
}