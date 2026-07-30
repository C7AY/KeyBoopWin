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
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            string appPath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                            key.SetValue("KeyBoopWin", appPath);
                        }
                        else
                        {
                            key.DeleteValue("KeyBoopWin", false);
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
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    if (key != null)
                    {
                        var value = key.GetValue("KeyBoopWin");
                        return value != null;
                    }
                }
            }
            catch { }
            return false;
        }
    }
}