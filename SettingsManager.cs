using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Windows.System;

namespace KeyBoopWin
{
    public class AppSettings
    {
        // === ГОРЯЧИЕ КЛАВИШИ ДЛЯ РУЧНОГО ИСПРАВЛЕНИЯ ===
        public bool EnableManualFixHotkeys { get; set; } = true; // По умолчанию включено (как сейчас)
        public int ConvertToRuKey { get; set; } = 219;
        public int ConvertToEnKey { get; set; } = 221;

        // === ГОРЯЧИЕ КЛАВИШИ ДЛЯ ОТКРЫТИЯ ОКОН ===
        public bool EnableVoiceInputHotkey { get; set; } = false;
        public VirtualKeyModifiers VoiceInputHotkeyModifiers { get; set; } = VirtualKeyModifiers.None;
        public VirtualKey VoiceInputHotkeyKey { get; set; } = VirtualKey.F1;

        public bool EnableTranslatorHotkey { get; set; } = false;
        public VirtualKeyModifiers TranslatorHotkeyModifiers { get; set; } = VirtualKeyModifiers.None;
        public VirtualKey TranslatorHotkeyKey { get; set; } = VirtualKey.F2;

        public bool EnableConverterHotkey { get; set; } = false;
        public VirtualKeyModifiers ConverterHotkeyModifiers { get; set; } = VirtualKeyModifiers.None;
        public VirtualKey ConverterHotkeyKey { get; set; } = VirtualKey.F3;

        public bool EnableScreenTranslatorHotkey { get; set; } = false;
        public VirtualKeyModifiers ScreenTranslatorHotkeyModifiers { get; set; } = VirtualKeyModifiers.None;
        public VirtualKey ScreenTranslatorHotkeyKey { get; set; } = VirtualKey.F10;

        // === СПЯЩИЙ РЕЖИМ И АВТОЗАГРУЗКА ===
        public bool IsSleepMode { get; set; } = false;
        public bool AutoStart { get; set; } = false;

        // === СЛОВАРИ ===
        public string RuDictionaryPath { get; set; } = "Dictionaries\\ru.txt";
        public string EnDictionaryPath { get; set; } = "Dictionaries\\en.txt";
        public string BanwordDictionaryPath { get; set; } = "Dictionaries\\banword.txt";

        // === УВЕДОМЛЕНИЯ ===
        public bool HasSeenTrayNotification { get; set; } = false;
    }

    public static class SettingsManager
    {
        private static readonly string SettingsFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка чтения настроек: {ex.Message}");
            }
            return new AppSettings();
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
                System.Diagnostics.Debug.WriteLine("✅ Настройки сохранены в settings.json");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения настроек: {ex.Message}");
            }
        }

        // Методы для автозагрузки
        public static void SetAutoStart(bool enable)
        {
            try
            {
                string appName = "KeyBoopWin";
                string runPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                string approvedPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

                // 1. Работаем с основной веткой Run
                using (var runKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(runPath, true))
                {
                    if (runKey != null)
                    {
                        if (enable)
                        {
                            string appPath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                            // Оборачиваем путь в кавычки на случай пробелов в имени папки
                            runKey.SetValue(appName, $"\"{appPath}\"");
                        }
                        else
                        {
                            runKey.DeleteValue(appName, false);
                        }
                    }
                }

                // 2. ХИТРОСТЬ: Работаем со скрытой веткой StartupApproved
                using (var approvedKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(approvedPath, true))
                {
                    if (approvedKey != null)
                    {
                        if (enable)
                        {
                            // Если мы ВКЛЮЧАЕМ автозагрузку, удаляем запись о блокировке от Диспетчера задач
                            approvedKey.DeleteValue(appName, false);
                        }
                        else
                        {
                            // Если мы ВЫКЛЮЧАЕМ, ставим официальный флаг "Отключено пользователем" (0x02)
                            byte[] disabledFlag = { 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                            approvedKey.SetValue(appName, disabledFlag, Microsoft.Win32.RegistryValueKind.Binary);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка настройки автозагрузки: {ex.Message}");
            }
        }

        public static bool CheckAutoStart()
        {
            try
            {
                string appName = "KeyBoopWin";
                string runPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                string approvedPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

                // 1. Сначала проверяем, есть ли вообще запись в Run
                using (var runKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(runPath, false))
                {
                    if (runKey == null || runKey.GetValue(appName) == null)
                    {
                        return false; // Записи нет = выключено
                    }
                }

                // 2. Проверяем, не отключил ли пользователь программу через Диспетчер задач
                using (var approvedKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(approvedPath, false))
                {
                    if (approvedKey != null)
                    {
                        var value = approvedKey.GetValue(appName);
                        if (value is byte[] bytes && bytes.Length > 0)
                        {
                            // 0x02, 0x03, 0x04, 0x06, 0x08, 0x09 означают "Disabled" в Диспетчере задач
                            // 0x01 или 0x00 (или отсутствие ключа) означают "Enabled"
                            if (bytes[0] != 0x01 && bytes[0] != 0x00)
                            {
                                return false; // Отключено через Диспетчер задач!
                            }
                        }
                    }
                }

                return true; // Запись есть и НЕ заблокирована Диспетчером задач
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка проверки автозагрузки: {ex.Message}");
                return false;
            }
        }
    }
}