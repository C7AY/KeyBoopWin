using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Windows.System;

namespace KeyBoopWin
{
    public class ScreenTranslatorHotkeyHook : IDisposable
    {
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelKeyboardProc _proc;
        private IntPtr _hookID = IntPtr.Zero;
        private readonly Action _onTriggered;

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        public ScreenTranslatorHotkeyHook(Action onTriggered)
        {
            _onTriggered = onTriggered;
            _proc = HookCallback;
            _hookID = SetHook(_proc);
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                var settings = SettingsManager.Load();

                // Проверяем, включена ли функция и не идет ли запись хоткея в настройках
                if (settings.EnableScreenTranslatorHotkey && !App.IsRecordingHotkey)
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    uint key = (uint)vkCode;

                    // Проверяем модификаторы
                    bool ctrl = (GetAsyncKeyState(0x11) & 0x8000) != 0;
                    bool alt = (GetAsyncKeyState(0x12) & 0x8000) != 0;
                    bool shift = (GetAsyncKeyState(0x10) & 0x8000) != 0;

                    bool modifiersMatch = true;
                    if (settings.ScreenTranslatorHotkeyModifiers.HasFlag(VirtualKeyModifiers.Control)) modifiersMatch &= ctrl; else modifiersMatch &= !ctrl;
                    if (settings.ScreenTranslatorHotkeyModifiers.HasFlag(VirtualKeyModifiers.Menu)) modifiersMatch &= alt; else modifiersMatch &= !alt;
                    if (settings.ScreenTranslatorHotkeyModifiers.HasFlag(VirtualKeyModifiers.Shift)) modifiersMatch &= shift; else modifiersMatch &= !shift;

                    if (modifiersMatch && key == (uint)settings.ScreenTranslatorHotkeyKey)
                    {
                        // Защита от залипания (Debounce)
                        if ((DateTime.Now - App.LastHotkeyChangeTime).TotalMilliseconds > 500)
                        {
                            _onTriggered?.Invoke();
                        }
                    }
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        public void Dispose()
        {
            UnhookWindowsHookEx(_hookID);
        }
    }
}