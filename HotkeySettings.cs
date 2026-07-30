using System;
using System.IO;
using System.Text.Json;
using Windows.System;

namespace KeyBoopWin
{
    public class HotkeySettings
    {
        public VirtualKeyModifiers Modifiers { get; set; } = VirtualKeyModifiers.None;
        public VirtualKey Key { get; set; } = VirtualKey.F10;

        private static readonly string SettingsPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "hotkey.json");

        public static HotkeySettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<HotkeySettings>(json);
                    return settings ?? new HotkeySettings();
                }
            }
            catch { }

            return new HotkeySettings();
        }

        public void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }

        public string GetKeyString()
        {
            string result = "";

            if (Modifiers.HasFlag(VirtualKeyModifiers.Control))
                result += "Ctrl+";
            if (Modifiers.HasFlag(VirtualKeyModifiers.Menu))
                result += "Alt+";
            if (Modifiers.HasFlag(VirtualKeyModifiers.Shift))
                result += "Shift+";
            if (Modifiers.HasFlag(VirtualKeyModifiers.Windows))
                result += "Win+";

            result += Key.ToString();

            return result;
        }
    }
}