using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace KeyBoopWin
{
    public static class DictionaryManager
    {
        // Словари
        private static HashSet<string> _ruWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static HashSet<string> _enWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static HashSet<string> _banwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static HashSet<string> _ruBigrams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Флаг инициализации
        private static bool _isInitialized = false;
        public static bool EnableBanword { get; set; } = true;

        public static void Initialize()
        {
            if (_isInitialized) return;

            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string dictDir = Path.Combine(baseDir, "Dictionaries");

                string ruPath = Path.Combine(dictDir, "ru.txt");
                string enPath = Path.Combine(dictDir, "en.txt");
                string banwordPath = Path.Combine(dictDir, "banword.txt");

                Debug.WriteLine($"📂 Путь к словарям: {dictDir}");
                Debug.WriteLine($"📄 Файл RU существует: {File.Exists(ruPath)}");
                Debug.WriteLine($"📄 Файл EN существует: {File.Exists(enPath)}");
                Debug.WriteLine($"📄 Файл Banwords существует: {File.Exists(banwordPath)}");

                // Загрузка русского словаря
                if (File.Exists(ruPath))
                {
                    var lines = File.ReadAllLines(ruPath, Encoding.UTF8)
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .ToArray();
                    _ruWords = new HashSet<string>(lines.Select(l => l.Trim().ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);
                    Debug.WriteLine($"✅ Загружено RU слов из файла: {_ruWords.Count}. Примеры: {string.Join(", ", _ruWords.Take(5))}");
                }

                // Загрузка английского словаря
                if (File.Exists(enPath))
                {
                    var lines = File.ReadAllLines(enPath, Encoding.UTF8)
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .ToArray();
                    _enWords = new HashSet<string>(lines.Select(l => l.Trim().ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);
                    Debug.WriteLine($"✅ Загружено EN слов из файла: {_enWords.Count}. Примеры: {string.Join(", ", _enWords.Take(5))}");
                }

                // Загрузка банвордов
                if (File.Exists(banwordPath))
                {
                    var lines = File.ReadAllLines(banwordPath, Encoding.UTF8)
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .ToArray();
                    _banwords = new HashSet<string>(lines.Select(l => l.Trim().ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);
                    Debug.WriteLine($"✅ Загружено банвордов: {_banwords.Count}");
                }

                _isInitialized = true;
                Debug.WriteLine($"🎯 ИТОГО СЛОВАРЕЙ: RU = {_ruWords.Count}, EN = {_enWords.Count}, BAN = {_banwords.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"️ Ошибка загрузки словарей: {ex.Message}");
            }
        }

        // Проверка наличия слова в словарях
        public static bool ContainsRu(string word) => _isInitialized && _ruWords.Contains(word.ToLowerInvariant());
        public static bool ContainsEn(string word) => _isInitialized && _enWords.Contains(word.ToLowerInvariant());
        public static bool ContainsBanword(string word) => EnableBanword && _isInitialized && _banwords.Contains(word.ToLowerInvariant());
        public static bool ExistsInBothDictionaries(string word) =>
            _isInitialized &&
            _ruWords.Contains(word.ToLowerInvariant()) &&
            _enWords.Contains(word.ToLowerInvariant());

        // Получение слов по длине
        public static IEnumerable<string> GetRuWordsByLength(int minLength, int maxLength)
        {
            if (!_isInitialized) Initialize();
            return _ruWords.Where(w => w.Length >= minLength && w.Length <= maxLength);
        }

        public static IEnumerable<string> GetEnWordsByLength(int minLength, int maxLength)
        {
            if (!_isInitialized) Initialize();
            return _enWords.Where(w => w.Length >= minLength && w.Length <= maxLength);
        }

        // Проверка биграмм
        public static bool ContainsRuBigram(string word1, string word2)
        {
            if (!_isInitialized) Initialize();
            string bigram = $"{word1.ToLowerInvariant()} {word2.ToLowerInvariant()}";
            return _ruBigrams.Contains(bigram);
        }

        //  НОВЫЙ МЕТОД: Перезагрузка словарей по пользовательским путям
        public static void ReloadDictionaries(string ruPath, string enPath, string banPath)
        {
            try
            {
                Debug.WriteLine("🔄 Начинаем перезагрузку словарей...");

                // Очищаем старые данные
                _ruWords.Clear();
                _enWords.Clear();
                _banwords.Clear();
                _ruBigrams.Clear();

                // ⚡ ПРЕОБРАЗУЕМ ОТНОСИТЕЛЬНЫЕ ПУТИ В АБСОЛЮТНЫЕ
                string absRuPath = ConvertToAbsolutePath(ruPath);
                string absEnPath = ConvertToAbsolutePath(enPath);
                string absBanPath = ConvertToAbsolutePath(banPath);

                // Загружаем русский
                if (!string.IsNullOrEmpty(absRuPath) && File.Exists(absRuPath))
                {
                    var lines = File.ReadAllLines(absRuPath, Encoding.UTF8)
                        .Where(l => !string.IsNullOrWhiteSpace(l));
                    foreach (var line in lines) _ruWords.Add(line.Trim().ToLowerInvariant());
                    Debug.WriteLine($"✅ Перезагружено RU слов: {_ruWords.Count} из {absRuPath}");
                }
                else
                {
                    Debug.WriteLine($"⚠️ RU словарь не найден: {absRuPath}");
                }

                // Загружаем английский
                if (!string.IsNullOrEmpty(absEnPath) && File.Exists(absEnPath))
                {
                    var lines = File.ReadAllLines(absEnPath, Encoding.UTF8)
                        .Where(l => !string.IsNullOrWhiteSpace(l));
                    foreach (var line in lines) _enWords.Add(line.Trim().ToLowerInvariant());
                    Debug.WriteLine($"✅ Перезагружено EN слов: {_enWords.Count} из {absEnPath}");
                }
                else
                {
                    Debug.WriteLine($"⚠️ EN словарь не найден: {absEnPath}");
                }

                // Загружаем банворды
                if (!string.IsNullOrEmpty(absBanPath) && File.Exists(absBanPath))
                {
                    var lines = File.ReadAllLines(absBanPath, Encoding.UTF8)
                        .Where(l => !string.IsNullOrWhiteSpace(l));
                    foreach (var line in lines) _banwords.Add(line.Trim().ToLowerInvariant());
                    Debug.WriteLine($"✅ Перезагружено банвордов: {_banwords.Count} из {absBanPath}");
                }
                else
                {
                    Debug.WriteLine($"⚠️ Banword словарь не найден: {absBanPath}");
                }

                _isInitialized = true;
                Debug.WriteLine($"🎯 ИТОГО ПОСЛЕ ПЕРЕЗАГРУЗКИ: RU = {_ruWords.Count}, EN = {_enWords.Count}, BAN = {_banwords.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ Ошибка перезагрузки словарей: {ex.Message}");
            }
        }

        // 🛠️ ВСПОМОГАТЕЛЬНЫЙ МЕТОД: Преобразование пути
        private static string ConvertToAbsolutePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            if (Path.IsPathRooted(path))
                return path;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(baseDir, path);
        }
    }
}