using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Linq;

namespace KeyBoopWin
{
    public sealed partial class SettingsWindow : Window
    {
        private AppSettings _currentSettings;
        private int _tempRuKey = 219;
        private int _tempEnKey = 221;

        private bool _isRecordingRu = false;
        private bool _isRecordingEn = false;

        // ⚡ Пути к словарям
        private string _ruDictionaryPath;
        private string _enDictionaryPath;
        private string _banwordDictionaryPath;

        public SettingsWindow()
        {
            this.InitializeComponent();
            this.AppWindow.Resize(new Windows.Graphics.SizeInt32(850, 850));

            _currentSettings = SettingsManager.Load();
            _tempRuKey = _currentSettings.ConvertToRuKey;
            _tempEnKey = _currentSettings.ConvertToEnKey;
            SleepModeToggle.IsOn = _currentSettings.IsSleepMode;

            _ruDictionaryPath = _currentSettings.RuDictionaryPath;
            _enDictionaryPath = _currentSettings.EnDictionaryPath;
            _banwordDictionaryPath = _currentSettings.BanwordDictionaryPath;

            UpdateTextBoxes();
            UpdateDictionaryPaths();
            CenterWindowOnScreen();

            this.Content.PreviewKeyDown += SettingsWindow_PreviewKeyDown;
        }

        private void SettingsWindow_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (_isRecordingRu)
            {
                HandleKeyPress(e, ref _tempRuKey, ref _isRecordingRu, "RU");
                e.Handled = true;
            }
            else if (_isRecordingEn)
            {
                HandleKeyPress(e, ref _tempEnKey, ref _isRecordingEn, "EN");
                e.Handled = true;
            }
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

        private void UpdateTextBoxes()
        {
            TxtConvertToRu.Text = $"Ctrl + {GetKeyName(_tempRuKey)}";
            TxtConvertToEn.Text = $"Ctrl + {GetKeyName(_tempEnKey)}";
        }

        private void UpdateDictionaryPaths()
        {
            TxtRuDictionary.Text = _ruDictionaryPath;
            TxtEnDictionary.Text = _enDictionaryPath;
            TxtBanwordDictionary.Text = _banwordDictionaryPath;
        }

        // 📂 Выбор русского словаря
        private void BrowseRuDictionary_Click(object sender, RoutedEventArgs e)
        {
            HandleDictionarySelection(ref _ruDictionaryPath, TxtRuDictionary, "🇷🇺 Русский словарь");
        }

        // 📂 Выбор английского словаря
        private void BrowseEnDictionary_Click(object sender, RoutedEventArgs e)
        {
            HandleDictionarySelection(ref _enDictionaryPath, TxtEnDictionary, "🇺🇸 Английский словарь");
        }

        // 📂 Выбор словаря запрещённых слов
        private void BrowseBanwordDictionary_Click(object sender, RoutedEventArgs e)
        {
            HandleDictionarySelection(ref _banwordDictionaryPath, TxtBanwordDictionary, "🚫 Словарь запрещённых слов");
        }

        // 🛠️ УНИВЕРСАЛЬНЫЙ МЕТОД: Выбор, подсчёт и применение
        private void HandleDictionarySelection(ref string pathVariable, TextBox targetTextBox, string dictName)
        {
            var path = ShowWin32OpenFileDialog("Text Files\0*.txt\0All Files\0*.*\0");
            if (!string.IsNullOrEmpty(path))
            {
                int wordCount = 0;
                try
                {
                    if (File.Exists(path))
                    {
                        // File.ReadLines читает построчно, не загружая весь файл в память
                        wordCount = File.ReadLines(path).Count(line => !string.IsNullOrWhiteSpace(line));
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Ошибка чтения файла: {ex.Message}");
                }

                // Обновляем переменную и интерфейс
                pathVariable = path;
                targetTextBox.Text = path;

                // Показываем уведомление (даже если wordCount == 0)
                _ = ShowDictionaryNotificationAsync(dictName, path, wordCount);

                // Сразу применяем новые словари в приложении
                ApplyNewDictionaries();
            }
        }

        // 💬 МЕТОД УВЕДОМЛЕНИЯ
        private async Task ShowDictionaryNotificationAsync(string dictName, string filePath, int count)
        {
            var dialog = new ContentDialog
            {
                Title = "✅ Словарь выбран",
                Content = $"{dictName}\n\n📝 Загружено слов: {count}\n📁 Файл: {Path.GetFileName(filePath)}",
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };
            await dialog.ShowAsync();
        }

        // 🛠️ НАДЁЖНЫЙ ВЫБОР ФАЙЛА ЧЕРЕЗ WIN32 API
        private string ShowWin32OpenFileDialog(string filter)
        {
            try
            {
                var ofn = new OPENFILENAME
                {
                    lStructSize = Marshal.SizeOf<OPENFILENAME>(),
                    hwndOwner = GetActiveWindow(),
                    lpstrFilter = filter,
                    lpstrFile = new string('\0', 260),
                    nMaxFile = 260,
                    lpstrTitle = "Выберите файл словаря",
                    Flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_EXPLORER
                };

                if (GetOpenFileName(ref ofn))
                {
                    string result = ofn.lpstrFile;
                    int nullIndex = result.IndexOf('\0');
                    if (nullIndex >= 0) result = result.Substring(0, nullIndex);

                    System.Diagnostics.Debug.WriteLine($"📁 Выбран файл: {result}");
                    return result;
                }

                System.Diagnostics.Debug.WriteLine("⚠️ Пользователь отменил выбор файла");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка ShowWin32OpenFileDialog: {ex.Message}");
            }

            return string.Empty;
        }

        // 🔄 ПРИМЕНЕНИЕ НОВЫХ СЛОВАРЕЙ (ОДИН РАЗ!)
        private void ApplyNewDictionaries()
        {
            System.Diagnostics.Debug.WriteLine($"🔄 Применение новых путей к словарям...");
            DictionaryManager.ReloadDictionaries(_ruDictionaryPath, _enDictionaryPath, _banwordDictionaryPath);
        }

        private async void UpdateDictionariesFromGitHub_Click(object sender, RoutedEventArgs e)
        {
            var btn = (Button)sender;
            var originalContent = btn.Content;
            btn.Content = "⏳ Загрузка...";
            btn.IsEnabled = false;

            try
            {
                string baseUrl = "https://raw.githubusercontent.com/C7AY/keyboopWin/Windows/Dictionaries/";
                string[] files = { "ru.txt", "en.txt", "banword.txt" };

                string appPath = AppDomain.CurrentDomain.BaseDirectory;
                string dictionariesPath = Path.Combine(appPath, "Dictionaries");

                if (!Directory.Exists(dictionariesPath))
                {
                    Directory.CreateDirectory(dictionariesPath);
                }

                using (HttpClient client = new HttpClient())
                {
                    foreach (var fileName in files)
                    {
                        try
                        {
                            string url = baseUrl + fileName;
                            string content = await client.GetStringAsync(url);
                            string filePath = Path.Combine(dictionariesPath, fileName);
                            await File.WriteAllTextAsync(filePath, content);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"❌ Ошибка загрузки {fileName}: {ex.Message}");
                        }
                    }
                }

                _ruDictionaryPath = "Dictionaries\\ru.txt";
                _enDictionaryPath = "Dictionaries\\en.txt";
                _banwordDictionaryPath = "Dictionaries\\banword.txt";
                UpdateDictionaryPaths();

                // Перезагружаем словари после скачивания
                ApplyNewDictionaries();

                var dialog = new ContentDialog
                {
                    Title = "✅ Обновление завершено",
                    Content = "Словари успешно обновлены с GitHub!",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = "❌ Ошибка обновления",
                    Content = $"Не удалось обновить словари:\n{ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
            finally
            {
                btn.Content = originalContent;
                btn.IsEnabled = true;
            }
        }

        private void ApplyHotkeysToHook()
        {
            if (App.Current is App app && app.KeyboardHook != null)
            {
                app.KeyboardHook.ConvertToRuKey = _tempRuKey;
                app.KeyboardHook.ConvertToEnKey = _tempEnKey;
            }
        }

        private int GetVkCode(Windows.System.VirtualKey key) => (int)key;

        private string GetKeyName(int vkCode)
        {
            return vkCode switch
            {
                219 => "[",
                221 => "]",
                186 => ";",
                222 => "'",
                188 => ",",
                190 => ".",
                220 => "\\",
                189 => "-",
                187 => "=",
                32 => "Space",
                _ => ((Windows.System.VirtualKey)vkCode).ToString()
            };
        }

        private void RecordToRu_Click(object sender, RoutedEventArgs e)
        {
            _isRecordingRu = true;
            _isRecordingEn = false;
            TxtConvertToRu.Text = "⌨️ Нажмите Ctrl + клавишу...";
            TxtConvertToRu.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.Yellow);
        }

        private void RecordToEn_Click(object sender, RoutedEventArgs e)
        {
            _isRecordingEn = true;
            _isRecordingRu = false;
            TxtConvertToEn.Text = "⌨️ Нажмите Ctrl + клавишу...";
            TxtConvertToEn.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.Yellow);
        }

        private void HandleKeyPress(KeyRoutedEventArgs e, ref int targetKey, ref bool isRecordingFlag, string mode)
        {
            if (e.Key == Windows.System.VirtualKey.Control || e.Key == Windows.System.VirtualKey.Shift || e.Key == Windows.System.VirtualKey.Menu)
                return;

            targetKey = GetVkCode(e.Key);
            UpdateTextBoxes();
            isRecordingFlag = false;

            var greenBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.LimeGreen);
            if (mode == "RU") TxtConvertToRu.Foreground = greenBrush;
            else TxtConvertToEn.Foreground = greenBrush;

            ApplyHotkeysToHook();
        }

        private void ResetToRu_Click(object sender, RoutedEventArgs e)
        {
            _tempRuKey = 219;
            UpdateTextBoxes();
            ApplyHotkeysToHook();
        }

        private void ResetToEn_Click(object sender, RoutedEventArgs e)
        {
            _tempEnKey = 221;
            UpdateTextBoxes();
            ApplyHotkeysToHook();
        }

        private void SleepMode_Toggled(object sender, RoutedEventArgs e) { }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _currentSettings.ConvertToRuKey = _tempRuKey;
            _currentSettings.ConvertToEnKey = _tempEnKey;
            _currentSettings.IsSleepMode = SleepModeToggle.IsOn;

            //  ПРЕОБРАЗУЕМ ОТНОСИТЕЛЬНЫЕ ПУТИ В АБСОЛЮТНЫЕ
            _currentSettings.RuDictionaryPath = ConvertToAbsolutePath(_ruDictionaryPath);
            _currentSettings.EnDictionaryPath = ConvertToAbsolutePath(_enDictionaryPath);
            _currentSettings.BanwordDictionaryPath = ConvertToAbsolutePath(_banwordDictionaryPath);

            SettingsManager.Save(_currentSettings);
            this.Close();
        }

        // 🛠️ МЕТОД: Преобразует относительный путь в абсолютный
        private string ConvertToAbsolutePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            // Если путь уже абсолютный — возвращаем как есть
            if (Path.IsPathRooted(path))
                return path;

            // Если путь относительный — делаем его абсолютным относительно папки с exe
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(baseDir, path);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // ==========================================
        // ⚡ WIN32 API ДЛЯ ДИАЛОГА ОТКРЫТИЯ ФАЙЛА
        // ==========================================
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct OPENFILENAME
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public string lpstrFilter;
            public string lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public string lpstrFile;
            public int nMaxFile;
            public string lpstrFileTitle;
            public int nMaxFileTitle;
            public string lpstrInitialDir;
            public string lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public string lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public string lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int FlagsEx;
        }

        private const int OFN_FILEMUSTEXIST = 0x00001000;
        private const int OFN_PATHMUSTEXIST = 0x00000800;
        private const int OFN_EXPLORER = 0x00080000;

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("comdlg32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GetOpenFileName(ref OPENFILENAME ofn);
    }
}