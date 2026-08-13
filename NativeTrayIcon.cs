using System;
using System.ComponentModel.Design;
using System.Drawing;
using System.Runtime.InteropServices;

namespace KeyBoopWin
{
    public class NativeTrayIcon : IDisposable
    {
        private const uint NIM_ADD = 0x00000000;
        private const uint NIM_MODIFY = 0x00000001;
        private const uint NIM_DELETE = 0x00000002;
        private const uint NIF_ICON = 0x00000002;
        private const uint NIF_TIP = 0x00000004;
        private const uint NIF_MESSAGE = 0x00000001;
        private const uint NIF_INFO = 0x00000010;
        private const uint NIIF_INFO = 0x00000001;

        private const uint WM_LBUTTONDBLCLK = 0x0203;
        private const uint WM_RBUTTONUP = 0x0205;
        private const uint WM_COMMAND = 0x0111;
        private const uint MF_STRING = 0x00000000;
        private const uint MF_SEPARATOR = 0x00000800;
        private const uint TPM_BOTTOMALIGN = 0x0020;
        private const uint TPM_LEFTALIGN = 0x0000;

        private const int ID_OPEN_MAIN = 1001;
        private const int ID_CONVERTER = 1002;
        private const int ID_SETTINGS = 1003;
        private const int ID_EXIT = 1004;
        private const int ID_TRANSLATOR = 1005;
        private const int ID_SCREEN_TRANSLATOR = 1006;
        private const int ID_SLEEP_MODE = 1007;  
        //private const int ID_AUTO_START = 1008;       
        private const int ID_SYMBOLS = 1009;          
        private const int ID_SCREEN_AUDIO = 1010;

        private IntPtr _hWnd;
        private bool _disposed = false;
        private int _uniqueId = 1;
        private WndProcDelegate _wndProcDelegate;

        public event EventHandler? OpenMainWindowRequested;
        public event EventHandler? OpenConverterRequested;
        public event EventHandler? OpenSettingsRequested;
        public event EventHandler? ExitRequested;
        public event EventHandler? OpenTranslatorRequested;
        public event EventHandler? OpenScreenTranslatorRequested;
        public event EventHandler? ToggleSleepModeRequested;   
        //public event EventHandler? ToggleAutoStartRequested;      
        public event EventHandler? OpenSymbolsRequested;
        public event EventHandler? OpenScreenAudioRequested;

        public NativeTrayIcon(Icon icon, string toolTip)
        {
            _wndProcDelegate = WndProc;
            CreateHiddenWindow();
            AddTrayIcon(icon, toolTip);
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWindowEx(int dwExStyle, string lpClassName, string lpWindowName, int dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpdata);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        private static extern bool TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out Point lpPoint);

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASSEX
        {
            public uint cbSize;
            public uint style;
            public WndProcDelegate lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string lpszMenuName;
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
            public uint uVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
            public uint dwInfoFlags;
        }

        private IntPtr WndProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam)
        {
            const uint WM_TRAYICON = 0x0400;
            if (uMsg == WM_TRAYICON)
            {
                int lParamInt = lParam.ToInt32();
                if (lParamInt == WM_LBUTTONDBLCLK)
                {
                    OpenMainWindowRequested?.Invoke(this, EventArgs.Empty);
                }
                else if (lParamInt == WM_RBUTTONUP)
                {
                    ShowContextMenu();
                }
            }
            else if (uMsg == WM_COMMAND)
            {
                int commandId = wParam.ToInt32();
                if (commandId == ID_OPEN_MAIN) OpenMainWindowRequested?.Invoke(this, EventArgs.Empty);
                else if (commandId == ID_CONVERTER) OpenConverterRequested?.Invoke(this, EventArgs.Empty);
                else if (commandId == ID_SETTINGS) OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
                else if (commandId == ID_EXIT) ExitRequested?.Invoke(this, EventArgs.Empty);
                else if (commandId == ID_TRANSLATOR) OpenTranslatorRequested?.Invoke(this, EventArgs.Empty);
                else if (commandId == ID_SCREEN_TRANSLATOR) OpenScreenTranslatorRequested?.Invoke(this, EventArgs.Empty);
                else if (commandId == ID_SLEEP_MODE) ToggleSleepModeRequested?.Invoke(this, EventArgs.Empty);      
                else if (commandId == ID_SYMBOLS) OpenSymbolsRequested?.Invoke(this, EventArgs.Empty);              
                //else if (commandId == ID_AUTO_START) ToggleAutoStartRequested?.Invoke(this, EventArgs.Empty);     
                else if (commandId == ID_SCREEN_AUDIO) OpenScreenAudioRequested?.Invoke(this, EventArgs.Empty); 
            }
            return DefWindowProc(hWnd, uMsg, wParam, lParam);
        }

        private void ShowContextMenu()
        {
            IntPtr hMenu = CreatePopupMenu();

            AppendMenu(hMenu, MF_STRING, ID_OPEN_MAIN, "🎤 Голосовой ввод");
            AppendMenu(hMenu, MF_STRING, ID_TRANSLATOR, "🌐 Переводчик");
            AppendMenu(hMenu, MF_STRING, ID_CONVERTER, "🔤 Конвертер регистров");
            AppendMenu(hMenu, MF_STRING, ID_SYMBOLS, "🔣 Специальные символы");

            AppendMenu(hMenu, MF_SEPARATOR, 0, "");

            AppendMenu(hMenu, MF_STRING, ID_SCREEN_TRANSLATOR, "📺 Экранный переводчик");
            AppendMenu(hMenu, MF_STRING, ID_SCREEN_AUDIO, "🔊 Экранный диктор"); 
                       
            AppendMenu(hMenu, MF_SEPARATOR, 0, "");

            var settings = SettingsManager.Load();
            string sleepModeText = settings.IsSleepMode ? "💤 Выйти из спящего режима" : "💤 Спящий режим";
            string autoStartText = settings.AutoStart ? "❌ Отключить автозагрузку" : "✅ Включить автозагрузку";

            AppendMenu(hMenu, MF_STRING, ID_SLEEP_MODE, sleepModeText);
            //AppendMenu(hMenu, MF_STRING, ID_AUTO_START, autoStartText);
            AppendMenu(hMenu, MF_SEPARATOR, 0, "");

            AppendMenu(hMenu, MF_STRING, ID_SETTINGS, "⚙️ Настройки");
            AppendMenu(hMenu, MF_STRING, ID_EXIT, "❌ Закрыть");

            GetCursorPos(out Point p);
            TrackPopupMenu(hMenu, TPM_BOTTOMALIGN | TPM_LEFTALIGN, p.X, p.Y, 0, _hWnd, IntPtr.Zero);
            DestroyMenu(hMenu);
        }

        public void ShowBalloonTip(string title, string text)
        {
            var nid = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hWnd,
                uID = (uint)_uniqueId,
                uFlags = NIF_INFO,
                szInfoTitle = title,
                szInfo = text,
                dwInfoFlags = NIIF_INFO
            };
            Shell_NotifyIcon(NIM_MODIFY, ref nid);
        }

        private void CreateHiddenWindow()
        {
            var wc = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                lpfnWndProc = _wndProcDelegate,
                lpszClassName = "KeyBoopTrayIconClass",
                hInstance = IntPtr.Zero
            };
            RegisterClassEx(ref wc);
            _hWnd = CreateWindowEx(0, "KeyBoopTrayIconClass", "KeyBoopTray", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        }

        private void AddTrayIcon(Icon icon, string toolTip)
        {
            var nid = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hWnd,
                uID = (uint)_uniqueId,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = 0x0400,
                hIcon = icon.Handle,
                szTip = toolTip
            };
            Shell_NotifyIcon(NIM_ADD, ref nid);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                var nid = new NOTIFYICONDATA
                {
                    cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                    hWnd = _hWnd,
                    uID = (uint)_uniqueId
                };
                Shell_NotifyIcon(NIM_DELETE, ref nid);
                DestroyWindow(_hWnd);
                _disposed = true;
            }
        }
    }
}