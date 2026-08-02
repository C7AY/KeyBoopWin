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
using Microsoft.UI.Xaml.Media;
using System.Linq;
using Windows.System;
using Windows.UI;

namespace KeyBoopWin
{
    public sealed partial class SettingsWindow : Window
    {
        private AppSettings _currentSettings;
        private int _tempRuKey = 219;
        private int _tempEnKey = 221;

        private bool _isRecordingRu = false;
        private bool _isRecordingEn = false;
        private bool _isRecordingVoice = false;
        private bool _isRecordingTranslator = false;
        private bool _isRecordingConverter = false;
        private bool _isRecordingScreenTranslator = false;

        private string _ruDictionaryPath;
        private string _enDictionaryPath;
        private string _banwordDictionaryPath;

        // Временные переменные для хоткеев окон
        private VirtualKeyModifiers _tempVoiceModifiers = VirtualKeyModifiers.None;
        private VirtualKey _tempVoiceKey = VirtualKey.F1;

        private VirtualKeyModifiers _tempTranslatorModifiers = VirtualKeyModifiers.None;
        private VirtualKey _tempTranslatorKey = VirtualKey.F2;

        private VirtualKeyModifiers _tempConverterModifiers = VirtualKeyModifiers.None;
        private VirtualKey _tempConverterKey = VirtualKey.F3;

        private VirtualKeyModifiers _tempScreenTranslatorModifiers = VirtualKeyModifiers.None;
        private VirtualKey _tempScreenTranslatorKey = VirtualKey.F10; //  F10 ТОЛЬКО ЗДЕСЬ

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private bool _isLoading = true;

        public SettingsWindow()
        {
            this.InitializeComponent();
            this.AppWindow.Resize(new Windows.Graphics.SizeInt32(850, 900));

            // Загружаем текущие настройки
            _currentSettings = SettingsManager.Load();

            // Загрузка настроек ручного исправления
            _tempRuKey = _currentSettings.ConvertToRuKey;
            _tempEnKey = _currentSettings.ConvertToEnKey;
            ManualFixToggle.IsOn = _currentSettings.EnableManualFixHotkeys;

            // Загрузка настроек хоткеев окон
            VoiceInputToggle.IsOn = _currentSettings.EnableVoiceInputHotkey;
            _tempVoiceModifiers = _currentSettings.VoiceInputHotkeyModifiers;
            _tempVoiceKey = _currentSettings.VoiceInputHotkeyKey;

            TranslatorToggle.IsOn = _currentSettings.EnableTranslatorHotkey;
            _tempTranslatorModifiers = _currentSettings.TranslatorHotkeyModifiers;
            _tempTranslatorKey = _currentSettings.TranslatorHotkeyKey;

            ConverterToggle.IsOn = _currentSettings.EnableConverterHotkey;
            _tempConverterModifiers = _currentSettings.ConverterHotkeyModifiers;
            _tempConverterKey = _currentSettings.ConverterHotkeyKey;

            ScreenTranslatorToggle.IsOn = _currentSettings.EnableScreenTranslatorHotkey;
            _tempScreenTranslatorModifiers = _currentSettings.ScreenTranslatorHotkeyModifiers;
            _tempScreenTranslatorKey = _currentSettings.ScreenTranslatorHotkeyKey;

            // Спящий режим и автозагрузка
            SleepModeToggle.IsOn = _currentSettings.IsSleepMode;
            AutoStartToggle.IsOn = SettingsManager.CheckAutoStart();

            // Словари
            _ruDictionaryPath = _currentSettings.RuDictionaryPath;
            _enDictionaryPath = _currentSettings.EnDictionaryPath;
            _banwordDictionaryPath = _currentSettings.BanwordDictionaryPath;

            // Обновление UI
            UpdateTextBoxes();
            UpdateHotkeyDisplays();
            UpdateManualFixUI();
            UpdateDictionaryPaths();
            CenterWindowOnScreen();

            this.Content.PreviewKeyDown += SettingsWindow_PreviewKeyDown;

            PopulateMonitorSelector();

            // ⚡ БЕЗОПАСНАЯ установка индекса: проверяем, что он существует в списке
            int safeIndex = _currentSettings.ScreenTranslatorMonitorIndex;
            if (safeIndex < 0 || safeIndex >= MonitorSelector.Items.Count)
            {
                safeIndex = 0;
            }
            MonitorSelector.SelectedIndex = safeIndex;



            _isLoading = false; // ⚡ Загрузка завершена, теперь можно сохранять изменения
        }

        private void SettingsWindow_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (_isRecordingRu) { HandleKeyPress(e, ref _tempRuKey, ref _isRecordingRu, "RU"); e.Handled = true; }
            else if (_isRecordingEn) { HandleKeyPress(e, ref _tempEnKey, ref _isRecordingEn, "EN"); e.Handled = true; }
            else if (_isRecordingVoice) { HandleWindowHotkeyKeyPress(e, ref _tempVoiceModifiers, ref _tempVoiceKey, ref _isRecordingVoice, VoiceInputBorder, TxtVoiceInput, "VoiceInput"); e.Handled = true; }
            else if (_isRecordingTranslator) { HandleWindowHotkeyKeyPress(e, ref _tempTranslatorModifiers, ref _tempTranslatorKey, ref _isRecordingTranslator, TranslatorBorder, TxtTranslator, "Translator"); e.Handled = true; }
            else if (_isRecordingConverter) { HandleWindowHotkeyKeyPress(e, ref _tempConverterModifiers, ref _tempConverterKey, ref _isRecordingConverter, ConverterBorder, TxtConverter, "Converter"); e.Handled = true; }
            else if (_isRecordingScreenTranslator) { HandleWindowHotkeyKeyPress(e, ref _tempScreenTranslatorModifiers, ref _tempScreenTranslatorKey, ref _isRecordingScreenTranslator, ScreenTranslatorBorder, TxtScreenTranslator, "ScreenTranslator"); e.Handled = true; }
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

        private void UpdateHotkeyDisplays()
        {
            // ⚡ ВАЖНО: Обновляем состояние кнопок в зависимости от тумблеров
            bool voiceEnabled = VoiceInputToggle.IsOn;
            BtnRecordVoice.IsEnabled = voiceEnabled;
            BtnResetVoice.IsEnabled = voiceEnabled;
            VoiceInputBorder.Opacity = voiceEnabled ? 1.0 : 0.5;
            TxtVoiceInput.Text = voiceEnabled ? GetHotkeyString(_tempVoiceModifiers, _tempVoiceKey) : "Не назначено";

            bool translatorEnabled = TranslatorToggle.IsOn;
            BtnRecordTranslator.IsEnabled = translatorEnabled;
            BtnResetTranslator.IsEnabled = translatorEnabled;
            TranslatorBorder.Opacity = translatorEnabled ? 1.0 : 0.5;
            TxtTranslator.Text = translatorEnabled ? GetHotkeyString(_tempTranslatorModifiers, _tempTranslatorKey) : "Не назначено";

            bool converterEnabled = ConverterToggle.IsOn;
            BtnRecordConverter.IsEnabled = converterEnabled;
            BtnResetConverter.IsEnabled = converterEnabled;
            ConverterBorder.Opacity = converterEnabled ? 1.0 : 0.5;
            TxtConverter.Text = converterEnabled ? GetHotkeyString(_tempConverterModifiers, _tempConverterKey) : "Не назначено";

            bool screenTranslatorEnabled = ScreenTranslatorToggle.IsOn;
            BtnRecordScreenTranslator.IsEnabled = screenTranslatorEnabled;
            BtnResetScreenTranslator.IsEnabled = screenTranslatorEnabled;
            ScreenTranslatorBorder.Opacity = screenTranslatorEnabled ? 1.0 : 0.5;
            TxtScreenTranslator.Text = screenTranslatorEnabled ? GetHotkeyString(_tempScreenTranslatorModifiers, _tempScreenTranslatorKey) : "Не назначено";
        }

        private string GetHotkeyString(VirtualKeyModifiers modifiers, VirtualKey key)
        {
            string result = "";
            if (modifiers.HasFlag(VirtualKeyModifiers.Control)) result += "Ctrl+";
            if (modifiers.HasFlag(VirtualKeyModifiers.Menu)) result += "Alt+";
            if (modifiers.HasFlag(VirtualKeyModifiers.Shift)) result += "Shift+";
            if (modifiers.HasFlag(VirtualKeyModifiers.Windows)) result += "Win+";
            result += key.ToString();
            return result;
        }

        private void UpdateManualFixUI()
        {
            bool isEnabled = ManualFixToggle.IsOn;
            if (BtnRecordToRu != null) BtnRecordToRu.IsEnabled = isEnabled;
            if (BtnRecordToEn != null) BtnRecordToEn.IsEnabled = isEnabled;
            if (BtnResetToRu != null) BtnResetToRu.IsEnabled = isEnabled;
            if (BtnResetToEn != null) BtnResetToEn.IsEnabled = isEnabled;
            if (ManualFixPanel != null) ManualFixPanel.Opacity = isEnabled ? 1.0 : 0.5;
        }

        private void UpdateDictionaryPaths()
        {
            TxtRuDictionary.Text = _ruDictionaryPath;
            TxtEnDictionary.Text = _enDictionaryPath;
            TxtBanwordDictionary.Text = _banwordDictionaryPath;
        }

        // === ОБРАБОТЧИКИ ТУМБЛЕРОВ ===
        private void ManualFixToggle_Toggled(object sender, RoutedEventArgs e)
        {
            UpdateManualFixUI();
            ApplySettingsImmediately(); // ⚡ Мгновенно сохраняем в settings.json

            // Сообщаем главному приложению об изменении
            if (App.Current is App app && app.KeyboardHook != null)
            {
                // Хук сам проверит EnableManualFixHotkeys при следующем нажатии (см. правку ниже)
            }
        }

        private void VoiceInputToggle_Toggled(object sender, RoutedEventArgs e)
        {
            UpdateHotkeyDisplays();
            ApplySettingsImmediately(); // ⚡ Сразу применяем
        }

        private void TranslatorToggle_Toggled(object sender, RoutedEventArgs e)
        {
            UpdateHotkeyDisplays();
            ApplySettingsImmediately();
        }

        private void ConverterToggle_Toggled(object sender, RoutedEventArgs e)
        {
            UpdateHotkeyDisplays();
            ApplySettingsImmediately();
        }

        private void ScreenTranslatorToggle_Toggled(object sender, RoutedEventArgs e)
        {
            UpdateHotkeyDisplays();
            ApplySettingsImmediately();
        }

        

        private void SleepModeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            ApplySettingsImmediately(); // ⚡ Мгновенно сохраняем в settings.json

            // ⚡ МГНОВЕННО применяем состояние к глобальному хуку
            if (App.Current is App app && app.KeyboardHook != null)
            {
                app.KeyboardHook.SetEnabled(!SleepModeToggle.IsOn);
            }
        }
        private void AutoStartToggle_Toggled(object sender, RoutedEventArgs e)
        {
            // ⚡ МГНОВЕННО применяем изменение в реестр (с учетом обхода Диспетчера задач)
            SettingsManager.SetAutoStart(AutoStartToggle.IsOn);

            // Синхронизируем с settings.json
            ApplySettingsImmediately();

            System.Diagnostics.Debug.WriteLine($"⚡ Автозагрузка в реестре {(AutoStartToggle.IsOn ? "ВКЛЮЧЕНА" : "ВЫКЛЮЧЕНА")}");
        }
        private void ApplySettingsImmediately()
        {

            if (_isLoading) return;

            // 1. Загружаем ТЕКУЩИЕ настройки, чтобы не потерять другие поля (AutoStart, уведомления и т.д.)
            var settings = SettingsManager.Load();

            // 2. Обновляем только те поля, которые относятся к тумблерам
            settings.EnableManualFixHotkeys = ManualFixToggle?.IsOn ?? settings.EnableManualFixHotkeys;
            settings.EnableVoiceInputHotkey = VoiceInputToggle?.IsOn ?? settings.EnableVoiceInputHotkey;
            settings.EnableTranslatorHotkey = TranslatorToggle?.IsOn ?? settings.EnableTranslatorHotkey;
            settings.EnableConverterHotkey = ConverterToggle?.IsOn ?? settings.EnableConverterHotkey;
            settings.EnableScreenTranslatorHotkey = ScreenTranslatorToggle?.IsOn ?? settings.EnableScreenTranslatorHotkey;
            settings.AutoStart = AutoStartToggle?.IsOn ?? settings.AutoStart;
            settings.IsSleepMode = SleepModeToggle?.IsOn ?? settings.IsSleepMode;

            // 3. Обновляем текущие значения хоткеев
            settings.ConvertToRuKey = _tempRuKey;
            settings.ConvertToEnKey = _tempEnKey;

            settings.VoiceInputHotkeyModifiers = _tempVoiceModifiers;
            settings.VoiceInputHotkeyKey = _tempVoiceKey;

            settings.TranslatorHotkeyModifiers = _tempTranslatorModifiers;
            settings.TranslatorHotkeyKey = _tempTranslatorKey;

            settings.ConverterHotkeyModifiers = _tempConverterModifiers;
            settings.ConverterHotkeyKey = _tempConverterKey;

            settings.ScreenTranslatorHotkeyModifiers = _tempScreenTranslatorModifiers;
            settings.ScreenTranslatorHotkeyKey = _tempScreenTranslatorKey;

            settings.ScreenTranslatorMonitorIndex = MonitorSelector.SelectedIndex;
            // 4. Обновляем пути к словарям
            settings.RuDictionaryPath = _ruDictionaryPath;
            settings.EnDictionaryPath = _enDictionaryPath;
            settings.BanwordDictionaryPath = _banwordDictionaryPath;

            // 5. Сохраняем обновленный объект обратно в файл
            SettingsManager.Save(settings);

            // Логируем для проверки
            System.Diagnostics.Debug.WriteLine($"💾 Настройки мгновенно сохранены: ManualFix={settings.EnableManualFixHotkeys}, SleepMode={settings.IsSleepMode}");
        }

        // === ОБРАБОТЧИКИ КНОПОК "ИЗМЕНИТЬ" ===
        private void RecordToRu_Click(object sender, RoutedEventArgs e)
        {
            _isRecordingRu = true; _isRecordingEn = false; _isRecordingVoice = false; _isRecordingTranslator = false; _isRecordingConverter = false; _isRecordingScreenTranslator = false;
            TxtConvertToRu.Text = "⌨️ Нажмите Ctrl + клавишу...";
            TxtConvertToRu.Foreground = new SolidColorBrush(Colors.Yellow);
            App.IsRecordingHotkey = true; // ⚡ Блокируем глобальный перехват
        }

        private void RecordToEn_Click(object sender, RoutedEventArgs e)
        {
            _isRecordingEn = true; _isRecordingRu = false; _isRecordingVoice = false; _isRecordingTranslator = false; _isRecordingConverter = false; _isRecordingScreenTranslator = false;
            TxtConvertToEn.Text = "⌨️ Нажмите Ctrl + клавишу...";
            TxtConvertToEn.Foreground = new SolidColorBrush(Colors.Yellow);
            App.IsRecordingHotkey = true; // ⚡ Блокируем глобальный перехват
        }

        private void RecordVoiceInput_Click(object sender, RoutedEventArgs e)
        {
            _isRecordingVoice = true; _isRecordingRu = false; _isRecordingEn = false; _isRecordingTranslator = false; _isRecordingConverter = false; _isRecordingScreenTranslator = false;
            TxtVoiceInput.Text = "⌨️ Нажмите комбинацию...";
            TxtVoiceInput.Foreground = new SolidColorBrush(Colors.Yellow);
            VoiceInputBorder.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 165, 0));
            App.IsRecordingHotkey = true; // ⚡ Блокируем глобальный перехват
        }

        private void RecordTranslator_Click(object sender, RoutedEventArgs e)
        {
            _isRecordingTranslator = true; _isRecordingRu = false; _isRecordingEn = false; _isRecordingVoice = false; _isRecordingConverter = false; _isRecordingScreenTranslator = false;
            TxtTranslator.Text = "⌨️ Нажмите комбинацию...";
            TxtTranslator.Foreground = new SolidColorBrush(Colors.Yellow);
            TranslatorBorder.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 165, 0));
            App.IsRecordingHotkey = true; // ⚡ Блокируем глобальный перехват
        }

        private void RecordConverter_Click(object sender, RoutedEventArgs e)
        {
            _isRecordingConverter = true; _isRecordingRu = false; _isRecordingEn = false; _isRecordingVoice = false; _isRecordingTranslator = false; _isRecordingScreenTranslator = false;
            TxtConverter.Text = "⌨️ Нажмите комбинацию...";
            TxtConverter.Foreground = new SolidColorBrush(Colors.Yellow);
            ConverterBorder.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 165, 0));
            App.IsRecordingHotkey = true; // ⚡ Блокируем глобальный перехват
        }

        private void RecordScreenTranslator_Click(object sender, RoutedEventArgs e)
        {
            _isRecordingScreenTranslator = true; _isRecordingRu = false; _isRecordingEn = false; _isRecordingVoice = false; _isRecordingTranslator = false; _isRecordingConverter = false;
            TxtScreenTranslator.Text = "⌨️ Нажмите комбинацию...";
            TxtScreenTranslator.Foreground = new SolidColorBrush(Colors.Yellow);
            ScreenTranslatorBorder.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 165, 0));
            App.IsRecordingHotkey = true; // ⚡ Блокируем глобальный перехват
        }

        // === ОБРАБОТЧИКИ КНОПОК "СБРОС" ===
        private void ResetToRu_Click(object sender, RoutedEventArgs e)
        {
            _tempRuKey = 219;
            UpdateTextBoxes();
            ApplySettingsImmediately();
            ApplyHotkeysToHook(); // ⚡ Применяем стандартное значение к хуку сразу
            App.LastHotkeyChangeTime = DateTime.Now;
        }

        private void ResetToEn_Click(object sender, RoutedEventArgs e)
        {
            _tempEnKey = 221;
            UpdateTextBoxes();
            ApplySettingsImmediately();
            ApplyHotkeysToHook(); // ⚡ Применяем стандартное значение к хуку сразу
            App.LastHotkeyChangeTime = DateTime.Now;
        }

        private void ResetVoiceInput_Click(object sender, RoutedEventArgs e)
        {
            _tempVoiceModifiers = VirtualKeyModifiers.None;
            _tempVoiceKey = VirtualKey.F1;
            UpdateHotkeyDisplays();
            ApplySettingsImmediately();
            App.LastHotkeyChangeTime = DateTime.Now;
        }

        private void ResetTranslator_Click(object sender, RoutedEventArgs e)
        {
            _tempTranslatorModifiers = VirtualKeyModifiers.None;
            _tempTranslatorKey = VirtualKey.F2;
            UpdateHotkeyDisplays();
            ApplySettingsImmediately();
            App.LastHotkeyChangeTime = DateTime.Now;
        }

        private void ResetConverter_Click(object sender, RoutedEventArgs e)
        {
            _tempConverterModifiers = VirtualKeyModifiers.None;
            _tempConverterKey = VirtualKey.F3;
            UpdateHotkeyDisplays();
            ApplySettingsImmediately();
            App.LastHotkeyChangeTime = DateTime.Now;
        }

        private void ResetScreenTranslator_Click(object sender, RoutedEventArgs e)
        {
            _tempScreenTranslatorModifiers = VirtualKeyModifiers.None;
            _tempScreenTranslatorKey = VirtualKey.F10;
            UpdateHotkeyDisplays();
            ApplySettingsImmediately();
            App.LastHotkeyChangeTime = DateTime.Now;
        }

        // ⚡ НОВЫЙ МЕТОД: Применяет хоткеи к хуку сразу
        private void ApplyHotkeysToHook()
        {
            if (App.Current is App app && app.KeyboardHook != null)
            {
                app.KeyboardHook.ConvertToRuKey = _tempRuKey;
                app.KeyboardHook.ConvertToEnKey = _tempEnKey;
                System.Diagnostics.Debug.WriteLine($"⚡ Хоткеи ПРИМЕНЕНЫ к хуку: RU={_tempRuKey}, EN={_tempEnKey}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("⚠️ ОШИБКА: Не удалось применить хоткеи. KeyboardHook равен null!");
            }
        }


        // === ОБРАБОТКА НАЖАТИЙ КЛАВИШ ===
        private void HandleKeyPress(KeyRoutedEventArgs e, ref int targetKey, ref bool isRecordingFlag, string mode)
        {
            if (e.Key == Windows.System.VirtualKey.Control || e.Key == Windows.System.VirtualKey.Shift || e.Key == Windows.System.VirtualKey.Menu) return;
            targetKey = GetVkCode(e.Key);
            UpdateTextBoxes();
            isRecordingFlag = false;
            var greenBrush = new SolidColorBrush(Colors.LimeGreen);
            if (mode == "RU") TxtConvertToRu.Foreground = greenBrush; else TxtConvertToEn.Foreground = greenBrush;
            ApplySettingsImmediately();
            ApplyHotkeysToHook();

            App.LastHotkeyChangeTime = DateTime.Now; // ⚡ Обновляем время изменения
            App.IsRecordingHotkey = false; // ⚡ Разблокируем глобальный перехват
        }

        private void HandleWindowHotkeyKeyPress(KeyRoutedEventArgs e, ref VirtualKeyModifiers modifiers, ref VirtualKey key, ref bool isRecordingFlag, Border border, TextBlock textBlock, string functionName)
        {
            if (e.Key == VirtualKey.Control || e.Key == VirtualKey.Shift || e.Key == VirtualKey.Menu || e.Key == VirtualKey.LeftWindows || e.Key == VirtualKey.RightWindows) return;

            var newModifiers = VirtualKeyModifiers.None;
            if ((GetAsyncKeyState(0x11) & 0x8000) != 0) newModifiers |= VirtualKeyModifiers.Control;
            if ((GetAsyncKeyState(0x12) & 0x8000) != 0) newModifiers |= VirtualKeyModifiers.Menu;
            if ((GetAsyncKeyState(0x10) & 0x8000) != 0) newModifiers |= VirtualKeyModifiers.Shift;
            if ((GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0) newModifiers |= VirtualKeyModifiers.Windows;

            if (CheckHotkeyConflict(newModifiers, e.Key, functionName))
            {
                isRecordingFlag = false;
                border.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 120, 212));
                UpdateHotkeyDisplays();
                App.LastHotkeyChangeTime = DateTime.Now; // ⚡ Даже при конфликте сбрасываем таймер
                App.IsRecordingHotkey = false;
                return;
            }

            modifiers = newModifiers;
            key = e.Key;
            isRecordingFlag = false;
            border.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 120, 212));
            UpdateHotkeyDisplays();
            ApplySettingsImmediately();

            App.LastHotkeyChangeTime = DateTime.Now; // ⚡ Обновляем время изменения
            App.IsRecordingHotkey = false; // ⚡ Разблокируем глобальный перехват
        }

        // ⚡ ПРОВЕРКА КОНФЛИКТОВ ХОТКЕЕВ
        private bool CheckHotkeyConflict(VirtualKeyModifiers modifiers, VirtualKey key, string currentFunction)
        {
            // Собираем все активные хоткеи
            var hotkeys = new System.Collections.Generic.Dictionary<string, (VirtualKeyModifiers Modifiers, VirtualKey Key)>();

            if (VoiceInputToggle.IsOn && currentFunction != "VoiceInput")
                hotkeys["Голосовой ввод"] = (_tempVoiceModifiers, _tempVoiceKey);

            if (TranslatorToggle.IsOn && currentFunction != "Translator")
                hotkeys["Переводчик"] = (_tempTranslatorModifiers, _tempTranslatorKey);

            if (ConverterToggle.IsOn && currentFunction != "Converter")
                hotkeys["Конвертер регистров"] = (_tempConverterModifiers, _tempConverterKey);

            if (ScreenTranslatorToggle.IsOn && currentFunction != "ScreenTranslator")
                hotkeys["Экранный переводчик"] = (_tempScreenTranslatorModifiers, _tempScreenTranslatorKey);

            // Проверяем конфликт
            foreach (var kvp in hotkeys)
            {
                if (kvp.Value.Modifiers == modifiers && kvp.Value.Key == key)
                {
                    var dialog = new ContentDialog
                    {
                        Title = "⚠️ Конфликт горячей клавиши",
                        Content = $"Комбинация {GetHotkeyString(modifiers, key)} уже используется функцией \"{kvp.Key}\".\n\nВыберите другую комбинацию.",
                        CloseButtonText = "OK",
                        XamlRoot = this.Content.XamlRoot
                    };
                    _ = dialog.ShowAsync();
                    return true;
                }
            }

            return false;
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

        // === СЛОВАРИ ===
        private void BrowseRuDictionary_Click(object sender, RoutedEventArgs e) => HandleDictionarySelection(ref _ruDictionaryPath, TxtRuDictionary, "🇺 Русский словарь");
        private void BrowseEnDictionary_Click(object sender, RoutedEventArgs e) => HandleDictionarySelection(ref _enDictionaryPath, TxtEnDictionary, "🇺🇸 Английский словарь");
        private void BrowseBanwordDictionary_Click(object sender, RoutedEventArgs e) => HandleDictionarySelection(ref _banwordDictionaryPath, TxtBanwordDictionary, "🚫 Словарь запрещённых слов");

        private void HandleDictionarySelection(ref string pathVariable, TextBox targetTextBox, string dictName)
        {
            var path = ShowWin32OpenFileDialog("Text Files\0*.txt\0All Files\0*.*\0");
            if (!string.IsNullOrEmpty(path))
            {
                int wordCount = 0;
                try
                {
                    if (File.Exists(path)) wordCount = File.ReadLines(path).Count(line => !string.IsNullOrWhiteSpace(line));
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"❌ Ошибка чтения файла: {ex.Message}"); }

                pathVariable = path;
                targetTextBox.Text = path;
                _ = ShowDictionaryNotificationAsync(dictName, path, wordCount);
                ApplyNewDictionaries();
            }
        }

        private async Task ShowDictionaryNotificationAsync(string dictName, string filePath, int count)
        {
            var dialog = new ContentDialog { Title = "✅ Словарь выбран", Content = $"{dictName}\n\n Загружено слов: {count}\n Файл: {Path.GetFileName(filePath)}", CloseButtonText = "OK", XamlRoot = this.Content.XamlRoot };
            await dialog.ShowAsync();
        }

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
                    return result;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"❌ Ошибка ShowWin32OpenFileDialog: {ex.Message}"); }
            return string.Empty;
        }

        private void ApplyNewDictionaries() => DictionaryManager.ReloadDictionaries(_ruDictionaryPath, _enDictionaryPath, _banwordDictionaryPath);

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

                if (!Directory.Exists(dictionariesPath)) Directory.CreateDirectory(dictionariesPath);

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
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"❌ Ошибка загрузки {fileName}: {ex.Message}"); }
                    }
                }

                _ruDictionaryPath = "Dictionaries\\ru.txt";
                _enDictionaryPath = "Dictionaries\\en.txt";
                _banwordDictionaryPath = "Dictionaries\\banword.txt";
                UpdateDictionaryPaths();
                ApplyNewDictionaries();

                ApplySettingsImmediately();

                var dialog = new ContentDialog { Title = "✅ Обновление завершено", Content = "Словари успешно обновлены с GitHub!", CloseButtonText = "OK", XamlRoot = this.Content.XamlRoot };
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog { Title = "❌ Ошибка обновления", Content = $"Не удалось обновить словари:\n{ex.Message}", CloseButtonText = "OK", XamlRoot = this.Content.XamlRoot };
                await dialog.ShowAsync();
            }
            finally
            {
                btn.Content = originalContent;
                btn.IsEnabled = true;
            }
        }

        // === СОХРАНЕНИЕ ===
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            ApplySettingsImmediately();

            SettingsManager.SetAutoStart(AutoStartToggle.IsOn);

            // Применяем изменения к хуку
            if (App.Current is App app && app.KeyboardHook != null)
            {
                app.KeyboardHook.ConvertToRuKey = _tempRuKey;
                app.KeyboardHook.ConvertToEnKey = _tempEnKey;
                app.KeyboardHook.SetEnabled(!SleepModeToggle.IsOn);
            }

            this.Close();
        }

        private string ConvertToAbsolutePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            if (Path.IsPathRooted(path)) return path;
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) { this.Close(); }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct OPENFILENAME
        {
            public int lStructSize; public IntPtr hwndOwner; public IntPtr hInstance; public string lpstrFilter;
            public string lpstrCustomFilter; public int nMaxCustFilter; public int nFilterIndex; public string lpstrFile;
            public int nMaxFile; public string lpstrFileTitle; public int nMaxFileTitle; public string lpstrInitialDir;
            public string lpstrTitle; public int Flags; public short nFileOffset; public short nFileExtension;
            public string lpstrDefExt; public IntPtr lCustData; public IntPtr lpfnHook; public string lpTemplateName;
            public IntPtr pvReserved; public int dwReserved; public int FlagsEx;
        }

        private const int OFN_FILEMUSTEXIST = 0x00001000;
        private const int OFN_PATHMUSTEXIST = 0x00000800;
        private const int OFN_EXPLORER = 0x00080000;

        [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
        [DllImport("comdlg32.dll", CharSet = CharSet.Auto, SetLastError = true)] private static extern bool GetOpenFileName(ref OPENFILENAME ofn);

        private void PopulateMonitorSelector()
        {
            MonitorSelector.Items.Clear();

            try
            {
                // Пробуем получить все доступные дисплеи
                var displays = Microsoft.UI.Windowing.DisplayArea.FindAll();

                System.Diagnostics.Debug.WriteLine($"[Settings] Найдено мониторов через FindAll: {displays.Count}");

                if (displays.Count == 0)
                {
                    // Запасной вариант: используем DisplayArea.GetFromWindowId
                    var currentDisplay = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(this.AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
                    if (currentDisplay != null)
                    {
                        MonitorSelector.Items.Add($"Основной монитор ({currentDisplay.WorkArea.Width}x{currentDisplay.WorkArea.Height})");
                        System.Diagnostics.Debug.WriteLine("[Settings] Добавлен монитор через GetFromWindowId");
                    }
                    else
                    {
                        MonitorSelector.Items.Add("Основной монитор");
                        System.Diagnostics.Debug.WriteLine("[Settings] Добавлен монитор по умолчанию");
                    }
                    return;
                }

                // Добавляем все найденные мониторы
                for (int i = 0; i < displays.Count; i++)
                {
                    var display = displays[i];
                    string monitorName;

                    if (i == 0)
                        monitorName = $"Основной монитор ({display.WorkArea.Width}x{display.WorkArea.Height})";
                    else
                        monitorName = $"Монитор {i + 1} ({display.WorkArea.Width}x{display.WorkArea.Height})";

                    MonitorSelector.Items.Add(monitorName);
                    System.Diagnostics.Debug.WriteLine($"[Settings] Добавлен: {monitorName}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"️ Ошибка при заполнении списка мониторов: {ex.Message}");
                MonitorSelector.Items.Add("Основной монитор");
            }
        }

        private void MonitorSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;

            int selectedIndex = MonitorSelector.SelectedIndex;

            // Игнорируем событие, если индекс сбросился в -1 (бывает при перерисовке)
            if (selectedIndex >= 0)
            {
                _currentSettings.ScreenTranslatorMonitorIndex = selectedIndex;
                ApplySettingsImmediately();
                System.Diagnostics.Debug.WriteLine($"[Settings] Сохранен индекс монитора: {selectedIndex}");
            }
        }
    }
}