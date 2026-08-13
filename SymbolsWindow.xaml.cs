using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KeyBoopWin
{
    public sealed partial class SymbolsWindow : Window
    {
        private List<string> _customSymbols = new List<string>();
        private int _statusToken = 0;

        private readonly string[] _typography = { "—", "–", "«", "»", "“", "”", "„", "‘", "’", "…", "•", "‰", "‱", "§", "¶", "©", "®", "™", "№", "°" };
        private readonly string[] _math = { "±", "×", "÷", "≠", "≈", "≡", "≤", "≥", "∞", "√", "∑", "∏", "∫", "∂", "∆", "π", "∈", "∉", "⊂", "⊆" };
        private readonly string[] _greek = { "α", "β", "γ", "δ", "ε", "θ", "λ", "μ", "π", "σ", "τ", "φ", "ψ", "ω", "Δ", "Ω", "Σ", "Ψ", "Φ" };
        private readonly string[] _arrows = { "←", "→", "↑", "↓", "↔", "↕", "⇐", "⇒", "⇔", "▲", "▼", "◄", "►", "↺", "↻" };
        private readonly string[] _misc = { "₽", "$", "€", "£", "¥", "₴", "★", "☆", "✓", "✗", "♠", "♣", "♥", "♦", "♪", "♫" };

        public class CountryFlag
        {
            public string Code { get; set; }
            public string Name { get; set; }
        }

        private readonly List<CountryFlag> _cisFlags = new List<CountryFlag>
{
    new CountryFlag { Code = "🇷🇺", Name = "Россия" },
    new CountryFlag { Code = "🇧🇾", Name = "Беларусь" },
    new CountryFlag { Code = "🇰🇿", Name = "Казахстан" },
    new CountryFlag { Code = "🇺🇦", Name = "Украина" },
    new CountryFlag { Code = "🇦🇲", Name = "Армения" },
    new CountryFlag { Code = "🇦🇿", Name = "Азербайджан" },
    new CountryFlag { Code = "🇬🇪", Name = "Грузия" },
    new CountryFlag { Code = "🇲🇩", Name = "Молдова" }
};

        private readonly List<CountryFlag> _europeFlags = new List<CountryFlag>
{
    new CountryFlag { Code = "🇩🇪", Name = "Германия" },
    new CountryFlag { Code = "🇫🇷", Name = "Франция" },
    new CountryFlag { Code = "🇬🇧", Name = "Великобритания" },
    new CountryFlag { Code = "🇮🇹", Name = "Италия" },
    new CountryFlag { Code = "🇪🇸", Name = "Испания" },
    new CountryFlag { Code = "🇵🇱", Name = "Польша" },
    new CountryFlag { Code = "🇳🇱", Name = "Нидерланды" },
    new CountryFlag { Code = "🇪🇺", Name = "ЕС" }
};

        private readonly List<CountryFlag> _americaAsiaFlags = new List<CountryFlag>
{
    new CountryFlag { Code = "🇺🇸", Name = "США" },
    new CountryFlag { Code = "🇨🇦", Name = "Канада" },
    new CountryFlag { Code = "🇧🇷", Name = "Бразилия" },
    new CountryFlag { Code = "🇨🇳", Name = "Китай" },
    new CountryFlag { Code = "🇯🇵", Name = "Япония" },
    new CountryFlag { Code = "🇰🇷", Name = "Ю. Корея" }
};




        public SymbolsWindow()
        {
            this.InitializeComponent();

            // 1. Задаем размеры окна
            this.AppWindow.Resize(new Windows.Graphics.SizeInt32(650, 720));

            // 2. Центрируем окно на текущем экране
            CenterWindowOnScreen();

            // 3. Заполняем базовые наборы
            PopulateGrid(TypographyGrid, _typography);
            PopulateGrid(MathGrid, _math);
            PopulateGrid(GreekGrid, _greek);
            PopulateGrid(ArrowsGrid, _arrows);
            PopulateGrid(MiscGrid, _misc);

            CisFlagsGrid.ItemsSource = _cisFlags;
            EuropeFlagsGrid.ItemsSource = _europeFlags;
            AmericaAsiaFlagsGrid.ItemsSource = _americaAsiaFlags;

            LoadCustomSymbolsUI();
        }

        // ⚡ МЕТОД ЦЕНТРИРОВАНИЯ ОКНА НА ЭКРАНЕ
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

        private void PopulateGrid(GridView grid, IEnumerable<string> symbols)
        {
            grid.Items.Clear();
            foreach (var symbol in symbols)
            {
                var btn = new Button
                {
                    Content = symbol,
                    FontSize = 20,
                    Width = 44,
                    Height = 44,
                    Padding = new Thickness(0)
                };
                btn.Click += (s, e) => CopySymbolToClipboard(symbol);
                grid.Items.Add(btn);
            }
        }

        private void LoadCustomSymbolsUI()
        {
            _customSymbols = CustomSymbolsManager.LoadCustomSymbols();

            if (_customSymbols.Count > 0)
            {
                CustomSectionPanel.Visibility = Visibility.Visible;
                CustomSymbolsGrid.ItemsSource = null;
                CustomSymbolsGrid.ItemsSource = _customSymbols;
            }
            else
            {
                CustomSectionPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void OnAddCustomSymbolClick(object sender, RoutedEventArgs e)
        {
            string symbol = NewSymbolTextBox.Text.Trim();
            if (string.IsNullOrEmpty(symbol)) return;

            if (!_customSymbols.Contains(symbol))
            {
                _customSymbols.Insert(0, symbol);
                CustomSymbolsManager.SaveCustomSymbols(_customSymbols);
                LoadCustomSymbolsUI();
                ShowStatus($"✅ Символ \"{symbol}\" успешно добавлен!");
            }

            NewSymbolTextBox.Text = string.Empty;
        }

        private void OnDeleteCustomSymbolClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is string symbol)
            {
                _customSymbols.Remove(symbol);
                CustomSymbolsManager.SaveCustomSymbols(_customSymbols);
                LoadCustomSymbolsUI();
                ShowStatus($"🗑️ Символ \"{symbol}\" удален");
            }
        }

        private void OnSymbolButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                // Проверяем, что пришло (строка или объект нашей модели)
                string symbol = string.Empty;
                if (btn.CommandParameter is string code)
                {
                    symbol = code;
                }
                else if (btn.DataContext is CountryFlag flag)
                {
                    symbol = flag.Code;
                }

                if (!string.IsNullOrEmpty(symbol))
                {
                    CopySymbolToClipboard(symbol);
                }
            }
        }

        private void OnSymbolClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is string symbol)
            {
                CopySymbolToClipboard(symbol);
            }
        }

        private void CopySymbolToClipboard(string symbol)
        {
            if (SetClipboardTextWin32(symbol))
            {
                ShowStatus($"✅ Символ \"{symbol}\" скопирован в буфер обмена!");
            }
            else
            {
                ShowStatus($"❌ Ошибка копирования символа \"{symbol}\"");
            }
        }

        private async void ShowStatus(string message)
        {
            int currentToken = ++_statusToken;
            StatusTextBlock.Text = message;
            StatusBorder.Visibility = Visibility.Visible;

            await Task.Delay(2200);

            if (currentToken == _statusToken)
            {
                StatusBorder.Visibility = Visibility.Collapsed;
            }
        }

        #region Win32 Clipboard Helpers
        private const uint CF_UNICODETEXT = 13;
        private const uint GMEM_MOVEABLE = 0x0002;

        [DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr hWndNewOwner);
        [DllImport("user32.dll")] private static extern bool CloseClipboard();
        [DllImport("user32.dll")] private static extern bool EmptyClipboard();
        [DllImport("user32.dll")] private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
        [DllImport("kernel32.dll")] private static extern IntPtr GlobalAlloc(uint uFlags, IntPtr dwBytes);
        [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr hMem);
        [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr hMem);
        [DllImport("kernel32.dll")] private static extern IntPtr GlobalFree(IntPtr hMem);

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
        #endregion
    }
}