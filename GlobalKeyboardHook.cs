using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KeyBoopWin
{
    public class GlobalKeyboardHook : IDisposable
    {
        public int ConvertToRuKey { get; set; } = 219;
        public int ConvertToEnKey { get; set; } = 221;

        // ⚡ ФЛАГ ВКЛЮЧЕНИЯ/ВЫКЛЮЧЕНИЯ ХУКА
        private bool _isEnabled = true;

        public event EventHandler<bool>? ManualConvertRequested;

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int LLKHF_INJECTED = 0x00000010;

        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelKeyboardProc _proc;
        private readonly HashSet<int> _pressedKeys = new HashSet<int>();
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

        // ⚡ НОВЫЙ МЕТОД: Включение/выключение хука
        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
            System.Diagnostics.Debug.WriteLine($" GlobalKeyboardHook: {(enabled ? "ВКЛЮЧЕН" : "ВЫКЛЮЧЕН")}");
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

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            // ⚡ ПРОВЕРКА: если хук выключен (спящий режим), пропускаем всё дальше
            if (!_isEnabled)
            {
                return CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            if (nCode < 0) return CallNextHookEx(_hookId, nCode, wParam, lParam);

            int vkCode = Marshal.ReadInt32(lParam);
            int flags = Marshal.ReadInt32(lParam, 8);

            if ((flags & LLKHF_INJECTED) != 0)
                return CallNextHookEx(_hookId, nCode, wParam, lParam);

            // ⚡ 1. ПРОВЕРКА РУЧНЫХ ГОРЯЧИХ КЛАВИШ (Ctrl + клавиша)
            bool isCtrlPressed = (_pressedKeys.Contains(0x11) || _pressedKeys.Contains(0xA2) || _pressedKeys.Contains(0xA3));

            if (wParam == (IntPtr)WM_KEYDOWN && isCtrlPressed)
            {
                if (vkCode == ConvertToRuKey)
                {
                    ManualConvertRequested?.Invoke(this, true);
                    return (IntPtr)1; // Блокируем передачу в систему
                }
                if (vkCode == ConvertToEnKey)
                {
                    ManualConvertRequested?.Invoke(this, false);
                    return (IntPtr)1; // Блокируем передачу в систему
                }
            }

            // ⚡ 2. СТАНДАРТНАЯ ЛОГИКА ОТСЛЕЖИВАНИЯ СЛОВ
            if (wParam == (IntPtr)WM_KEYDOWN)
            {
                if (!_pressedKeys.Contains(vkCode))
                {
                    _pressedKeys.Add(vkCode);

                    bool isModifierPressed = _pressedKeys.Contains(0x11) ||
                                             _pressedKeys.Contains(0xA2) ||
                                             _pressedKeys.Contains(0xA3) ||
                                             _pressedKeys.Contains(0x12) ||
                                             _pressedKeys.Contains(0xA4) ||
                                             _pressedKeys.Contains(0xA5);

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
            }
            else if (wParam == (IntPtr)0x0101) // WM_KEYUP
            {
                _pressedKeys.Remove(vkCode);
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
    }
}