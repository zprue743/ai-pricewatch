using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AiPriceWatch.App.Services;
using AiPriceWatch.Core.Models;
using AiPriceWatch.Core.Services;
using AiPriceWatch.Core.ViewModels;

namespace AiPriceWatch.App;

public partial class MainWindow : Window
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private readonly HudViewModel _viewModel;
    private GlobalHotkeyService? _hotkeyService;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new HudViewModel(PricingCatalogService.LoadDefault());
        DataContext = _viewModel;

        BuildProviderTabs();
        UpdateHudBackgroundAlpha(_viewModel.BackgroundOpacity);

        SourceInitialized += OnSourceInitialized;
        Closed += OnWindowClosed;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hotkeyService = new GlobalHotkeyService(ToggleOverlayVisibility);
        _hotkeyService.Attach(this);
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _hotkeyService?.Dispose();
    }

    private void ToggleOverlayVisibility()
    {
        if (IsVisible && WindowState != WindowState.Minimized)
        {
            Hide();
        }
        else
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }
    }

    private void BuildProviderTabs()
    {
        ProviderTabsPanel.Children.Clear();
        for (int i = 0; i < _viewModel.Providers.Count; i++)
        {
            string provider = _viewModel.Providers[i];
            bool isSelected = string.Equals(provider, _viewModel.SelectedProvider, StringComparison.OrdinalIgnoreCase);

            var button = new Button
            {
                Content = i < 7 ? $"[{i + 1}] {provider}" : provider,
                Tag = provider,
                Margin = new Thickness(0, 0, 6, 4),
                Padding = new Thickness(9, 4, 9, 4),
                FontSize = 11,
                FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal,
                Foreground = new SolidColorBrush(isSelected
                    ? Color.FromRgb(0xF0, 0xF6, 0xFC)
                    : Color.FromRgb(0x8B, 0x94, 0x9E)),
                Background = new SolidColorBrush(isSelected
                    ? Color.FromRgb(0x1F, 0x6F, 0xEB)
                    : Color.FromRgb(0x16, 0x1B, 0x22)),
                BorderBrush = new SolidColorBrush(isSelected
                    ? Color.FromRgb(0x58, 0xA6, 0xFF)
                    : Color.FromRgb(0x30, 0x36, 0x3D)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ToolTip = $"Filter by {provider} (Alt+{i + 1})"
            };

            button.Click += ProviderTab_Click;
            ProviderTabsPanel.Children.Add(button);
        }
    }

    private void ProviderTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string provider })
        {
            _viewModel.SelectedProvider = provider;
            BuildProviderTabs();
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_viewModel is not null)
        {
            UpdateHudBackgroundAlpha(e.NewValue);
        }
    }

    private void UpdateHudBackgroundAlpha(double opacity)
    {
        if (HudBackgroundBrush is null)
        {
            return;
        }

        double clamped = Math.Clamp(opacity, 0.35, 1.0);
        byte alpha = (byte)Math.Round(clamped * 255);
        HudBackgroundBrush.Color = Color.FromArgb(alpha, 0x0D, 0x11, 0x17);
    }

    private void FastModeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleFastMode();
    }

    private void ToggleFastMode()
    {
        _viewModel.IsFastMode = !_viewModel.IsFastMode;
        FastModeButton.Content = _viewModel.IsFastMode ? "⚡ FAST: ON" : "⚡ FAST: OFF";
        FastModeButton.Foreground = new SolidColorBrush(_viewModel.IsFastMode
            ? Color.FromRgb(0xFF, 0xA6, 0x57)
            : Color.FromRgb(0x8B, 0x94, 0x9E));
        FastModeButton.BorderBrush = new SolidColorBrush(_viewModel.IsFastMode
            ? Color.FromRgb(0xFF, 0xA6, 0x57)
            : Color.FromRgb(0x30, 0x36, 0x3D));
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.IsAlwaysOnTop = !_viewModel.IsAlwaysOnTop;
        PinButton.Content = _viewModel.IsAlwaysOnTop ? "PIN: ON" : "PIN: OFF";
        PinButton.Foreground = new SolidColorBrush(_viewModel.IsAlwaysOnTop
            ? Color.FromRgb(0x58, 0xA6, 0xFF)
            : Color.FromRgb(0x8B, 0x94, 0x9E));
    }

    private async void SyncButton_Click(object sender, RoutedEventArgs e)
    {
        SyncButton.IsEnabled = false;
        try
        {
            await _viewModel.SyncLivePricesAsync(SharedHttpClient);
        }
        finally
        {
            SyncButton.IsEnabled = true;
        }
    }

    private void TierComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is null || TierComboBox.SelectedItem is not ComboBoxItem { Tag: string tag })
        {
            return;
        }

        _viewModel.SelectedTier = tag switch
        {
            "Fast" => TaskTier.Fast,
            "Balanced" => TaskTier.Balanced,
            "Reasoning" => TaskTier.Reasoning,
            _ => null
        };
    }

    private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is null || SortComboBox.SelectedItem is not ComboBoxItem { Tag: string tag })
        {
            return;
        }

        if (Enum.TryParse<PricingSortField>(tag, out PricingSortField sortField))
        {
            _viewModel.SelectedSort = sortField;
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.M)
        {
            ToggleFastMode();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Alt)
        {
            Key actualKey = e.Key == Key.System ? e.SystemKey : e.Key;
            int index = actualKey switch
            {
                Key.D1 => 0,
                Key.D2 => 1,
                Key.D3 => 2,
                Key.D4 => 3,
                Key.D5 => 4,
                Key.D6 => 5,
                Key.D7 => 6,
                _ => -1
            };

            if (index >= 0 && index < _viewModel.Providers.Count)
            {
                _viewModel.SelectedProvider = _viewModel.Providers[index];
                BuildProviderTabs();
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Escape)
        {
            if (!string.IsNullOrEmpty(_viewModel.SearchText))
            {
                _viewModel.SearchText = string.Empty;
                e.Handled = true;
            }
        }
    }
}
