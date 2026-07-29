using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace KeyBoopWin
{
    public class LayoutCorrector
    {
        private static readonly Dictionary<char, char> _ruToEn = new Dictionary<char, char>
        {
            {'й','q'}, {'ц','w'}, {'у','e'}, {'к','r'}, {'е','t'}, {'н','y'}, {'г','u'},
            {'ш','i'}, {'щ','o'}, {'з','p'}, {'х','['}, {'ъ',']'}, {'ф','a'}, {'ы','s'},
            {'в','d'}, {'а','f'}, {'п','g'}, {'р','h'}, {'о','j'}, {'л','k'}, {'д','l'},
            {'ж',';'}, {'э','\''}, {'я','z'}, {'ч','x'}, {'с','c'}, {'м','v'}, {'и','b'},
            {'т','n'}, {'ь','m'}, {'б',','}, {'ю','.'}, {'ё','`'}
        };

        private static readonly Dictionary<char, char> _enToRu = new Dictionary<char, char>
        {
            {'q','й'}, {'w','ц'}, {'e','у'}, {'r','к'}, {'t','е'}, {'y','н'}, {'u','г'},
            {'i','ш'}, {'o','щ'}, {'p','з'}, {'[','х'}, {']','ъ'}, {'a','ф'}, {'s','ы'},
            {'d','в'}, {'f','а'}, {'g','п'}, {'h','р'}, {'j','о'}, {'k','л'}, {'l','д'},
            {';','ж'}, {'\'','э'}, {'z','я'}, {'x','ч'}, {'c','с'}, {'v','м'}, {'b','и'},
            {'n','т'}, {'m','ь'}, {',','б'}, {'.','ю'}, {'`','ё'}
        };

        public string VkCodesToString(List<int> vkCodes, IntPtr layout)
        {
            StringBuilder sb = new StringBuilder();
            byte[] keyState = new byte[256];

            foreach (int vk in vkCodes)
            {
                if (vk == 32) { sb.Append(' '); continue; }

                if (layout != IntPtr.Zero)
                {
                    uint scanCode = MapVirtualKeyEx((uint)vk, 0, layout);
                    StringBuilder charBuilder = new StringBuilder(2);
                    int result = ToUnicodeEx((uint)vk, scanCode, keyState, charBuilder, charBuilder.Capacity, 0, layout);
                    if (result > 0 && charBuilder.Length > 0)
                    {
                        char c = charBuilder[0];
                        // ⚡ ИСПРАВЛЕНО: Добавлены все символы, которые в русской раскладке являются буквами
                        if (char.IsLetter(c) || char.IsWhiteSpace(c) || c == '[' || c == ']' || c == ';' || c == '\'' || c == ',' || c == '.' || c == '`')
                        {
                            sb.Append(c);
                        }
                    }
                }
            }
            return sb.ToString();
        }

        public bool LooksLikeWrongLayout(string cleanCurrent, string cleanAlt, out string? correctedWord, out bool needsLayoutSwitch)
        {
            correctedWord = null;
            needsLayoutSwitch = false;

            if (string.IsNullOrEmpty(cleanCurrent) || cleanCurrent.Length < 2) return false;

            bool hasEnLetters = cleanCurrent.Any(c => _enToRu.ContainsKey(c));
            bool hasRuLetters = cleanCurrent.Any(c => _ruToEn.ContainsKey(c));

            bool isCurrentRu = hasRuLetters && !hasEnLetters;
            bool isCurrentEn = hasEnLetters && !hasRuLetters;

            if (!isCurrentRu && !isCurrentEn)
            {
                System.Diagnostics.Debug.WriteLine($"⏭ Пропуск: смешанный язык или нет букв '{cleanCurrent}'");
                return false;
            }

            System.Diagnostics.Debug.WriteLine($"🔍 Анализ: Текущее='{cleanCurrent}', Альт='{cleanAlt}'");
            System.Diagnostics.Debug.WriteLine($"   В RU словаре: {DictionaryManager.ContainsRu(cleanCurrent)}, В EN словаре: {DictionaryManager.ContainsEn(cleanCurrent)}");
            System.Diagnostics.Debug.WriteLine($"   Альт в RU: {DictionaryManager.ContainsRu(cleanAlt)}, Альт в EN: {DictionaryManager.ContainsEn(cleanAlt)}");

            // ==========================================
            // ПРАВИЛО 0: Если слово УЖЕ ПРАВИЛЬНОЕ в текущем языке, мы его НЕ ТРОГАЕМ.
            // Это предотвращает циклы типа 'ку' -> 're' -> 'ку'
            // ==========================================
            if (isCurrentRu && DictionaryManager.ContainsRu(cleanCurrent))
            {
                System.Diagnostics.Debug.WriteLine($"⏭ Пропуск: '{cleanCurrent}' уже является правильным русским словом.");
                return false;
            }
            if (isCurrentEn && DictionaryManager.ContainsEn(cleanCurrent))
            {
                System.Diagnostics.Debug.WriteLine($"⏭ Пропуск: '{cleanCurrent}' уже является правильным английским словом.");
                return false;
            }

            // ==========================================
            // СЦЕНАРИЙ 1: Исправление опечатки в ТЕКУЩЕМ языке
            // ==========================================
            if (isCurrentRu)
            {
                string? ruCorrection = SpellingChecker.GetCorrectedSpelling(cleanCurrent, true);
                if (ruCorrection != null)
                {
                    correctedWord = ruCorrection;
                    needsLayoutSwitch = false;
                    System.Diagnostics.Debug.WriteLine($"✅ Найдена опечатка в RU: '{cleanCurrent}' → '{ruCorrection}'");
                    return true;
                }
            }
            else if (isCurrentEn)
            {
                string? enCorrection = SpellingChecker.GetCorrectedSpelling(cleanCurrent, false);
                if (enCorrection != null)
                {
                    correctedWord = enCorrection;
                    needsLayoutSwitch = false;
                    System.Diagnostics.Debug.WriteLine($"✅ Найдена опечатка в EN: '{cleanCurrent}' → '{enCorrection}'");
                    return true;
                }
            }

            // ==========================================
            // СЦЕНАРИЙ 2: Исправление НЕПРАВИЛЬНОЙ раскладки
            // (Срабатывает только если слово НЕ было правильным в текущем языке, см. Правило 0)
            // ==========================================
            if (isCurrentEn && DictionaryManager.ContainsRu(cleanAlt))
            {
                correctedWord = cleanAlt;
                needsLayoutSwitch = true;
                System.Diagnostics.Debug.WriteLine($"✅ Найдена смена раскладки EN->RU: '{cleanCurrent}' → '{cleanAlt}'");
                return true;
            }

            if (isCurrentRu && DictionaryManager.ContainsEn(cleanAlt))
            {
                correctedWord = cleanAlt;
                needsLayoutSwitch = true;
                System.Diagnostics.Debug.WriteLine($"✅ Найдена смена раскладки RU->EN: '{cleanCurrent}' → '{cleanAlt}'");
                return true;
            }

            System.Diagnostics.Debug.WriteLine($"⏭ Пропуск: слово '{cleanCurrent}' не распознано как ошибка.");
            return false;
        }

        [DllImport("user32.dll")] private static extern uint MapVirtualKeyEx(uint uCode, uint uMapType, IntPtr dwhkl);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int ToUnicodeEx(uint wVirtKey, uint wScanCode, byte[] lpKeyState, StringBuilder pwszBuff, int cchBuff, uint wFlags, IntPtr dwhkl);
        public static string ConvertLayout(string text, bool toRussian)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var map = toRussian ? _enToRu : _ruToEn;
            var sb = new StringBuilder();

            foreach (char c in text)
            {
                if (map.TryGetValue(c, out char mapped))
                    sb.Append(mapped);
                else
                    sb.Append(c); // Оставляем пробелы, цифры и знаки препинания как есть
            }

            return sb.ToString();
        }
    }
}