using System;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;
using System.IO;

namespace KeyBoopWin
{
    public sealed partial class MainWindow : Window
    {
        public static IntPtr WindowHandle { get; private set; }
        private SpeechRecognizer? _speechRecognizer;
        private MicrophonePreview? _micPreview;
        private bool _isPreviewing = false;
        public ObservableCollection<HistoryItem> SessionHistory { get; set; } = new();

        private Microsoft.UI.Dispatching.DispatcherQueueTimer? _hotkeyTimer;
        private bool _wasKeyPressed = false;
        private DateTime _keyPressStartTime = DateTime.MinValue; // ⚡ Добавлено поле для защиты от залипания

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        public MainWindow()
        {
            this.InitializeComponent();
            this.AppWindow?.Resize(new Windows.Graphics.SizeInt32(1000, 750));

            HistoryList.ItemsSource = SessionHistory;

            WindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            SubclassWindow(WindowHandle);

            InitializeSpeechRecognizer();

            RegisterHotkeyTimer();
            CenterWindowOnScreen();
        }

        private void CenterWindowOnScreen()
        {
            var displayArea = DisplayArea.GetFromWindowId(this.AppWindow.Id, DisplayAreaFallback.Nearest);
            if (displayArea is not null)
            {
                var centeredPosition = this.AppWindow.Position;
                centeredPosition.X = ((displayArea.WorkArea.Width - this.AppWindow.Size.Width) / 2);
                centeredPosition.Y = ((displayArea.WorkArea.Height - this.AppWindow.Size.Height) / 2);
                this.AppWindow.Move(centeredPosition);
            }
        }

        private void InitializeSpeechRecognizer()
        {
            try
            {
                _speechRecognizer = new SpeechRecognizer("ru");
                _speechRecognizer.TextRecognized += OnTextRecognized;
                _speechRecognizer.AudioLevelChanged += OnAudioLevelChanged;
                _speechRecognizer.NoiseSuppressor.LearningProgressChanged += OnNoiseLearningProgress;
                _speechRecognizer.NoiseSuppressor.LearningCompleted += OnNoiseLearningCompleted;
                _speechRecognizer.SetNoiseSuppression(true);

                if (NoiseSuppressionCheckBox != null) NoiseSuppressionCheckBox.IsChecked = true;
                StatusText.Text = "✅ Модель загружена. Шумоподавление включено.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"❌ Ошибка: {ex.Message}. Проверь папку Models.";
            }
        }

        public void UnloadSpeechModel()
        {
            if (_speechRecognizer != null)
            {
                _speechRecognizer.StopListening();
                _speechRecognizer.Dispose();
                _speechRecognizer = null;
                System.Diagnostics.Debug.WriteLine("🔇 Модель Vosk выгружена из памяти");
            }
        }

        public void LoadSpeechModel()
        {
            if (_speechRecognizer == null)
            {
                InitializeSpeechRecognizer();
                System.Diagnostics.Debug.WriteLine(" Модель Vosk загружена в память");
            }
        }

        private void StartVoice_Click(object sender, RoutedEventArgs e)
        {
            if (_speechRecognizer != null)
            {
                _speechRecognizer.StartListening();
                StartVoiceBtn.IsEnabled = false;
                StopVoiceBtn.IsEnabled = true;
                StatusText.Text = "🎤 Запись идет... Говорите.";
            }
        }

        private void StopVoice_Click(object sender, RoutedEventArgs e)
        {
            if (_speechRecognizer != null)
            {
                _speechRecognizer.StopListening();
                StartVoiceBtn.IsEnabled = true;
                StopVoiceBtn.IsEnabled = false;
                AddCurrentTextToHistory();
                StatusText.Text = "⏹️ Запись остановлена. Текст добавлен в историю.";
            }
        }

        private void PreviewMic_Click(object sender, RoutedEventArgs e)
        {
            if (!_isPreviewing)
            {
                _micPreview = new MicrophonePreview();
                _micPreview.AudioLevelChanged += OnAudioLevelChanged;
                _micPreview.StartPreview(_speechRecognizer);
                _isPreviewing = true;
                PreviewMicBtn.Content = "⏹️ Остановить тест";
                StatusText.Text = "🎧 Тест микрофона активен.";
            }
            else
            {
                _micPreview?.StopPreview();
                _micPreview?.Dispose();
                _isPreviewing = false;
                PreviewMicBtn.Content = "🎧 Тест микрофона";
                StatusText.Text = "Тест микрофона остановлен.";
                AudioLevelBar.Value = 0;
            }
        }

        private void OnTextRecognized(object? sender, string text) => DispatcherQueue.TryEnqueue(() => RecognizedText.Text += text + " ");
        private void OnAudioLevelChanged(object? sender, float level) => DispatcherQueue.TryEnqueue(() => AudioLevelBar.Value = Math.Min(1.0, level * 2));

        private void OnNoiseLearningProgress(object? sender, int progressPercent)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (progressPercent < 100)
                {
                    NoiseLearningBar.Visibility = Visibility.Visible;
                    NoiseLearningBar.Value = progressPercent;
                    StatusText.Text = $"🔇 Обучение шуму: {progressPercent}% (молчите ~{(100 - progressPercent) / 50} сек)";
                }
            });
        }

        private void OnNoiseLearningCompleted(object? sender, EventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                NoiseLearningBar.Visibility = Visibility.Collapsed;
                StatusText.Text = "✅ Шумоподавление активно!";
            });
        }

        private void AddCurrentTextToHistory()
        {
            if (RecognizedText == null || string.IsNullOrWhiteSpace(RecognizedText.Text)) return;

            var item = new HistoryItem { Timestamp = DateTime.Now.ToString("HH:mm:ss"), Text = RecognizedText.Text.Trim() };
            SessionHistory.Add(item);
            RecognizedText.Text = "";
        }

        private void AddToHistory_Click(object sender, RoutedEventArgs e) { AddCurrentTextToHistory(); StatusText.Text = "✅ Текст добавлен в историю."; }
        private void ClearHistory_Click(object sender, RoutedEventArgs e) { SessionHistory.Clear(); StatusText.Text = "🗑️ История очищена."; }

        private async void CopyHistoryItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string text)
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage { RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy };
                dataPackage.SetText(text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                StatusText.Text = "📋 Элемент истории скопирован!";
                await Task.Delay(2000);
                if (StatusText.Text == "📋 Элемент истории скопирован!") StatusText.Text = "✅ Готов к работе";
            }
        }

        private async void CopyToClipboard_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(RecognizedText.Text))
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage { RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy };
                dataPackage.SetText(RecognizedText.Text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                StatusText.Text = "📋 Текущий текст скопирован!";
                await Task.Delay(2000);
                if (StatusText.Text == "📋 Текущий текст скопирован!") StatusText.Text = "✅ Готов к работе";
            }
        }

        private void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageSelector.SelectedItem is ComboBoxItem selectedItem)
            {
                string lang = selectedItem.Tag?.ToString() ?? "ru";
                try
                {
                    AddCurrentTextToHistory();
                    _speechRecognizer?.ChangeLanguage(lang);
                    StatusText.Text = $"✅ Язык изменен на: {(lang == "ru" ? "Русский" : "English")}";
                }
                catch (Exception ex) { StatusText.Text = $"❌ Ошибка смены языка: {ex.Message}"; }
            }
        }

        private void MicGainSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            GainValueText.Text = $"{e.NewValue:F1}x";
            if (_speechRecognizer != null) _speechRecognizer.SetMicrophoneGain((float)e.NewValue);
        }

        private void NoiseSuppression_Checked(object sender, RoutedEventArgs e) { if (_speechRecognizer != null) _speechRecognizer.SetNoiseSuppression(true); }
        private void NoiseSuppression_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_speechRecognizer != null) { _speechRecognizer.SetNoiseSuppression(false); NoiseLearningBar.Visibility = Visibility.Collapsed; StatusText.Text = "✅ Шумоподавление выключено"; }
        }

        // ==========================================
        // ГЛОБАЛЬНЫЙ ПЕРЕХВАТ ВСЕХ ХОТКЕЕВ
        // ==========================================
        private void RegisterHotkeyTimer()
        {
            _hotkeyTimer = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().CreateTimer();
            _hotkeyTimer.Interval = TimeSpan.FromMilliseconds(100);
            _hotkeyTimer.Tick += HotkeyTimer_Tick;
            _hotkeyTimer.Start();
        }

        private void HotkeyTimer_Tick(object? sender, object e)
        {

            var settings = SettingsManager.Load();

            if (settings.EnableScreenTranslatorHotkey)
            {
                uint vk = (uint)settings.ScreenTranslatorHotkeyKey;
                bool keyDown = (GetAsyncKeyState((int)vk) & 0x8000) != 0;

                bool ctrlPressed = (GetAsyncKeyState(0x11) & 0x8000) != 0;
                bool altPressed = (GetAsyncKeyState(0x12) & 0x8000) != 0;
                bool shiftPressed = (GetAsyncKeyState(0x10) & 0x8000) != 0;

                bool modifiersMatch = true;
                if (settings.ScreenTranslatorHotkeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Control)) modifiersMatch &= ctrlPressed; else modifiersMatch &= !ctrlPressed;
                if (settings.ScreenTranslatorHotkeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Menu)) modifiersMatch &= altPressed; else modifiersMatch &= !altPressed;
                if (settings.ScreenTranslatorHotkeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Shift)) modifiersMatch &= shiftPressed; else modifiersMatch &= !shiftPressed;

                if (keyDown && modifiersMatch && !_wasKeyPressed)
                {
                    _wasKeyPressed = true;
                    if (App.Current is App app)
                    {
                        app.ActivateScreenTranslator();
                    }
                }
                else if (!keyDown)
                {
                    // Сбрасываем флаг только когда клавиша отпущена
                    // (если это не пересекается с другими проверками)
                }
            }

            if (App.IsRecordingHotkey) return;

            if ((DateTime.Now - App.LastHotkeyChangeTime).TotalMilliseconds < 500) return;

            if (settings.IsSleepMode) return;

            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");

            CheckHotkey(settings.EnableScreenTranslatorHotkey, settings.ScreenTranslatorHotkeyModifiers, settings.ScreenTranslatorHotkeyKey, () =>
            {
                if (App.Current is App app)
                {
                    app.ActivateScreenTranslator();
                }
            });

            CheckHotkey(settings.EnableVoiceInputHotkey, settings.VoiceInputHotkeyModifiers, settings.VoiceInputHotkeyKey, () =>
            {
                this.Activate();
            });

            CheckHotkey(settings.EnableTranslatorHotkey, settings.TranslatorHotkeyModifiers, settings.TranslatorHotkeyKey, () =>
            {
                var window = new TranslatorWindow();
                if (File.Exists(iconPath)) window.AppWindow.SetIcon(iconPath);
                window.Activate();
            });

            CheckHotkey(settings.EnableConverterHotkey, settings.ConverterHotkeyModifiers, settings.ConverterHotkeyKey, () =>
            {
                var window = new ConverterWindow();
                if (File.Exists(iconPath)) window.AppWindow.SetIcon(iconPath);
                window.Activate();
            });

            CheckHotkey(settings.EnableScreenTranslatorHotkey, settings.ScreenTranslatorHotkeyModifiers, settings.ScreenTranslatorHotkeyKey, () =>
            {
                if (App.Current is App app)
                {
                    app.ActivateScreenTranslator();
                }
            });

            CheckHotkey(settings.EnableSystemLayoutSwitchHotkey, settings.SystemLayoutSwitchModifiers, settings.SystemLayoutSwitchKey, () =>
            {
                SwitchSystemKeyboardLayout();
            });

            CheckSystemLayoutSwitch(settings);
        }

        private void CheckSystemLayoutSwitch(AppSettings settings)
        {
            if (settings.EnableSystemLayoutPresetHotkey)
            {
                bool triggered = false;
                int targetVk = 0;

                switch (settings.SystemLayoutPresetIndex)
                {
                    case 0: targetVk = 0xA3; break; // Правый Ctrl
                    case 1: targetVk = 0xA2; break; // Левый Ctrl
                    case 2: targetVk = 0xA1; break; // Правый Shift
                    case 3: targetVk = 0xA0; break; // Левый Shift
                    case 4: targetVk = 0x14; break; // Caps Lock
                    case 5: targetVk = 0x20; break; // Alt + Space
                    case 6: targetVk = 0x20; break; // Ctrl + Space
                }

                if (settings.SystemLayoutPresetIndex <= 4)
                {
                    triggered = IsKeySinglePressed(targetVk);
                }
                else if (settings.SystemLayoutPresetIndex == 5)
                {
                    triggered = (GetAsyncKeyState(0x12) & 0x8000) != 0 && (GetAsyncKeyState(0x20) & 0x8000) != 0;
                }
                else if (settings.SystemLayoutPresetIndex == 6)
                {
                    triggered = (GetAsyncKeyState(0x11) & 0x8000) != 0 && (GetAsyncKeyState(0x20) & 0x8000) != 0;
                }

                if (triggered && (targetVk == 0xA2 || targetVk == 0xA3 || targetVk == 0xA0 || targetVk == 0xA1))
                {
                    for (int vk = 0x41; vk <= 0x5A; vk++)
                    {
                        if ((GetAsyncKeyState(vk) & 0x8000) != 0)
                        {
                            triggered = false;
                            break;
                        }
                    }
                }

                if (triggered && !_wasKeyPressed)
                {
                    _wasKeyPressed = true;
                    _keyPressStartTime = DateTime.Now;
                    SwitchSystemKeyboardLayout();
                }
                else if (triggered && _wasKeyPressed)
                {
                    if ((DateTime.Now - _keyPressStartTime).TotalMilliseconds > 1000)
                    {
                        _wasKeyPressed = false;
                    }
                }
                else if (!triggered && !IsAnyPresetKeyPressed(settings.SystemLayoutPresetIndex))
                {
                    _wasKeyPressed = false;
                }
            }
            else if (settings.EnableSystemLayoutSwitchHotkey)
            {
                CheckHotkey(true, settings.SystemLayoutSwitchModifiers, settings.SystemLayoutSwitchKey, () =>
                {
                    SwitchSystemKeyboardLayout();
                });
            }
        }

        private bool IsKeySinglePressed(int vKey)
        {
            return (GetAsyncKeyState(vKey) & 0x8000) != 0;
        }

        private bool IsAnyPresetKeyPressed(int index)
        {
            int vk = index switch
            {
                0 => 0xA3,
                1 => 0xA2,
                2 => 0xA1,
                3 => 0xA0,
                4 => 0x14,
                5 => 0x20,
                6 => 0x20,
                _ => 0
            };
            return vk != 0 && (GetAsyncKeyState(vk) & 0x8000) != 0;
        }

        private void CheckHotkey(bool isEnabled, VirtualKeyModifiers modifiers, VirtualKey key, Action action)
        {
            if (!isEnabled) return;

            uint vk = (uint)key;
            bool keyDown = (GetAsyncKeyState((int)vk) & 0x8000) != 0;
            bool ctrlPressed = (GetAsyncKeyState(0x11) & 0x8000) != 0;
            bool altPressed = (GetAsyncKeyState(0x12) & 0x8000) != 0;
            bool shiftPressed = (GetAsyncKeyState(0x10) & 0x8000) != 0;

            bool modifiersMatch = true;
            if (modifiers.HasFlag(VirtualKeyModifiers.Control)) modifiersMatch &= ctrlPressed; else modifiersMatch &= !ctrlPressed;
            if (modifiers.HasFlag(VirtualKeyModifiers.Menu)) modifiersMatch &= altPressed; else modifiersMatch &= !altPressed;
            if (modifiers.HasFlag(VirtualKeyModifiers.Shift)) modifiersMatch &= shiftPressed; else modifiersMatch &= !shiftPressed;

            if (keyDown && modifiersMatch && !_wasKeyPressed)
            {
                _wasKeyPressed = true;
                action();
            }
            else if (!keyDown)
            {
                _wasKeyPressed = false;
            }
        }

        private void OpenTranslator_Click(object sender, RoutedEventArgs e)
        {
            var window = new TranslatorWindow();
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(iconPath)) window.AppWindow.SetIcon(iconPath);
            window.Activate();
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            if (App.Current is App app) app.OpenSettings();
        }

        private void OpenConverter_Click(object sender, RoutedEventArgs e)
        {
            var window = new ConverterWindow();
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(iconPath)) window.AppWindow.SetIcon(iconPath);
            window.Activate();
        }

        private void SwitchSystemKeyboardLayout()
        {
            IntPtr hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return;

            uint threadId = GetWindowThreadProcessId(hWnd, out _);
            IntPtr currentLayout = GetKeyboardLayout(threadId);
            uint currentLayoutId = (uint)currentLayout & 0xFFFF;

            IntPtr targetLayout = (currentLayoutId == 0x0419)
                ? LoadKeyboardLayout("00000409", 0x00000001)
                : LoadKeyboardLayout("00000419", 0x00000001);

            if (targetLayout != IntPtr.Zero)
            {
                ActivateKeyboardLayout(targetLayout, 0x00000001);
                PostMessage(hWnd, WM_INPUTLANGCHANGEREQUEST, (IntPtr)1, targetLayout);
            }
        }

        // ==========================================
        // САБКЛАССИНГ ОКНА (ПЕРЕХВАТ WM_HOTKEY)
        // ==========================================
        private delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData);

        [DllImport("comctl32.dll", SetLastError = true)]
        private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, IntPtr uIdSubclass, IntPtr dwRefData);

        [DllImport("comctl32.dll", SetLastError = true)]
        private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        private SubclassProc? _subclassDelegate;

        private void SubclassWindow(IntPtr hwnd)
        {
            _subclassDelegate = new SubclassProc(WindowSubclassCallback);
            SetWindowSubclass(hwnd, _subclassDelegate, IntPtr.Zero, IntPtr.Zero);
        }

        private IntPtr WindowSubclassCallback(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData)
        {
            const uint WM_HOTKEY = 0x0312;

            if (uMsg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (Microsoft.UI.Xaml.Application.Current is App app)
                {
                    if (app.CheckHotkeysMessage(id))
                    {
                        return IntPtr.Zero;
                    }
                }
            }

            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern IntPtr GetKeyboardLayout(uint idThread);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint flags);
        [DllImport("user32.dll")] private static extern IntPtr ActivateKeyboardLayout(IntPtr hkl, uint flags);
        private const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;
    }

    public class HistoryItem
    {
        public string Timestamp { get; set; } = "";
        public string Text { get; set; } = "";
    }
}