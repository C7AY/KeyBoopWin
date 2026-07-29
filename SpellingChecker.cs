using System;
using System.Collections.Generic;
using System.Linq;

namespace KeyBoopWin
{
    public static class SpellingChecker
    {
        // Глобальный переключатель (можно будет вынести в настройки)
        public static bool EnableSpellCheck { get; set; } = true;

        public static string? GetCorrectedSpelling(string word, bool isRussian)
        {
            if (!EnableSpellCheck) return null;
            if (string.IsNullOrEmpty(word) || word.Length < 2) return null;

            // Ищем слова той же длины или +/- 1 символ
            var candidates = isRussian
                ? DictionaryManager.GetRuWordsByLength(word.Length - 1, word.Length + 1)
                : DictionaryManager.GetEnWordsByLength(word.Length - 1, word.Length + 1);

            string? bestMatch = null;
            int validCandidatesCount = 0;

            foreach (var candidate in candidates)
            {
                // Правило 1: Никогда не меняем слово на само себя
                if (candidate.Equals(word, StringComparison.OrdinalIgnoreCase))
                    continue;

                int distance = CalculateLevenshteinDistance(word, candidate);

                // Правило 2: Расстояние должно быть ровно 1
                if (distance == 1)
                {
                    validCandidatesCount++;
                    bestMatch = candidate;
                }
            }

            // Правило 3: Меняем слово ТОЛЬКО если есть ровно один очевидный вариант.
            // Если вариантов 2 и более (например, "прив" -> "привет" или "привык"), мы не угадываем.
            if (validCandidatesCount == 1)
            {
                return bestMatch;
            }

            return null;
        }

        private static int CalculateLevenshteinDistance(string source, string target)
        {
            if (string.IsNullOrEmpty(source)) return string.IsNullOrEmpty(target) ? 0 : target.Length;
            if (string.IsNullOrEmpty(target)) return source.Length;

            int n = source.Length;
            int m = target.Length;

            // Оптимизация: если длина отличается более чем на 1, расстояние точно > 1
            if (Math.Abs(n - m) > 1) return 2;

            int[,] d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);

                    // Ранний выход для оптимизации: если минимальное значение в строке > 1, дальше нет смысла считать
                    if (i == n && d[i, j] > 1) return 2;
                }
            }
            return d[n, m];
        }
    }
}