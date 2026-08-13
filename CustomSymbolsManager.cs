using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace KeyBoopWin
{
    public static class CustomSymbolsManager
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KeyBoopWin",
            "custom_symbols.json"
        );

        public static List<string> LoadCustomSymbols()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки символов: {ex.Message}");
            }
            return new List<string>();
        }

        public static void SaveCustomSymbols(List<string> symbols)
        {
            try
            {
                string dir = Path.GetDirectoryName(FilePath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string json = JsonSerializer.Serialize(symbols, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения символов: {ex.Message}");
            }
        }
    }
}