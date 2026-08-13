using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KeyBoopWin
{
    public class GlobalKeyboardHook : IDisposable
    {
        private bool _isSuspended = false;

        public void Suspend(bool suspend)
        {
            _isSuspended = suspend;
            if (suspend)
            {
                _pressedModifiers.Clear();
                lock (_lock)
                {
                    _vkBuffer.Clear(); // ⚡ Очищаем буфер, чтобы избежать накопления мусора во время исправления
                    _lastWordStart = 0;
                }
            }
        }

        // Новые свойства для экранного переводчика
        public bool EnableScreenTranslator { get; set; } = false;
        public int ScreenTranslatorKey { get; set; } = 121; // По умолчанию F10 (VK_F10)
        public bool ScreenTranslatorNeedsCtrl { get; set; }
        public bool ScreenTranslatorNeedsAlt { get; set; }
        public bool ScreenTranslatorNeedsShift { get; set; }

        // Событие вызова
        public event EventHandler? ScreenTranslatorRequested;

        public int ConvertToRuKey { get; set; } = 219;
        public int ConvertToEnKey { get; set; } = 221;

        private bool _isEnabled = true;
        public event EventHandler<bool>? ManualConvertRequested;
        private static bool _isInputBlocked = false;
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int LLKHF_INJECTED = 0x00000010;

        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelKeyboardProc _proc;

        // Храним ТОЛЬКО служебные клавиши-модификаторы (Ctrl, Shift, Alt), чтобы не захламлять память
        private readonly HashSet<int> _pressedModifiers = new HashSet<int>();

        private readonly List<int> _vkBuffer = new List<int>();
        private readonly object _lock = new object();
        private int _lastWordStart = 0;

        public char LastBoundaryChar { get; private set; } = ' ';
        public event EventHandler<List<int>>? WordCompleted;

        public GlobalKeyboardHook()
        {
            _proc = HookCallback;
            _hookId = SetHook(_proc);
        }

        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
            if (!enabled) _pressedModifiers.Clear();
            System.Diagnostics.Debug.WriteLine($" GlobalKeyboardHook: {(enabled ? "ВКЛЮЧЕН" : "ВЫКЛЮЧЕН")}");
        }

        public static void SetInputBlocked(bool blocked)
        {
            _isInputBlocked = blocked;
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (var process = Process.GetCurrentProcess())
            using (var module = process.MainModule)
            {
                if (module == null) throw new InvalidOperationException("Не удалось получить модуль процесса");
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(module.ModuleName), 0);
            }
        }

        private bool IsModifierKey(int vkCode)
        {
            return vkCode == 0x11 || vkCode == 0xA2 || vkCode == 0xA3 || // Ctrl (Left/Right)
                   vkCode == 0x10 || vkCode == 0xA0 || vkCode == 0xA1 || // Shift
                   vkCode == 0x12 || vkCode == 0xA4 || vkCode == 0xA5;    // Alt
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0) return CallNextHookEx(_hookId, nCode, wParam, lParam);

            int vkCode = Marshal.ReadInt32(lParam);
            int flags = Marshal.ReadInt32(lParam, 8);

            bool isInjected = (flags & LLKHF_INJECTED) != 0;

            // ⚡ Если включена блокировка ввода, пропускаем ТОЛЬКО события, созданные самой программой (injected)
            if (_isInputBlocked)
            {
                if (isInjected)
                {
                    return CallNextHookEx(_hookId, nCode, wParam, lParam);
                }
                return (IntPtr)1; // Реальные нажатия пользователя глушим
            }

            if (!_isEnabled || _isSuspended)
            {
                return CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            if (isInjected)
                return CallNextHookEx(_hookId, nCode, wParam, lParam);

            // Обработка нажатия модификаторов
            if (wParam == (IntPtr)WM_KEYDOWN)
            {
                if (IsModifierKey(vkCode))
                {
                    _pressedModifiers.Add(vkCode);
                }
            }
            // Обработка отпускания (WM_KEYUP = 0x0101, WM_SYSKEYUP = 0x0105)
            else if (wParam == (IntPtr)0x0101 || wParam == (IntPtr)0x0105)
            {
                if (IsModifierKey(vkCode))
                {
                    _pressedModifiers.Remove(vkCode);
                }
            }

            // Жесткая страховка: проверяем реальное состояние Ctrl через GetKeyState
            bool isCtrlPhysicallyPressed = (GetKeyState(0x11) & 0x8000) != 0 ||
                                           (GetKeyState(0xA2) & 0x8000) != 0 ||
                                           (GetKeyState(0xA3) & 0x8000) != 0;

            if (!isCtrlPhysicallyPressed)
            {
                _pressedModifiers.Remove(0x11);
                _pressedModifiers.Remove(0xA2);
                _pressedModifiers.Remove(0xA3);
            }

            bool isCtrlPressed = isCtrlPhysicallyPressed ||
                                 _pressedModifiers.Contains(0x11) ||
                                 _pressedModifiers.Contains(0xA2) ||
                                 _pressedModifiers.Contains(0xA3);

            // ⚡ 1. ПРОВЕРКА РУЧНЫХ ГОРЯЧИХ КЛАВИШ (Ctrl + клавиша)
            if (wParam == (IntPtr)WM_KEYDOWN && isCtrlPressed)
            {
                if (vkCode == ConvertToRuKey)
                {
                    ManualConvertRequested?.Invoke(this, true);
                    return (IntPtr)1;
                }
                if (vkCode == ConvertToEnKey)
                {
                    ManualConvertRequested?.Invoke(this, false);
                    return (IntPtr)1;
                }
            }

            // ⚡ 1.5 ПРОВЕРКА ЭКРАННОГО ПЕРЕВОДЧИКА
            if (EnableScreenTranslator && wParam == (IntPtr)WM_KEYDOWN)
            {
                bool isAltPressed = _pressedModifiers.Contains(0x12) || _pressedModifiers.Contains(0xA4) || _pressedModifiers.Contains(0xA5);
                bool isShiftPressed = _pressedModifiers.Contains(0x10) || _pressedModifiers.Contains(0xA0) || _pressedModifiers.Contains(0xA1);

                if (vkCode == ScreenTranslatorKey &&
                    isCtrlPressed == ScreenTranslatorNeedsCtrl &&
                    isAltPressed == ScreenTranslatorNeedsAlt &&
                    isShiftPressed == ScreenTranslatorNeedsShift)
                {
                    // Вызываем событие асинхронно или через Dispatcher, чтобы не тормозить хук
                    ScreenTranslatorRequested?.Invoke(this, EventArgs.Empty);

                    // Возвращаем 1, чтобы "съесть" нажатие, и игра не открыла свои меню на F10
                    return (IntPtr)1;
                }
            }

            // ⚡ 2. СТАНДАРТНАЯ ЛОГИКА ОТСЛЕЖИВАНИЯ СЛОВ
            if (wParam == (IntPtr)WM_KEYDOWN)
            {
                bool isModifierPressed = isCtrlPressed ||
                                         _pressedModifiers.Contains(0x10) || _pressedModifiers.Contains(0xA0) || _pressedModifiers.Contains(0xA1) ||
                                         _pressedModifiers.Contains(0x12) || _pressedModifiers.Contains(0xA4) || _pressedModifiers.Contains(0xA5);

                if (!isModifierPressed && IsPrintableKey(vkCode, out char boundaryChar))
                {
                    lock (_lock)
                    {
                        _vkBuffer.Add(vkCode);
                        if (boundaryChar == ' ' || boundaryChar == '\n' || boundaryChar == '\r')
                        {
                            LastBoundaryChar = boundaryChar;
                            if (_vkBuffer.Count > _lastWordStart + 1)
                            {
                                var wordVkCodes = _vkBuffer.GetRange(_lastWordStart, _vkBuffer.Count - _lastWordStart);
                                WordCompleted?.Invoke(this, wordVkCodes);
                            }
                            _lastWordStart = _vkBuffer.Count;
                        }
                    }
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private bool IsPrintableKey(int vkCode, out char boundaryChar)
        {
            boundaryChar = '\0';
            if (vkCode == 32) { boundaryChar = ' '; return true; }
            if (vkCode == 13) { boundaryChar = '\n'; return true; }
            if (vkCode >= 65 && vkCode <= 90) return true;
            if (vkCode == 219) return true;
            if (vkCode == 221) return true;
            if (vkCode == 186) return true;
            if (vkCode == 222) return true;
            if (vkCode == 188) return true;
            if (vkCode == 190) return true;
            if (vkCode == 192) return true;
            return false;
        }

        public List<int> GetLastWordVkCodes()
        {
            lock (_lock)
            {
                if (_vkBuffer.Count <= _lastWordStart) return new List<int>();
                return _vkBuffer.GetRange(_lastWordStart, _vkBuffer.Count - _lastWordStart);
            }
        }

        public int GetCurrentBufferLength()
        {
            lock (_lock) { return _vkBuffer.Count; }
        }

        public void Dispose()
        {
            if (_hookId != IntPtr.Zero) { UnhookWindowsHookEx(_hookId); _hookId = IntPtr.Zero; }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);
    }
}