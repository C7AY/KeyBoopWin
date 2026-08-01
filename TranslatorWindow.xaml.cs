using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        private bool _isRuToEn = true;

        //  РАЗДЕЛЬНЫЕ СЛОВАРИ для русских и английских букв
        private static readonly Dictionary<string, char> MorseToCharEn;
        private static readonly Dictionary<string, char> MorseToCharRu;
        private static readonly Dictionary<char, string> CharToMorseEn;
        private static readonly Dictionary<char, string> CharToMorseRu;

        static TranslatorWindow()
        {
            // Английский словарь
            MorseToCharEn = new Dictionary<string, char>(StringComparer.OrdinalIgnoreCase);
            AddMorse(MorseToCharEn, ".-", 'A'); AddMorse(MorseToCharEn, "-...", 'B'); AddMorse(MorseToCharEn, "-.-.", 'C');
            AddMorse(MorseToCharEn, "-..", 'D'); AddMorse(MorseToCharEn, ".", 'E'); AddMorse(MorseToCharEn, "..-.", 'F');
            AddMorse(MorseToCharEn, "--.", 'G'); AddMorse(MorseToCharEn, "....", 'H'); AddMorse(MorseToCharEn, "..", 'I');
            AddMorse(MorseToCharEn, ".---", 'J'); AddMorse(MorseToCharEn, "-.-", 'K'); AddMorse(MorseToCharEn, ".-..", 'L');
            AddMorse(MorseToCharEn, "--", 'M'); AddMorse(MorseToCharEn, "-.", 'N'); AddMorse(MorseToCharEn, "---", 'O');
            AddMorse(MorseToCharEn, ".--.", 'P'); AddMorse(MorseToCharEn, "--.-", 'Q'); AddMorse(MorseToCharEn, ".-.", 'R');
            AddMorse(MorseToCharEn, "...", 'S'); AddMorse(MorseToCharEn, "-", 'T'); AddMorse(MorseToCharEn, "..-", 'U');
            AddMorse(MorseToCharEn, "...-", 'V'); AddMorse(MorseToCharEn, ".--", 'W'); AddMorse(MorseToCharEn, "-..-", 'X');
            AddMorse(MorseToCharEn, "-.--", 'Y'); AddMorse(MorseToCharEn, "--..", 'Z');

            // Русский словарь
            MorseToCharRu = new Dictionary<string, char>(StringComparer.OrdinalIgnoreCase);
            AddMorse(MorseToCharRu, ".-", 'А'); AddMorse(MorseToCharRu, "-...", 'Б'); AddMorse(MorseToCharRu, ".--", 'В');
            AddMorse(MorseToCharRu, "--.", 'Г'); AddMorse(MorseToCharRu, "-..", 'Д'); AddMorse(MorseToCharRu, ".", 'Е');
            AddMorse(MorseToCharRu, "...-", 'Ж'); AddMorse(MorseToCharRu, "--..", 'З'); AddMorse(MorseToCharRu, "..", 'И');
            AddMorse(MorseToCharRu, ".---", 'Й'); AddMorse(MorseToCharRu, "-.-", 'К'); AddMorse(MorseToCharRu, ".-..", 'Л');
            AddMorse(MorseToCharRu, "--", 'М'); AddMorse(MorseToCharRu, "-.", 'Н'); AddMorse(MorseToCharRu, "---", 'О');
            AddMorse(MorseToCharRu, ".--.", 'П'); AddMorse(MorseToCharRu, ".-.", 'Р'); AddMorse(MorseToCharRu, "...", 'С');
            AddMorse(MorseToCharRu, "-", 'Т'); AddMorse(MorseToCharRu, "..-", 'У'); AddMorse(MorseToCharRu, "..-.", 'Ф');
            AddMorse(MorseToCharRu, "....", 'Х'); AddMorse(MorseToCharRu, "-.-.", 'Ц'); AddMorse(MorseToCharRu, "---.", 'Ч');
            AddMorse(MorseToCharRu, "----", 'Ш'); AddMorse(MorseToCharRu, "--.-", 'Щ'); AddMorse(MorseToCharRu, ".--.-.", 'Ъ');
            AddMorse(MorseToCharRu, "-.--", 'Ы'); AddMorse(MorseToCharRu, "-..-", 'Ь'); AddMorse(MorseToCharRu, "..-..", 'Э');
            AddMorse(MorseToCharRu, "..--", 'Ю'); AddMorse(MorseToCharRu, ".-.-", 'Я');

            // Цифры (общие)
            AddMorse(MorseToCharEn, "-----", '0'); AddMorse(MorseToCharEn, ".----", '1'); AddMorse(MorseToCharEn, "..---", '2');
            AddMorse(MorseToCharEn, "...--", '3'); AddMorse(MorseToCharEn, "....-", '4'); AddMorse(MorseToCharEn, ".....", '5');
            AddMorse(MorseToCharEn, "-....", '6'); AddMorse(MorseToCharEn, "--...", '7'); AddMorse(MorseToCharEn, "---..", '8');
            AddMorse(MorseToCharEn, "----.", '9');
            AddMorse(MorseToCharRu, "-----", '0'); AddMorse(MorseToCharRu, ".----", '1'); AddMorse(MorseToCharRu, "..---", '2');
            AddMorse(MorseToCharRu, "...--", '3'); AddMorse(MorseToCharRu, "....-", '4'); AddMorse(MorseToCharRu, ".....", '5');
            AddMorse(MorseToCharRu, "-....", '6'); AddMorse(MorseToCharRu, "--...", '7'); AddMorse(MorseToCharRu, "---..", '8');
            AddMorse(MorseToCharRu, "----.", '9');

            // Обратные словари
            CharToMorseEn = new Dictionary<char, string>();
            foreach (var kvp in MorseToCharEn) CharToMorseEn[kvp.Value] = kvp.Key;

            CharToMorseRu = new Dictionary<char, string>();
            foreach (var kvp in MorseToCharRu) CharToMorseRu[kvp.Value] = kvp.Key;
        }

        private static void AddMorse(Dictionary<string, char> dict, string code, char ch)
        {
            if (!dict.ContainsKey(code)) dict.Add(code, ch);
        }

        public TranslatorWindow()
        {
            this.InitializeComponent();
            this.AppWindow.Resize(new Windows.Graphics.SizeInt32(950, 650));
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

        private void InputText_TextChanged(object sender, TextChangedEventArgs e)
        {
            string input = InputText.Text.Trim();

            if (IsMorseCode(input))
            {
                // Это Морзе - декодируем на оба языка
                string decodedEn = DecodeMorse(input, MorseToCharEn);
                string decodedRu = DecodeMorse(input, MorseToCharRu);

                // Показываем расшифровку в нижних блоках (вместо кода Морзе)
                if (_isRuToEn)
                {
                    LeftMorseText.Text = decodedRu;   // Русская расшифровка слева
                    RightMorseText.Text = decodedEn;  // Английская расшифровка справа
                    OutputText.Text = decodedEn;      // Перевод в правое поле
                }
                else
                {
                    LeftMorseText.Text = decodedEn;   // Английская расшифровка слева
                    RightMorseText.Text = decodedRu;  // Русская расшифровка справа
                    OutputText.Text = decodedRu;      // Перевод в правое поле
                }

                UpdateMorseLabels();
            }
            else
            {
                // Обычный текст - кодируем в Морзе
                if (_isRuToEn)
                {
                    LeftMorseText.Text = EncodeToMorse(input, CharToMorseRu);
                    if (!string.IsNullOrEmpty(OutputText.Text))
                    {
                        RightMorseText.Text = EncodeToMorse(OutputText.Text, CharToMorseEn);
                    }
                    else
                    {
                        RightMorseText.Text = "";
                    }
                }
                else
                {
                    LeftMorseText.Text = EncodeToMorse(input, CharToMorseEn);
                    if (!string.IsNullOrEmpty(OutputText.Text))
                    {
                        RightMorseText.Text = EncodeToMorse(OutputText.Text, CharToMorseRu);
                    }
                    else
                    {
                        RightMorseText.Text = "";
                    }
                }

                UpdateMorseLabels();
            }
        }

        private bool IsMorseCode(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            bool hasMorseChars = text.Contains('.') || text.Contains('-');
            bool onlyMorseChars = text.All(c => c == '.' || c == '-' || c == ' ');
            return hasMorseChars && onlyMorseChars;
        }

        private string EncodeToMorse(string text, Dictionary<char, string> charToMorse)
        {
            StringBuilder morseBuilder = new StringBuilder();
            string[] words = text.Split(' ');

            for (int i = 0; i < words.Length; i++)
            {
                if (i > 0) morseBuilder.Append("  ");

                foreach (char c in words[i].ToUpper())
                {
                    if (charToMorse.TryGetValue(c, out string morse))
                    {
                        morseBuilder.Append(morse).Append(" ");
                    }
                }
            }

            return morseBuilder.ToString().Trim();
        }

        private string DecodeMorse(string morseCode, Dictionary<string, char> morseToChar)
        {
            StringBuilder decodedText = new StringBuilder();
            string[] words = morseCode.Split(new[] { "  " }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string word in words)
            {
                string[] symbols = word.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string symbol in symbols)
                {
                    if (morseToChar.TryGetValue(symbol, out char ch))
                    {
                        decodedText.Append(ch);
                    }
                }
                decodedText.Append(' ');
            }

            return decodedText.ToString().Trim();
        }

        private async void Translate_Click(object sender, RoutedEventArgs e)
        {
            string inputText = InputText.Text.Trim();
            if (string.IsNullOrEmpty(inputText))
            {
                ShowError("Введите текст для перевода");
                return;
            }

            string sourceLang = _isRuToEn ? "ru" : "en";
            string targetLang = _isRuToEn ? "en" : "ru";

            try
            {
                OutputText.Text = "🔄 Перевод...";
                string translatedText = await TranslateWithGoogleAsync(inputText, sourceLang, targetLang);
                OutputText.Text = translatedText;

                // Кодируем ОБА текста в Морзе
                if (_isRuToEn)
                {
                    LeftMorseText.Text = EncodeToMorse(inputText, CharToMorseRu);
                    RightMorseText.Text = EncodeToMorse(translatedText, CharToMorseEn);
                }
                else
                {
                    LeftMorseText.Text = EncodeToMorse(inputText, CharToMorseEn);
                    RightMorseText.Text = EncodeToMorse(translatedText, CharToMorseRu);
                }

                UpdateMorseLabels();
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
                JsonElement sentences = doc.RootElement[0];
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
            _isRuToEn = !_isRuToEn;
            UpdateDirectionLabel();

            // Меняем местами тексты
            var tempText = InputText.Text;
            InputText.Text = OutputText.Text;
            OutputText.Text = tempText;

            // Меняем местами азбуку Морзе
            var tempMorse = LeftMorseText.Text;
            LeftMorseText.Text = RightMorseText.Text;
            RightMorseText.Text = tempMorse;

            UpdateMorseLabels();
        }

        private void UpdateDirectionLabel()
        {
            DirectionLabel.Text = _isRuToEn ? "ru Русский ➔ en English + Морзе" : "en English ➔ ru Русский + Морзе";
            UpdateMorseLabels();
        }

        private void UpdateMorseLabels()
        {
            if (_isRuToEn)
            {
                LeftMorseLabel.Text = "📡 Азбука Морзе (Русский)";
                RightMorseLabel.Text = "📡 Азбука Морзе (English)";
            }
            else
            {
                LeftMorseLabel.Text = "📡 Азбука Морзе (English)";
                RightMorseLabel.Text = " Азбука Морзе (Русский)";
            }
        }

        private void CopyInput_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(InputText.Text))
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage { RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy };
                dataPackage.SetText(InputText.Text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                ShowNotification("Исходный текст скопирован");
            }
        }

        private void CopyTranslation_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(OutputText.Text))
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage { RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy };
                dataPackage.SetText(OutputText.Text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                ShowNotification("Перевод скопирован в буфер обмена");
            }
        }

        private void CopyLeftMorse_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(LeftMorseText.Text))
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage { RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy };
                dataPackage.SetText(LeftMorseText.Text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                ShowNotification("Морзе скопирован");
            }
        }

        private void CopyRightMorse_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(RightMorseText.Text))
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage { RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy };
                dataPackage.SetText(RightMorseText.Text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                ShowNotification("Морзе скопирован");
            }
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            InputText.Text = "";
            OutputText.Text = "";
            LeftMorseText.Text = "";
            RightMorseText.Text = "";
            InputText.Focus(FocusState.Programmatic);
        }

        private void ShowError(string message)
        {
            var dialog = new ContentDialog { Title = "❌ Ошибка", Content = message, CloseButtonText = "OK", XamlRoot = this.Content.XamlRoot };
            _ = dialog.ShowAsync();
        }

        private void ShowNotification(string message)
        {
            System.Diagnostics.Debug.WriteLine($"📢 {message}");
        }
    }
}