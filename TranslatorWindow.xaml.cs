using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;

namespace KeyBoopWin
{
    public sealed partial class TranslatorWindow : Window
    {
        private HttpClient _httpClient;
        private bool _isRuToEn = true;

        // РАЗДЕЛЬНЫЕ СЛОВАРИ для русских и английских букв Морзе
        private static readonly Dictionary<string, char> MorseToCharEn;
        private static readonly Dictionary<string, char> MorseToCharRu;
        private static readonly Dictionary<char, string> CharToMorseEn;
        private static readonly Dictionary<char, string> CharToMorseRu;

        static TranslatorWindow()
        {
            // Английский словарь Морзе
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

            // Русский словарь Морзе
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
            this.AppWindow.Resize(new Windows.Graphics.SizeInt32(980, 850));
            CenterWindowOnScreen();

            _httpClient = new HttpClient();
            this.Closed += TranslatorWindow_Closed;
            UpdateDirectionLabel();
        }

        #region Binary Helpers
        private bool IsBinaryCode(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            bool onlyBinaryChars = text.All(c => c == '0' || c == '1' || c == ' ');
            bool hasBits = text.Contains('0') || text.Contains('1');
            return hasBits && onlyBinaryChars;
        }

        // Кодирование текста в двоичные 8-битные блоки (стандарт UTF-8)
        private string EncodeToBinary(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            return string.Join(" ", bytes.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
        }

        // Расшифровка binary строго через UTF-8
        private string DecodeBinary(string binaryText)
        {
            if (string.IsNullOrWhiteSpace(binaryText)) return "";

            try
            {
                string[] tokens = binaryText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                List<byte> bytes = new List<byte>();

                foreach (string token in tokens)
                {
                    if (token.Length == 8 && token.All(c => c == '0' || c == '1'))
                    {
                        bytes.Add(Convert.ToByte(token, 2));
                    }
                }

                return Encoding.UTF8.GetString(bytes.ToArray());
            }
            catch
            {
                return binaryText;
            }
        }
        #endregion

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
                // === 1. ВВЕДЕН МОРЗЕ ===
                string decodedEn = DecodeMorse(input, MorseToCharEn);
                string decodedRu = DecodeMorse(input, MorseToCharRu);

                if (_isRuToEn)
                {
                    LeftMorseText.Text = decodedRu;
                    RightMorseText.Text = decodedEn;
                    OutputText.Text = decodedEn;

                    LeftBinaryText.Text = EncodeToBinary(decodedRu);
                    RightBinaryText.Text = EncodeToBinary(decodedEn);
                }
                else
                {
                    LeftMorseText.Text = decodedEn;
                    RightMorseText.Text = decodedRu;
                    OutputText.Text = decodedRu;

                    LeftBinaryText.Text = EncodeToBinary(decodedEn);
                    RightBinaryText.Text = EncodeToBinary(decodedRu);
                }

                UpdateDynamicLabels();
            }
            else if (IsBinaryCode(input))
            {
                // === 2. ВВЕДЕН БИНАРНЫЙ КОД (UTF-8) ===
                string decodedText = DecodeBinary(input);

                if (_isRuToEn)
                {
                    LeftBinaryText.Text = decodedText;
                    LeftMorseText.Text = EncodeToMorse(decodedText, CharToMorseRu);
                }
                else
                {
                    LeftBinaryText.Text = decodedText;
                    LeftMorseText.Text = EncodeToMorse(decodedText, CharToMorseEn);
                }

                UpdateDynamicLabels();
            }
            else
            {
                // === 3. ВВЕДЕН ОБЫЧНЫЙ ТЕКСТ ===
                if (_isRuToEn)
                {
                    LeftMorseText.Text = EncodeToMorse(input, CharToMorseRu);
                    LeftBinaryText.Text = EncodeToBinary(input);

                    if (!string.IsNullOrEmpty(OutputText.Text))
                    {
                        RightMorseText.Text = EncodeToMorse(OutputText.Text, CharToMorseEn);
                        RightBinaryText.Text = EncodeToBinary(OutputText.Text);
                    }
                    else
                    {
                        RightMorseText.Text = "";
                        RightBinaryText.Text = "";
                    }
                }
                else
                {
                    LeftMorseText.Text = EncodeToMorse(input, CharToMorseEn);
                    LeftBinaryText.Text = EncodeToBinary(input);

                    if (!string.IsNullOrEmpty(OutputText.Text))
                    {
                        RightMorseText.Text = EncodeToMorse(OutputText.Text, CharToMorseRu);
                        RightBinaryText.Text = EncodeToBinary(OutputText.Text);
                    }
                    else
                    {
                        RightMorseText.Text = "";
                        RightBinaryText.Text = "";
                    }
                }

                UpdateDynamicLabels();
            }
        }

        #region Morse Helpers
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
        #endregion

        private async void Translate_Click(object sender, RoutedEventArgs e)
        {
            string inputText = InputText.Text.Trim();
            if (string.IsNullOrEmpty(inputText))
            {
                ShowError("Введите текст для перевода");
                return;
            }

            string textToTranslate = inputText;
            bool inputIsMorse = IsMorseCode(inputText);
            bool inputIsBinary = IsBinaryCode(inputText);

            // 1. Извлекаем расшифрованный текст для Google Translate
            if (inputIsMorse)
            {
                textToTranslate = _isRuToEn
                    ? DecodeMorse(inputText, MorseToCharRu)
                    : DecodeMorse(inputText, MorseToCharEn);
            }
            else if (inputIsBinary)
            {
                textToTranslate = DecodeBinary(inputText);
            }

            string sourceLang = _isRuToEn ? "ru" : "en";
            string targetLang = _isRuToEn ? "en" : "ru";

            try
            {
                OutputText.Text = "🔄 Перевод...";
                string translatedText = await TranslateWithGoogleAsync(textToTranslate, sourceLang, targetLang);
                OutputText.Text = translatedText;

                // 2. Логика заполнения нижних полей
                if (inputIsMorse)
                {
                    // Для Морзе: в нижних полях Морзе показываем расшифровку слов (ПРИВЕТ / HELLO), 
                    // а в Binary — их двоичный код
                    string decodedEn = DecodeMorse(inputText, MorseToCharEn);
                    string decodedRu = DecodeMorse(inputText, MorseToCharRu);

                    if (_isRuToEn)
                    {
                        LeftMorseText.Text = decodedRu;
                        RightMorseText.Text = translatedText;

                        LeftBinaryText.Text = EncodeToBinary(decodedRu);
                        RightBinaryText.Text = EncodeToBinary(translatedText);
                    }
                    else
                    {
                        LeftMorseText.Text = decodedEn;
                        RightMorseText.Text = translatedText;

                        LeftBinaryText.Text = EncodeToBinary(decodedEn);
                        RightBinaryText.Text = EncodeToBinary(translatedText);
                    }
                }
                else if (inputIsBinary)
                {
                    // Для Binary: в полях Binary показываем расшифрованные слова
                    if (_isRuToEn)
                    {
                        LeftBinaryText.Text = textToTranslate;
                        RightBinaryText.Text = translatedText;

                        LeftMorseText.Text = EncodeToMorse(textToTranslate, CharToMorseRu);
                        RightMorseText.Text = EncodeToMorse(translatedText, CharToMorseEn);
                    }
                    else
                    {
                        LeftBinaryText.Text = textToTranslate;
                        RightBinaryText.Text = translatedText;

                        LeftMorseText.Text = EncodeToMorse(textToTranslate, CharToMorseEn);
                        RightMorseText.Text = EncodeToMorse(translatedText, CharToMorseRu);
                    }
                }
                else
                {
                    // Стандартный ввод (обычный текст): кодируем исходник и перевод
                    if (_isRuToEn)
                    {
                        LeftMorseText.Text = EncodeToMorse(textToTranslate, CharToMorseRu);
                        RightMorseText.Text = EncodeToMorse(translatedText, CharToMorseEn);

                        LeftBinaryText.Text = EncodeToBinary(textToTranslate);
                        RightBinaryText.Text = EncodeToBinary(translatedText);
                    }
                    else
                    {
                        LeftMorseText.Text = EncodeToMorse(textToTranslate, CharToMorseEn);
                        RightMorseText.Text = EncodeToMorse(translatedText, CharToMorseRu);

                        LeftBinaryText.Text = EncodeToBinary(textToTranslate);
                        RightBinaryText.Text = EncodeToBinary(translatedText);
                    }
                }

                UpdateDynamicLabels();
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

            // Меняем местами Морзе
            var tempMorse = LeftMorseText.Text;
            LeftMorseText.Text = RightMorseText.Text;
            RightMorseText.Text = tempMorse;

            // Меняем местами Binary
            var tempBinary = LeftBinaryText.Text;
            LeftBinaryText.Text = RightBinaryText.Text;
            RightBinaryText.Text = tempBinary;

            UpdateDynamicLabels();
        }

        private void UpdateDirectionLabel()
        {
            DirectionLabel.Text = _isRuToEn ? "ru Русский ➔ en English + 📡 Морзе + 💻 Binary" : "en English ➔ ru Русский + 📡 Морзе + 💻 Binary";
            UpdateDynamicLabels();
        }

        private void UpdateDynamicLabels()
        {
            if (_isRuToEn)
            {
                LeftMorseLabel.Text = "📡 Азбука Морзе (Русский)";
                RightMorseLabel.Text = "📡 Азбука Морзе (English)";

                LeftBinaryLabel.Text = "💻 Двоичный код (Русский UTF-8)";
                RightBinaryLabel.Text = "💻 Двоичный код (English UTF-8)";
            }
            else
            {
                LeftMorseLabel.Text = "📡 Азбука Морзе (English)";
                RightMorseLabel.Text = "📡 Азбука Морзе (Русский)";

                LeftBinaryLabel.Text = "💻 Двоичный код (English UTF-8)";
                RightBinaryLabel.Text = "💻 Двоичный код (Русский UTF-8)";
            }
        }

        #region Clipboard Helpers
        private void CopyToClipboard(string text, string message)
        {
            if (!string.IsNullOrEmpty(text))
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage { RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy };
                dataPackage.SetText(text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                ShowNotification(message);
            }
        }

        private void CopyInput_Click(object sender, RoutedEventArgs e) => CopyToClipboard(InputText.Text, "Исходный текст скопирован");
        private void CopyTranslation_Click(object sender, RoutedEventArgs e) => CopyToClipboard(OutputText.Text, "Перевод скопирован в буфер обмена");
        private void CopyLeftMorse_Click(object sender, RoutedEventArgs e) => CopyToClipboard(LeftMorseText.Text, "Морзе скопирован");
        private void CopyRightMorse_Click(object sender, RoutedEventArgs e) => CopyToClipboard(RightMorseText.Text, "Морзе скопирован");
        private void CopyLeftBinary_Click(object sender, RoutedEventArgs e) => CopyToClipboard(LeftBinaryText.Text, "Двоичный код скопирован");
        private void CopyRightBinary_Click(object sender, RoutedEventArgs e) => CopyToClipboard(RightBinaryText.Text, "Двоичный код скопирован");
        #endregion

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            InputText.Text = "";
            OutputText.Text = "";
            LeftMorseText.Text = "";
            RightMorseText.Text = "";
            LeftBinaryText.Text = "";
            RightBinaryText.Text = "";
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