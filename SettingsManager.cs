using System;
using System.IO;
using System.Text.Json;

namespace KeyBoopWin
{
    public class AppSettings
    {
        public int ConvertToRuKey { get; set; } = 219;
        public int ConvertToEnKey { get; set; } = 221;
        public bool IsSleepMode { get; set; } = false;

        // ⚡ ДОБАВЛЯЕМ ЭТИ ТРИ СТРОКИ
        public string RuDictionaryPath { get; set; } = "Dictionaries\\ru.txt";
        public string EnDictionaryPath { get; set; } = "Dictionaries\\en.txt";
        public string BanwordDictionaryPath { get; set; } = "Dictionaries\\banword.txt";

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

                    // ⚡ ЯВНАЯ ПРОВЕРКА НА NULL, которую понимает компилятор
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

            // Если файл не найден или произошла ошибка, возвращаем гарантированно не-null объект
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
    }
}