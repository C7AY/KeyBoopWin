using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KeyBoopWin
{
    public sealed partial class ConverterWindow : Window
    {
        public ConverterWindow()
        {
            this.InitializeComponent();

            // ⚡ УВЕЛИЧЕННЫЙ РАЗМЕР ОКНА
            this.AppWindow.Resize(new Windows.Graphics.SizeInt32(960, 550));
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

        // 🔤 ВЕРХНИЙ РЕГИСТР
        private void ToUpperCase_Click(object sender, RoutedEventArgs e)
        {
            string input = InputText.Text;
            OutputText.Text = input.ToUpper();
        }

        // 🔡 НИЖНИЙ РЕГИСТР
        private void ToLowerCase_Click(object sender, RoutedEventArgs e)
        {
            string input = InputText.Text;
            OutputText.Text = input.ToLower();
        }

        // 🔠 ЗАГЛАВНЫЕ БУКВЫ (Title Case)
        private void ToTitleCase_Click(object sender, RoutedEventArgs e)
        {
            string input = InputText.Text;
            OutputText.Text = ToTitleCase(input);
        }

        // 🔀 ПЕРЕМЕШАННЫЙ РЕГИСТР (Toggle Case)
        private void ToToggleCase_Click(object sender, RoutedEventArgs e)
        {
            string input = InputText.Text;
            StringBuilder result = new StringBuilder();

            foreach (char c in input)
            {
                if (char.IsUpper(c))
                    result.Append(char.ToLower(c));
                else if (char.IsLower(c))
                    result.Append(char.ToUpper(c));
                else
                    result.Append(c);
            }

            OutputText.Text = result.ToString();
        }

        //  СЛУЧАЙНЫЙ РЕГИСТР (Random Case)
        private void ToRandomCase_Click(object sender, RoutedEventArgs e)
        {
            string input = InputText.Text;
            StringBuilder result = new StringBuilder();
            Random rand = new Random();

            foreach (char c in input)
            {
                if (rand.Next(2) == 0)
                    result.Append(char.ToUpper(c));
                else
                    result.Append(char.ToLower(c));
            }

            OutputText.Text = result.ToString();
        }

        //  РЕГИСТР ПРЕДЛОЖЕНИЯ (Sentence Case)
        private void ToSentenceCase_Click(object sender, RoutedEventArgs e)
        {
            string input = InputText.Text;
            OutputText.Text = ToSentenceCase(input);
        }

        //  КОПИРОВАТЬ РЕЗУЛЬТАТ
        private async void CopyResult_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(OutputText.Text))
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dataPackage.SetText(OutputText.Text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);

                // Визуальная обратная связь
                var btn = (Button)sender;
                var originalContent = btn.Content;
                btn.Content = "✅ Скопировано!";
                await Task.Delay(2000);
                btn.Content = originalContent;
            }
        }

        // 🗑️ ОЧИСТИТЬ ВСЁ
        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            InputText.Text = "";
            OutputText.Text = "";

            // ⚡ ИСПРАВЛЕНО: В WinUI 3 нужно явно указать тип фокуса
            InputText.Focus(FocusState.Programmatic);
        }

        // Вспомогательный метод: Title Case
        private string ToTitleCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var words = input.Split(' ');
            var result = new StringBuilder();

            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    result.Append(char.ToUpper(words[i][0]));
                    if (words[i].Length > 1)
                        result.Append(words[i].Substring(1).ToLower());
                }

                if (i < words.Length - 1)
                    result.Append(' ');
            }

            return result.ToString();
        }

        // Вспомогательный метод: Sentence Case
        private string ToSentenceCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var result = new StringBuilder(input.ToLower());
            bool newSentence = true;

            for (int i = 0; i < result.Length; i++)
            {
                char c = result[i];

                if (newSentence && char.IsLetter(c))
                {
                    result[i] = char.ToUpper(c);
                    newSentence = false;
                }
                else if (c == '.' || c == '!' || c == '?')
                {
                    newSentence = true;
                }
            }

            return result.ToString();
        }
    }
}