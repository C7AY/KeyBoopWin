using System;
using System.Runtime.InteropServices;
using Windows.System;

namespace KeyBoopWin
{
    public class ScreenAudioHotkeyHook : IDisposable
    {
        private readonly IntPtr _windowHandle;
        private readonly int _hotkeyId;
        private readonly Action _onTriggered;
        private static int _idCounter = 9002;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const uint MOD_NOREPEAT = 0x4000;

        public ScreenAudioHotkeyHook(IntPtr windowHandle, Action onTriggered)
        {
            _windowHandle = windowHandle;
            _hotkeyId = _idCounter++;
            _onTriggered = onTriggered;

            var settings = SettingsManager.Load();
            if (!settings.IsSleepMode && !App.IsRecordingHotkey && settings.EnableScreenAudioHotkey)
            {
                RegisterHotKey(_windowHandle, _hotkeyId, MOD_NOREPEAT, (uint)settings.ScreenAudioHotkeyKey);
            }
        }

        public bool CheckMessage(int id)
        {
            if (id == _hotkeyId)
            {
                if ((DateTime.Now - App.LastHotkeyChangeTime).TotalMilliseconds > 500)
                {
                    _onTriggered?.Invoke();
                    return true;
                }
            }
            return false;
        }

        public void Dispose()
        {
            UnregisterHotKey(_windowHandle, _hotkeyId);
        }
    }
}