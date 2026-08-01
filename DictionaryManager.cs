using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace KeyBoopWin
{
    public static class DictionaryManager
    {
        // Словари (теперь могут быть null для освобождения памяти)
        private static HashSet<string> _ruWords;
        private static HashSet<string> _enWords;
        private static HashSet<string> _banwords;
        private static HashSet<string> _ruBigrams;

        private static bool _isInitialized = false;
        private static bool _isLoaded = false; // Флаг: загружены ли слова в память прямо сейчас

        public static bool EnableBanword { get; set; } = true;

        public static void Initialize()
        {
            if (_isInitialized) return;
            _isInitialized = true;
            LoadDictionariesInternal(); // При первом запуске загружаем сразу
        }

        // Внутренний метод загрузки
        private static void LoadDictionariesInternal()
        {
            if (_isLoaded) return;

            try
            {
                // Инициализируем коллекции, если они null
                if (_ruWords == null) _ruWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (_enWords == null) _enWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (_banwords == null) _banwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (_ruBigrams == null) _ruBigrams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string dictDir = Path.Combine(baseDir, "Dictionaries");

                string ruPath = Path.Combine(dictDir, "ru.txt");
                string enPath = Path.Combine(dictDir, "en.txt");
                string banwordPath = Path.Combine(dictDir, "banword.txt");

                if (File.Exists(ruPath))
                {
                    var lines = File.ReadAllLines(ruPath, Encoding.UTF8).Where(l => !string.IsNullOrWhiteSpace(l));
                    foreach (var line in lines) _ruWords.Add(line.Trim().ToLowerInvariant());
                }

                if (File.Exists(enPath))
                {
                    var lines = File.ReadAllLines(enPath, Encoding.UTF8).Where(l => !string.IsNullOrWhiteSpace(l));
                    foreach (var line in lines) _enWords.Add(line.Trim().ToLowerInvariant());
                }

                if (File.Exists(banwordPath))
                {
                    var lines = File.ReadAllLines(banwordPath, Encoding.UTF8).Where(l => !string.IsNullOrWhiteSpace(l));
                    foreach (var line in lines) _banwords.Add(line.Trim().ToLowerInvariant());
                }

                _isLoaded = true;
                Debug.WriteLine($"🎯 Словари загружены в память: RU = {_ruWords.Count}, EN = {_enWords.Count}, BAN = {_banwords.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ Ошибка загрузки словарей: {ex.Message}");
            }
        }

        // ⚡ НОВЫЙ МЕТОД: Выгрузка словарей из памяти (для спящего режима)
        public static void UnloadDictionaries()
        {
            if (!_isLoaded) return;

            _ruWords?.Clear(); _ruWords = null;
            _enWords?.Clear(); _enWords = null;
            _banwords?.Clear(); _banwords = null;
            _ruBigrams?.Clear(); _ruBigrams = null;
            _isLoaded = false;

            Debug.WriteLine("📚 Словари выгружены из памяти (RAM освобождена)");
        }

        // ⚡ НОВЫЙ МЕТОД: Принудительная загрузка (при выходе из спящего режима)
        public static void LoadDictionaries()
        {
            if (!_isInitialized) Initialize();
            LoadDictionariesInternal();
        }

        // ⚡ БЕЗОПАСНЫЕ ПРОВЕРКИ: если словари выгружены, они загрузятся автоматически (ленивая загрузка)
        public static bool ContainsRu(string word)
        {
            if (!_isLoaded) LoadDictionaries();
            return _ruWords != null && _ruWords.Contains(word.ToLowerInvariant());
        }

        public static bool ContainsEn(string word)
        {
            if (!_isLoaded) LoadDictionaries();
            return _enWords != null && _enWords.Contains(word.ToLowerInvariant());
        }

        public static bool ContainsBanword(string word)
        {
            if (!_isLoaded) LoadDictionaries();
            return EnableBanword && _banwords != null && _banwords.Contains(word.ToLowerInvariant());
        }

        public static bool ExistsInBothDictionaries(string word)
        {
            if (!_isLoaded) LoadDictionaries();
            string lower = word.ToLowerInvariant();
            return _ruWords != null && _enWords != null &&
                   _ruWords.Contains(lower) && _enWords.Contains(lower);
        }

        public static IEnumerable<string> GetRuWordsByLength(int minLength, int maxLength)
        {
            if (!_isLoaded) LoadDictionaries();
            return _ruWords?.Where(w => w.Length >= minLength && w.Length <= maxLength) ?? Enumerable.Empty<string>();
        }

        public static IEnumerable<string> GetEnWordsByLength(int minLength, int maxLength)
        {
            if (!_isLoaded) LoadDictionaries();
            return _enWords?.Where(w => w.Length >= minLength && w.Length <= maxLength) ?? Enumerable.Empty<string>();
        }

        public static bool ContainsRuBigram(string word1, string word2)
        {
            if (!_isLoaded) LoadDictionaries();
            string bigram = $"{word1.ToLowerInvariant()} {word2.ToLowerInvariant()}";
            return _ruBigrams != null && _ruBigrams.Contains(bigram);
        }

        // Перезагрузка словарей по пользовательским путям
        public static void ReloadDictionaries(string ruPath, string enPath, string banPath)
        {
            try
            {
                Debug.WriteLine("🔄 Начинаем перезагрузку словарей...");

                if (_ruWords == null) _ruWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (_enWords == null) _enWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (_banwords == null) _banwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (_ruBigrams == null) _ruBigrams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                _ruWords.Clear();
                _enWords.Clear();
                _banwords.Clear();
                _ruBigrams.Clear();

                string absRuPath = ConvertToAbsolutePath(ruPath);
                string absEnPath = ConvertToAbsolutePath(enPath);
                string absBanPath = ConvertToAbsolutePath(banPath);

                if (!string.IsNullOrEmpty(absRuPath) && File.Exists(absRuPath))
                {
                    var lines = File.ReadAllLines(absRuPath, Encoding.UTF8).Where(l => !string.IsNullOrWhiteSpace(l));
                    foreach (var line in lines) _ruWords.Add(line.Trim().ToLowerInvariant());
                }

                if (!string.IsNullOrEmpty(absEnPath) && File.Exists(absEnPath))
                {
                    var lines = File.ReadAllLines(absEnPath, Encoding.UTF8).Where(l => !string.IsNullOrWhiteSpace(l));
                    foreach (var line in lines) _enWords.Add(line.Trim().ToLowerInvariant());
                }

                if (!string.IsNullOrEmpty(absBanPath) && File.Exists(absBanPath))
                {
                    var lines = File.ReadAllLines(absBanPath, Encoding.UTF8).Where(l => !string.IsNullOrWhiteSpace(l));
                    foreach (var line in lines) _banwords.Add(line.Trim().ToLowerInvariant());
                }

                _isLoaded = true;
                Debug.WriteLine($"🎯 ИТОГО ПОСЛЕ ПЕРЕЗАГРУЗКИ: RU = {_ruWords.Count}, EN = {_enWords.Count}, BAN = {_banwords.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ Ошибка перезагрузки словарей: {ex.Message}");
            }
        }

        private static string ConvertToAbsolutePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            if (Path.IsPathRooted(path)) return path;
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
        }
    }
}