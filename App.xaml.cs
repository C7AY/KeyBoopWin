using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Microsoft.Win32;
using WinRT.Interop;

namespace KeyBoopWin
{
    public partial class App : Application
    {
        private Window? _window;
        private NativeTrayIcon? _trayIcon;
        public GlobalKeyboardHook? KeyboardHook { get; private set; }
        private LayoutCorrector _corrector = new LayoutCorrector();
        private bool _isCorrecting = false;
        private bool _forceClose = false;

        // ⚡ ЭКРАННЫЙ ПЕРЕВОДЧИК: ОДИН экземпляр на все время работы
        private ScreenTranslatorWindow? _screenTranslatorWindow;

        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_INJECTED = 0x0010;
        private const uint CF_UNICODETEXT = 13;
        private const uint GMEM_MOVEABLE = 0x0002;
        private const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;

        public App() { this.InitializeComponent(); }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            DictionaryManager.Initialize();
            _window = new MainWindow();
            _window.AppWindow.Closing += Window_Closing;

            Icon customIcon = CreateSimpleIcon();
            _trayIcon = new NativeTrayIcon(customIcon, "KeyBoopWin");

            _trayIcon.OpenMainWindowRequested += (s, e) => _window?.AppWindow.Show();
            _trayIcon.OpenSettingsRequested += (s, e) => OpenSettings();
            _trayIcon.OpenConverterRequested += (s, e) => new ConverterWindow().Activate();
            _trayIcon.OpenTranslatorRequested += (s, e) => new TranslatorWindow().Activate();

            _trayIcon.OpenScreenTranslatorRequested += (s, e) => ActivateScreenTranslator();

            // ⚡ ПУНКТЫ ТРЕЯ: Спящий режим и Автозагрузка
            _trayIcon.ToggleSleepModeRequested += (s, e) => ToggleSleepMode();
            //_trayIcon.ToggleAutoStartRequested += (s, e) => ToggleAutoStart();

            _trayIcon.ExitRequested += (s, e) =>
            {
                _forceClose = true;
                _window?.Close();
            };

            KeyboardHook = new GlobalKeyboardHook();
            KeyboardHook.WordCompleted += OnWordCompleted;
            KeyboardHook.ManualConvertRequested += OnManualConvertRequested;

            // ⚡ ИНИЦИАЛИЗАЦИЯ ЭКРАННОГО ПЕРЕВОДЧИКА
            _screenTranslatorWindow = new ScreenTranslatorWindow();

            // ⚡ ПРОВЕРКА СПЯЩЕГО РЕЖИМА ПРИ ЗАПУСКЕ (исправлено дублирование)
            var settings = SettingsManager.Load();
            if (settings.IsSleepMode)
            {
                KeyboardHook?.SetEnabled(false);
                System.Diagnostics.Debug.WriteLine("💤 Приложение запущено в спящем режиме");
            }

            System.Diagnostics.Debug.WriteLine("✅ KeyBoop запущен");
            _window.Activate();
        }

        // ⚡ Активация экранного переводчика (с проверкой спящего режима)
        public void ActivateScreenTranslator()
        {
            var settings = SettingsManager.Load();
            if (settings.IsSleepMode)
            {
                System.Diagnostics.Debug.WriteLine("💤 Приложение в спящем режиме. Экранный переводчик не активирован.");
                return;
            }

            if (_screenTranslatorWindow != null)
            {
                _ = _screenTranslatorWindow.TriggerScreenTranslationAsync();
            }
        }

        // ⚡ Переключение спящего режима
        public void ToggleSleepMode()
        {
            var settings = SettingsManager.Load();
            settings.IsSleepMode = !settings.IsSleepMode;
            SettingsManager.Save(settings);

            KeyboardHook?.SetEnabled(!settings.IsSleepMode);

            string status = settings.IsSleepMode ? "💤 Спящий режим АКТИВИРОВАН" : "✅ Спящий режим ОТКЛЮЧЕН";
            System.Diagnostics.Debug.WriteLine(status);

            _trayIcon?.ShowBalloonTip("KeyBoopWin", settings.IsSleepMode ?
                "Приложение приостановлено (спящий режим)" :
                "Приложение активно");
        }

        // ⚡ Переключение автозагрузки
        public void ToggleAutoStart()
        {
            var settings = SettingsManager.Load();
            settings.AutoStart = !settings.AutoStart;
            SettingsManager.Save(settings);

            SettingsManager.SetAutoStart(settings.AutoStart);

            _trayIcon?.ShowBalloonTip("KeyBoopWin", settings.AutoStart ?
                "Автозагрузка ВКЛЮЧЕНА" :
                "Автозагрузка ОТКЛЮЧЕНА");
        }

        private void Window_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
        {
            if (!_forceClose)
            {
                args.Cancel = true;
                _window?.AppWindow.Hide();

                var settings = SettingsManager.Load();
                if (!settings.HasSeenTrayNotification)
                {
                    _trayIcon?.ShowBalloonTip("KeyBoopWin", "Приложение свернуто в трей. Чтобы выйти полностью, используйте меню иконки.");
                    settings.HasSeenTrayNotification = true;
                    SettingsManager.Save(settings);
                }
            }
            else
            {
                KeyboardHook?.Dispose();
                _trayIcon?.Dispose();
                _screenTranslatorWindow?.Close();
            }
        }

        public void OpenSettings() => new SettingsWindow().Activate();

        private async Task SendMultipleBackspaces(int count)
        {
            if (count <= 0) return;
            var inputs = new INPUT[count * 2];
            for (int i = 0; i < count; i++)
            {
                inputs[i * 2].type = 1;
                inputs[i * 2].U.ki.wVk = 0x08;
                inputs[i * 2].U.ki.dwFlags = KEYEVENTF_INJECTED;
                inputs[i * 2 + 1].type = 1;
                inputs[i * 2 + 1].U.ki.wVk = 0x08;
                inputs[i * 2 + 1].U.ki.dwFlags = KEYEVENTF_KEYUP | KEYEVENTF_INJECTED;
            }
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            await Task.Delay(30);
        }

        private async void OnManualConvertRequested(object? sender, bool toRussian)
        {
            var settings = SettingsManager.Load();
            if (settings.IsSleepMode || _isCorrecting) return;

            _isCorrecting = true;
            try
            {
                System.Diagnostics.Debug.WriteLine($"🔄 Ручная конвертация: в {(toRussian ? "RU" : "EN")}");
                string originalClipboard = GetClipboardTextWin32() ?? string.Empty;
                string clipboardBeforeCopy = originalClipboard;
                await SimulateCopy();
                string selectedText = GetClipboardTextWin32() ?? "";

                if (!string.IsNullOrWhiteSpace(selectedText) && selectedText != clipboardBeforeCopy)
                {
                    string textToConvert = selectedText.Trim();
                    System.Diagnostics.Debug.WriteLine($"✅ Найден выделенный текст: '{textToConvert}'");

                    string convertedText = LayoutCorrector.ConvertLayout(textToConvert, toRussian);
                    if (convertedText == textToConvert)
                    {
                        System.Diagnostics.Debug.WriteLine("⚠️ Текст не изменился после конвертации");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"🔄 Конвертация: '{textToConvert}' → '{convertedText}'");
                        SetClipboardTextWin32(convertedText);
                        await SendKeyCombo(0x11, false, false);
                        await SendKeyCombo(0x56, true, false);
                        await SendKeyCombo(0x56, true, true);
                        await SendKeyCombo(0x11, false, true);
                        await Task.Delay(50);

                        if (!string.IsNullOrEmpty(originalClipboard))
                        {
                            SetClipboardTextWin32(originalClipboard);
                        }
                        System.Diagnostics.Debug.WriteLine("🎉 Успешная конвертация!");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ Текст не выделен.");
                    if (!string.IsNullOrEmpty(originalClipboard)) SetClipboardTextWin32(originalClipboard);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка: {ex.Message}");
            }
            finally
            {
                _isCorrecting = false;
            }
        }

        private async Task SendKeyCombo(ushort vk, bool isInjected, bool isKeyUp = false)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = 1;
            inputs[0].U.ki.wVk = vk;
            uint flags = (isKeyUp ? KEYEVENTF_KEYUP : 0) | (isInjected ? KEYEVENTF_INJECTED : 0);
            inputs[0].U.ki.dwFlags = flags;
            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
            await Task.Delay(20);
        }

        private async Task SimulateCopy()
        {
            await SendKeyCombo(0x11, false, false);
            await SendKeyCombo(0x43, true, false);
            await SendKeyCombo(0x43, true, true);
            await SendKeyCombo(0x11, false, true);
            await Task.Delay(100);
        }

        private void OnWordCompleted(object? sender, List<int> vkCodes)
        {
            var settings = SettingsManager.Load();
            if (settings.IsSleepMode || _isCorrecting) return;

            if (KeyboardHook == null || vkCodes == null || vkCodes.Count < 2) return;
            int expectedLength = KeyboardHook.GetCurrentBufferLength();

            IntPtr hWnd = GetForegroundWindow();
            GetWindowThreadProcessId(hWnd, out uint processId);
            if (processId == Process.GetCurrentProcess().Id) return;

            uint threadId = GetWindowThreadProcessId(hWnd, out _);
            IntPtr currentLayout = GetKeyboardLayout(threadId);
            if (currentLayout == IntPtr.Zero) currentLayout = LoadKeyboardLayout("00000419", 0x00000001);

            uint currentLayoutId = (uint)currentLayout & 0xFFFF;
            IntPtr altLayout = (currentLayoutId == 0x0419)
                ? LoadKeyboardLayout("00000409", 0x00000001)
                : LoadKeyboardLayout("00000419", 0x00000001);

            string strCurrent = _corrector.VkCodesToString(vkCodes, currentLayout) ?? "";
            string strAlt = _corrector.VkCodesToString(vkCodes, altLayout) ?? "";
            string cleanCurrent = new string(strCurrent.Where(c => char.IsLetter(c) || c == '[' || c == ']' || c == ';' || c == '\'' || c == ',' || c == '.' || c == '`').ToArray());
            string cleanAlt = new string(strAlt.Where(c => char.IsLetter(c) || c == '[' || c == ']' || c == ';' || c == '\'' || c == ',' || c == '.' || c == '`').ToArray());

            if (cleanCurrent.Length < 2) return;

            if (_corrector.LooksLikeWrongLayout(cleanCurrent, cleanAlt, out string? finalCorrectedWord, out bool needsLayoutSwitch))
            {
                _isCorrecting = true;
                Task.Run(async () =>
                {
                    try
                    {
                        char boundary = KeyboardHook.LastBoundaryChar != '\0' ? KeyboardHook.LastBoundaryChar : ' ';
                        string wordToInsert = finalCorrectedWord ?? cleanAlt;
                        int charsToDelete = cleanCurrent.Length + 1;

                        for (int i = 0; i < charsToDelete; i++)
                        {
                            if (KeyboardHook.GetCurrentBufferLength() != expectedLength) return;
                            await SendKeyCombo(0x08, true, false);
                            await SendKeyCombo(0x08, true, true);
                            await Task.Delay(10);
                        }

                        if (needsLayoutSwitch && altLayout != IntPtr.Zero)
                        {
                            ActivateKeyboardLayout(altLayout, 0x00000001);
                            IntPtr targetHWnd = GetForegroundWindow();
                            if (targetHWnd != IntPtr.Zero)
                                PostMessage(targetHWnd, WM_INPUTLANGCHANGEREQUEST, (IntPtr)1, altLayout);
                            await Task.Delay(50);
                        }

                        string textToInsert = wordToInsert + boundary;
                        string? originalClipboard = GetClipboardTextWin32();
                        if (SetClipboardTextWin32(textToInsert))
                        {
                            await SendKeyCombo(0x11, false, false);
                            await SendKeyCombo(0x56, true, false);
                            await SendKeyCombo(0x56, true, true);
                            await SendKeyCombo(0x11, false, true);
                            await Task.Delay(50);
                            if (originalClipboard != null) SetClipboardTextWin32(originalClipboard);
                        }
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"❌ Ошибка: {ex.Message}"); }
                    finally { _isCorrecting = false; }
                });
            }
        }

        private Icon CreateSimpleIcon()
        {
            Bitmap bitmap = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.FromArgb(0, 120, 215));
                using (Font font = new Font("Arial", 20, FontStyle.Bold))
                using (Brush brush = new SolidBrush(Color.White))
                {
                    g.DrawString("K", font, brush, 6, 4);
                }
            }
            return Icon.FromHandle(bitmap.GetHicon());
        }

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern IntPtr GetKeyboardLayout(uint idThread);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint flags);
        [DllImport("user32.dll")] private static extern IntPtr ActivateKeyboardLayout(IntPtr hkl, uint flags);
        [DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr hWndNewOwner);
        [DllImport("user32.dll")] private static extern bool CloseClipboard();
        [DllImport("user32.dll")] private static extern bool EmptyClipboard();
        [DllImport("user32.dll")] private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
        [DllImport("user32.dll")] private static extern IntPtr GetClipboardData(uint uFormat);
        [DllImport("kernel32.dll")] private static extern IntPtr GlobalAlloc(uint uFlags, IntPtr dwBytes);
        [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr hMem);
        [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr hMem);
        [DllImport("kernel32.dll")] private static extern IntPtr GlobalFree(IntPtr hMem);

        private string? GetClipboardTextWin32()
        {
            if (!OpenClipboard(IntPtr.Zero)) return null;
            try
            {
                IntPtr hMem = GetClipboardData(CF_UNICODETEXT);
                if (hMem == IntPtr.Zero) return null;
                IntPtr pMem = GlobalLock(hMem);
                if (pMem == IntPtr.Zero) return null;
                string text = Marshal.PtrToStringUni(pMem) ?? "";
                GlobalUnlock(hMem);
                return text;
            }
            finally { CloseClipboard(); }
        }

        private bool SetClipboardTextWin32(string text)
        {
            if (string.IsNullOrEmpty(text) || !OpenClipboard(IntPtr.Zero)) return false;
            try
            {
                EmptyClipboard();
                IntPtr hMem = GlobalAlloc(GMEM_MOVEABLE, (IntPtr)((text.Length + 1) * 2));
                if (hMem == IntPtr.Zero) return false;
                IntPtr pMem = GlobalLock(hMem);
                if (pMem == IntPtr.Zero) { GlobalFree(hMem); return false; }
                Marshal.Copy(text.ToCharArray(), 0, pMem, text.Length);
                Marshal.WriteInt16(pMem, text.Length * 2, 0);
                GlobalUnlock(hMem);
                return SetClipboardData(CF_UNICODETEXT, hMem) != IntPtr.Zero;
            }
            finally { CloseClipboard(); }
        }

        [StructLayout(LayoutKind.Sequential)] private struct INPUT { public uint type; public InputUnion U; }
        [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; [FieldOffset(0)] public HARDWAREINPUT hi; }
        [StructLayout(LayoutKind.Sequential)] private struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)] private struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)] private struct HARDWAREINPUT { public uint uMsg; public ushort wParamL; public ushort wParamH; }
        [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    }
}