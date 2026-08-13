using System;
using System.Diagnostics;
using System.Security.Principal;
using System.IO;
using System.Text.Json;
using Windows.System;

namespace KeyBoopWin
{
    public class AppSettings
    {


        public bool EnableSystemLayoutSwitchHotkey { get; set; } = false;
        public VirtualKeyModifiers SystemLayoutSwitchModifiers { get; set; } = VirtualKeyModifiers.Menu; // По умолчанию Alt
        public VirtualKey SystemLayoutSwitchKey { get; set; } = VirtualKey.Space; // По умолчанию Space (Alt+Space)

        public bool EnableSystemLayoutPresetHotkey { get; set; } = false;
        public int SystemLayoutPresetIndex { get; set; } = 0;

        public bool DisableAutoLayoutCorrection { get; set; } = false;
        // === ОЗВУЧКА ЭКРАНА (PIPER TTS) ===
        public bool EnableScreenAudioHotkey { get; set; } = false;
        public VirtualKeyModifiers ScreenAudioHotkeyModifiers { get; set; } = VirtualKeyModifiers.None;
        public VirtualKey ScreenAudioHotkeyKey { get; set; } = VirtualKey.F11;
        public string SelectedPiperVoice { get; set; } = "ru_RU-dmitri-medium";

        // === ГОРЯЧИЕ КЛАВИШИ ДЛЯ РУЧНОГО ИСПРАВЛЕНИЯ ===
        public bool EnableManualFixHotkeys { get; set; } = true; 
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

        public bool EnableSymbolsHotkey { get; set; } = false;
        public VirtualKeyModifiers SymbolsHotkeyModifiers { get; set; } = VirtualKeyModifiers.None;
        public VirtualKey SymbolsHotkeyKey { get; set; } = VirtualKey.F4;

        public bool EnableScreenTranslatorHotkey { get; set; } = false;
        public VirtualKeyModifiers ScreenTranslatorHotkeyModifiers { get; set; } = VirtualKeyModifiers.None;
        public VirtualKey ScreenTranslatorHotkeyKey { get; set; } = VirtualKey.F10;
        public int ScreenTranslatorMonitorIndex { get; set; } = 0;

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

        // 1. Проверка прав администратора
        public static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        // 2. Проверка, существует ли уже задача в планировщике
        public static bool IsAutoStartEnabled()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = "/Query /TN \"KeyBoopWin_AutoStart\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (Process process = Process.Start(psi))
                {
                    process?.WaitForExit();
                    // Если код 0, значит задача найдена. Если ошибка (например, не найдена) — вернет не 0.
                    return process?.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
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


        public static void SetAutoStart(bool enable)
        {
            // Название задачи, как оно будет отображаться в Планировщике
            string taskName = "KeyBoopWin_AutoStart";

            // Получаем точный путь к текущему .exe
            string appPath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";

            if (string.IsNullOrEmpty(appPath))
            {
                System.Diagnostics.Debug.WriteLine("Не удалось определить путь к приложению.");
                return;
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    CreateNoWindow = true, // Прячем черное окно консоли
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                if (enable)
                {
                    // Параметры:
                    // /Create - создать задачу
                    // /TN - имя задачи
                    // /TR - путь к программе (обернут в экранированные кавычки на случай пробелов в пути)
                    // /SC ONLOGON - запускать при входе пользователя
                    // /RL HIGHEST - запускать с наивысшими правами (от имени Администратора)
                    // /F - принудительно перезаписать, если такая задача уже есть
                    psi.Arguments = $"/Create /TN \"{taskName}\" /TR \"\\\"{appPath}\\\"\" /SC ONLOGON /RL HIGHEST /F";
                }
                else
                {
                    // Параметры:
                    // /Delete - удалить задачу
                    // /TN - имя задачи
                    // /F - принудительно (без запроса подтверждения)
                    psi.Arguments = $"/Delete /TN \"{taskName}\" /F";
                }

                // Запускаем процесс schtasks в фоне
                using (Process process = Process.Start(psi))
                {
                    process?.WaitForExit(); // Ждем завершения команды

                    if (process?.ExitCode != 0)
                    {
                        string error = process?.StandardError.ReadToEnd();
                        System.Diagnostics.Debug.WriteLine($"Ошибка schtasks (код {process?.ExitCode}): {error}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine(enable ? "Задача автозапуска успешно создана." : "Задача автозапуска удалена.");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка настройки автозагрузки через планировщик: {ex.Message}");
            }
        }

        
    }
}