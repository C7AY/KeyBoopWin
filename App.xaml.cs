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
using System.IO;
using Microsoft.UI.Dispatching;

namespace KeyBoopWin
{
    public partial class App : Application
    {
        private SymbolsHotkeyHook? _symbolsHotkeyHook;
        private static Microsoft.UI.Dispatching.DispatcherQueue? _dispatcherQueue;
        private Window? _window;
        private NativeTrayIcon? _trayIcon;

        public Window MainWindow { get; set; }
        public GlobalKeyboardHook? KeyboardHook { get; private set; }
        private LayoutCorrector _corrector = new LayoutCorrector();
        private bool _isCorrecting = false;
        private bool _forceClose = false;
        private List<Window> _allWindows = new List<Window>();
        private ScreenTranslatorWindow? _screenTranslatorWindow;
        private SymbolsWindow? _symbolsWindow; 
        public static bool IsRecordingHotkey { get; set; } = false;
        public static DateTime LastHotkeyChangeTime { get; set; } = DateTime.MinValue;
        //private ScreenTranslatorHotkeyHook? _screenTranslatorHook;
        private ScreenAudioService? _screenAudioService;
        private ScreenAudioHotkeyHook? _screenAudioHotkeyHook;

        // ⚡ ЗАЩИТА ОТ ПОВТОРНЫХ НАЖАТИЙ (Debounce)
        private DateTime _lastManualConvertTime = DateTime.MinValue;
        private const int MANUAL_CONVERT_COOLDOWN_MS = 500;

        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_INJECTED = 0x0010;
        private const uint CF_UNICODETEXT = 13;
        private const uint GMEM_MOVEABLE = 0x0002;
        private const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;

        public App() 
        {

            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            this.InitializeComponent();
            _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        }

       

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            var settings = SettingsManager.Load();
            if (!settings.IsSleepMode && !settings.DisableAutoLayoutCorrection)
            {
                DictionaryManager.Initialize();
            }
            else
            {
                // Если включен спящий режим или отключена автокоррекция, держим словари выгруженными
                DictionaryManager.UnloadDictionaries();
            }

            MainWindow = new MainWindow();
            _window = MainWindow;

            // Используем уже созданный экземпляр MainWindow для трея и иконки
            _window = MainWindow;

            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(iconPath))
            {
                _window.AppWindow.SetIcon(iconPath);
                System.Diagnostics.Debug.WriteLine($"✅ [MainWindow] Иконка установлена.");
            }

            _symbolsHotkeyHook = new SymbolsHotkeyHook(() =>
            {
                var settings = SettingsManager.Load();
                if (!settings.IsSleepMode)
                {
                    OpenSymbolsWindow();
                }
            });



            _allWindows.Add(_window);
            _window.Closed += (sender, args) => _allWindows.Remove(_window);
            _window.AppWindow.Closing += Window_Closing;

            Icon customIcon = CreateSimpleIcon();
            _trayIcon = new NativeTrayIcon(customIcon, "KeyBoopWin");

            _trayIcon.OpenMainWindowRequested += (s, e) => _window?.AppWindow.Show();
            _trayIcon.OpenSettingsRequested += (s, e) => OpenSettings();

            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow); // Получаем HWND главного окна

            _screenAudioService = new ScreenAudioService();
            _screenAudioHotkeyHook = new ScreenAudioHotkeyHook(hwnd, async () =>
            {
                if (_screenAudioService != null)
                {
                    await _screenAudioService.TriggerScreenAudioAsync();
                }
            });


            _trayIcon.OpenScreenAudioRequested += async (s, e) =>
            {


                var settings = SettingsManager.Load();
                if (settings.IsSleepMode)
                {
                    System.Diagnostics.Debug.WriteLine("💤 Приложение в спящем режиме. Озвучка не активирована.");
                    return;
                }

                if (_screenAudioService != null)
                {
                    await _screenAudioService.TriggerScreenAudioAsync();
                }
            };

            _trayIcon.OpenConverterRequested += (s, e) =>
            {
                var window = new ConverterWindow();
                _allWindows.Add(window);
                window.Closed += (sender, args) => _allWindows.Remove(window);

                window.Activate();

                string currentIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
                if (File.Exists(currentIconPath))
                {
                    window.AppWindow.SetIcon(currentIconPath);
                }
            };

            _trayIcon.OpenTranslatorRequested += (s, e) =>
            {
                var window = new TranslatorWindow();
                _allWindows.Add(window);
                window.Closed += (sender, args) => _allWindows.Remove(window);

                window.Activate();

                string currentIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
                if (File.Exists(currentIconPath))
                {
                    window.AppWindow.SetIcon(currentIconPath);
                }
            };

            // ⚡ ПОДПИСКА НА СОБЫТИЕ ОТКРЫТИЯ ТАБЛИЦЫ СИМВОЛОВ
            _trayIcon.OpenSymbolsRequested += (s, e) => OpenSymbolsWindow();

            _trayIcon.OpenScreenTranslatorRequested += (s, e) => ActivateScreenTranslator();

            _trayIcon.ToggleSleepModeRequested += (s, e) => ToggleSleepMode();

            _trayIcon.ExitRequested += (s, e) =>
            {
                _forceClose = true;
                foreach (var window in _allWindows.ToList())
                {
                    window.Close();
                }
                _window?.Close();
            };

            KeyboardHook = new GlobalKeyboardHook();
            KeyboardHook.WordCompleted += OnWordCompleted;
            KeyboardHook.ManualConvertRequested += OnManualConvertRequested;

            // Читаем настройки и передаем их в хук
            var currentSettings = SettingsManager.Load();
            KeyboardHook.EnableScreenTranslator = currentSettings.EnableScreenTranslatorHotkey;

            if (currentSettings.EnableScreenTranslatorHotkey)
            {
                KeyboardHook.ScreenTranslatorKey = (int)currentSettings.ScreenTranslatorHotkeyKey;
                KeyboardHook.ScreenTranslatorNeedsCtrl = currentSettings.ScreenTranslatorHotkeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Control);
                KeyboardHook.ScreenTranslatorNeedsAlt = currentSettings.ScreenTranslatorHotkeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Menu);
                KeyboardHook.ScreenTranslatorNeedsShift = currentSettings.ScreenTranslatorHotkeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Shift);
            }

            // Подписываемся на глобальное событие
            KeyboardHook.ScreenTranslatorRequested += (s, e) =>
            {
                // Обязательно переводим выполнение в UI-поток приложения
                _dispatcherQueue?.TryEnqueue(() =>
                {
                    ActivateScreenTranslator();
                });
            };

            _screenTranslatorWindow = new ScreenTranslatorWindow();
            _allWindows.Add(_screenTranslatorWindow);
            _screenTranslatorWindow.Closed += (sender, args) => _allWindows.Remove(_screenTranslatorWindow);

            
            if (settings.IsSleepMode)
            {
                KeyboardHook?.SetEnabled(false);
                System.Diagnostics.Debug.WriteLine("💤 Приложение запущено в спящем режиме");
            }
        }

        public bool CheckHotkeysMessage(int wParamInt)
        {
           

            // Проверяем экранную озвучку
            if (_screenAudioHotkeyHook != null && _screenAudioHotkeyHook.CheckMessage(wParamInt))
                return true;


            return false;
        }

        // ⚡ МЕТОД ОТКРЫТИЯ ОКНА СПЕЦСИМВОЛОВ
        public void OpenSymbolsWindow()
        {
            // Оборачиваем в Dispatcher, чтобы предотвратить баги потоков WinUI 3
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(() =>
            {
                if (_symbolsWindow == null)
                {
                    _symbolsWindow = new SymbolsWindow();
                    _allWindows.Add(_symbolsWindow);

                    _symbolsWindow.Closed += (sender, args) =>
                    {
                        _allWindows.Remove(_symbolsWindow);
                        _symbolsWindow = null;
                    };
                }

                _symbolsWindow.Activate();

                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
                if (File.Exists(iconPath))
                {
                    _symbolsWindow.AppWindow.SetIcon(iconPath);
                }
            });
        }

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

        public void ToggleSleepMode()
        {
            var settings = SettingsManager.Load();
            settings.IsSleepMode = !settings.IsSleepMode;
            SettingsManager.Save(settings);

            KeyboardHook?.SetEnabled(!settings.IsSleepMode);

            if (_window is MainWindow mainWindow)
            {
                if (settings.IsSleepMode)
                {
                    mainWindow.UnloadSpeechModel();
                    DictionaryManager.UnloadDictionaries();
                    System.Diagnostics.Debug.WriteLine("💤 Спящий режим: модель и словари выгружены из RAM");
                }
                else
                {
                    mainWindow.LoadSpeechModel();
                    DictionaryManager.LoadDictionaries();
                    System.Diagnostics.Debug.WriteLine("✅ Пробуждение: модель и словари загружены в RAM");
                }
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            string status = settings.IsSleepMode ? "💤 Спящий режим АКТИВИРОВАН" : "✅ Спящий режим ОТКЛЮЧЕН";
            System.Diagnostics.Debug.WriteLine(status);
        }

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
                _symbolsWindow?.Close();
                _screenAudioHotkeyHook?.Dispose();
                _screenAudioService?.Dispose();
                _symbolsHotkeyHook?.Dispose();
            }
        }

        public void OpenSettings()
        {
            var settingsWindow = new SettingsWindow();
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(iconPath)) settingsWindow.AppWindow.SetIcon(iconPath);
            _allWindows.Add(settingsWindow);
            settingsWindow.Closed += (sender, args) => _allWindows.Remove(settingsWindow);
            settingsWindow.Activate();
        }

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
            var now = DateTime.Now;
            if ((now - _lastManualConvertTime).TotalMilliseconds < MANUAL_CONVERT_COOLDOWN_MS)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Пропуск повторного нажатия (прошло {(now - _lastManualConvertTime).TotalMilliseconds}мс)");
                return;
            }
            _lastManualConvertTime = now;

            var settings = SettingsManager.Load();

            System.Diagnostics.Debug.WriteLine($"🔍 Проверка настроек: IsSleepMode={settings.IsSleepMode}, EnableManualFixHotkeys={settings.EnableManualFixHotkeys}, _isCorrecting={_isCorrecting}");

            if (settings.IsSleepMode || _isCorrecting || !settings.EnableManualFixHotkeys)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Ручное исправление проигнорировано. Причина: IsSleepMode={settings.IsSleepMode}, EnableManualFixHotkeys={settings.EnableManualFixHotkeys}");
                return;
            }

            _isCorrecting = true;
            KeyboardHook?.Suspend(true);
            try
            {
                System.Diagnostics.Debug.WriteLine($"🔄 Ручная конвертация: в {(toRussian ? "RU" : "EN")}");
                string originalClipboard = GetClipboardTextWin32() ?? string.Empty;
                string clipboardBeforeCopy = originalClipboard;

                await Task.Delay(50);
                await SimulateCopy();
                await Task.Delay(150);

                string selectedText = GetClipboardTextWin32() ?? "";
                System.Diagnostics.Debug.WriteLine($"📋 Буфер обмена: '{selectedText}' (длина: {selectedText.Length})");

                if (!string.IsNullOrWhiteSpace(selectedText) && selectedText != clipboardBeforeCopy)
                {
                    string textToConvert = selectedText.Trim();
                    System.Diagnostics.Debug.WriteLine($"✅ Найден выделенный текст: '{textToConvert}'");

                    string convertedText = LayoutCorrector.ConvertLayout(textToConvert, toRussian);
                    System.Diagnostics.Debug.WriteLine($"🔄 Конвертация: '{textToConvert}' → '{convertedText}'");

                    if (convertedText == textToConvert)
                    {
                        System.Diagnostics.Debug.WriteLine("⚠️ Текст не изменился после конвертации (возможно, уже в правильной раскладке)");
                    }
                    else
                    {
                        SetClipboardTextWin32(convertedText);
                        await SendKeyCombo(0x11, false, false); // Ctrl Down
                        await SendKeyCombo(0x56, true, false);  // V Down
                        await SendKeyCombo(0x56, true, true);   // V Up
                        await SendKeyCombo(0x11, false, true);  // Ctrl Up
                        await Task.Delay(100);

                        System.Diagnostics.Debug.WriteLine("🎉 Успешная конвертация!");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ Текст не выделен или буфер не изменился");
                }

                if (!string.IsNullOrEmpty(originalClipboard))
                {
                    await Task.Delay(50);
                    SetClipboardTextWin32(originalClipboard);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка: {ex.Message}");
            }
            finally
            {
                KeyboardHook?.Suspend(false);
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
            await SendKeyCombo(0x11, false, false); // Ctrl Down
            await SendKeyCombo(0x43, true, false);  // C Down
            await SendKeyCombo(0x43, true, true);   // C Up
            await SendKeyCombo(0x11, false, true);  // Ctrl Up
            await Task.Delay(100);
        }

        private void OnWordCompleted(object? sender, List<int> vkCodes)
        {
            var settings = SettingsManager.Load();
            if (settings.IsSleepMode || _isCorrecting || settings.DisableAutoLayoutCorrection) return;
            if (KeyboardHook == null || vkCodes == null || vkCodes.Count < 2) return;

            if (vkCodes.Count < 2 || vkCodes.Count > 20) return;

            IntPtr hWnd = GetForegroundWindow();
            GetWindowThreadProcessId(hWnd, out uint processId);
            if (processId == Process.GetCurrentProcess().Id) return;

            Task.Run(() =>
            {
                uint threadId = GetWindowThreadProcessId(hWnd, out _);
                IntPtr currentLayout = GetKeyboardLayout(threadId);
                if (currentLayout == IntPtr.Zero) currentLayout = LoadKeyboardLayout("00000419", 0x00000001);

                uint currentLayoutId = (uint)currentLayout & 0xFFFF;
                IntPtr altLayout = (currentLayoutId == 0x0419)
                    ? LoadKeyboardLayout("00000409", 0x00000001)
                    : LoadKeyboardLayout("00000419", 0x00000001);

                string strCurrent = _corrector.VkCodesToString(vkCodes, currentLayout) ?? "";
                string strAlt = _corrector.VkCodesToString(vkCodes, altLayout) ?? "";

                string cleanCurrent = new string(strCurrent.Where(c => char.IsLetter(c) || c == '[' || c == ']' || c == ';' || c == '\'' || c == ',' || c == '.').ToArray());
                string cleanAlt = new string(strAlt.Where(c => char.IsLetter(c) || c == '[' || c == ']' || c == ';' || c == '\'' || c == ',' || c == '.').ToArray());

                if (cleanCurrent.Length < 2) return;

                if (_corrector.LooksLikeWrongLayout(cleanCurrent, cleanAlt, out string? finalCorrectedWord, out bool needsLayoutSwitch))
                {
                    _isCorrecting = true;
                    KeyboardHook?.Suspend(true);
                    GlobalKeyboardHook.SetInputBlocked(true); // ⚡ Блокируем клавиатуру

                    try
                    {
                        char boundary = KeyboardHook.LastBoundaryChar != '\0' ? KeyboardHook.LastBoundaryChar : ' ';
                        string wordToInsert = finalCorrectedWord ?? cleanAlt;
                        int charsToDelete = cleanCurrent.Length + 1;

                        _dispatcherQueue?.TryEnqueue(async () =>
                        {
                            try
                            {
                                await SendMultipleBackspaces(charsToDelete);

                                if (needsLayoutSwitch && altLayout != IntPtr.Zero)
                                {
                                    ActivateKeyboardLayout(altLayout, 0x00000001);
                                    IntPtr targetHWnd = GetForegroundWindow();
                                    if (targetHWnd != IntPtr.Zero)
                                        PostMessage(targetHWnd, WM_INPUTLANGCHANGEREQUEST, (IntPtr)1, altLayout);
                                    await Task.Delay(200);
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
                            finally
                            {
                                await Task.Delay(50);
                                GlobalKeyboardHook.SetInputBlocked(false); // ⚡ Снимаем блокировку клавиатуры здесь
                                KeyboardHook?.Suspend(false);
                                _isCorrecting = false;
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Ошибка: {ex.Message}");
                        GlobalKeyboardHook.SetInputBlocked(false);
                        KeyboardHook?.Suspend(false);
                        _isCorrecting = false;
                    }
                }
            });
        }

        private void SetWindowIcon(Window window)
        {
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
                if (File.Exists(iconPath))
                {
                    window.AppWindow.SetIcon(iconPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Не удалось установить иконку для окна: {ex.Message}");
            }
        }

        private Icon CreateSimpleIcon()
        {
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");

            if (File.Exists(iconPath))
            {
                try
                {
                    return new Icon(iconPath);
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ Не удалось загрузить app.ico для трея, используем запасной вариант");
                }
            }

            Bitmap bitmap = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(System.Drawing.Color.FromArgb(255, 0, 120, 215));
                using (Font font = new Font("Arial", 20, FontStyle.Bold))
                using (Brush brush = new SolidBrush(System.Drawing.Color.White))
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