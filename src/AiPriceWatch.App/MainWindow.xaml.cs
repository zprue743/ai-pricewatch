using System.ComponentModel;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AiPriceWatch.App.Services;
using AiPriceWatch.Core.Models;
using AiPriceWatch.Core.Services;
using AiPriceWatch.Core.ViewModels;
using Velopack;

namespace AiPriceWatch.App;

public partial class MainWindow : Window
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private readonly HudViewModel _viewModel;
    private readonly AppUpdateService _updateService = new();
    private GlobalHotkeyService? _hotkeyService;
    private TrayIconService? _trayService;
    private UpdateInfo? _pendingUpdate;
    private bool _isExplicitExit;

    public MainWindow()
    {
        InitializeComponent();
        SettingsVersionText.Text = AppVersionInfo.GetDisplayVersion(typeof(MainWindow).Assembly);

        _viewModel = new HudViewModel(PricingCatalogService.LoadDefault());
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        BuildProviderTabs();
        UpdateHudBackgroundAlpha(_viewModel.BackgroundOpacity);
        SyncHeaderToggleVisuals();
        UpdateOnboardingAndEmptyState();

        if (_updateService.IsStoreManaged)
        {
            SettingsCheckUpdateButton.Content = "Updates managed by Microsoft Store";
            SettingsCheckUpdateButton.IsEnabled = false;
        }

        _trayService = new TrayIconService(
            onToggleVisibility: () => Dispatcher.Invoke(ToggleOverlayVisibility),
            onOpenSettings: () => Dispatcher.Invoke(OpenSettingsFromTray),
            onCheckForUpdates: () => Dispatcher.Invoke(() => _ = CheckUpdatesManualAsync()),
            onExit: () => Dispatcher.Invoke(ExitApplication),
            updatesSupported: !_updateService.IsStoreManaged);

        SourceInitialized += OnSourceInitialized;
        Loaded += OnWindowLoaded;
        Closed += OnWindowClosed;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HudViewModel.VisibleModels) or nameof(HudViewModel.HasDismissedOnboarding))
        {
            UpdateOnboardingAndEmptyState();
        }
    }

    private void UpdateOnboardingAndEmptyState()
    {
        FirstRunBanner.Visibility = _viewModel.HasDismissedOnboarding ? Visibility.Collapsed : Visibility.Visible;
        EmptyStatePanel.Visibility = _viewModel.VisibleModels.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SyncHeaderToggleVisuals()
    {
        FastModeButton.Content = _viewModel.IsFastMode ? "⚡ FAST: ON" : "⚡ FAST: OFF";
        FastModeButton.Foreground = new SolidColorBrush(_viewModel.IsFastMode
            ? Color.FromRgb(0xFF, 0xA6, 0x57)
            : Color.FromRgb(0x8B, 0x94, 0x9E));
        FastModeButton.BorderBrush = new SolidColorBrush(_viewModel.IsFastMode
            ? Color.FromRgb(0xFF, 0xA6, 0x57)
            : Color.FromRgb(0x30, 0x36, 0x3D));

        PinButton.Content = _viewModel.IsAlwaysOnTop ? "PIN: ON" : "PIN: OFF";
        PinButton.Foreground = new SolidColorBrush(_viewModel.IsAlwaysOnTop
            ? Color.FromRgb(0x58, 0xA6, 0xFF)
            : Color.FromRgb(0x8B, 0x94, 0x9E));
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExplicitExit)
        {
            e.Cancel = true;
            Hide();
            if (!_viewModel.HasShownTrayMinimizeNotice)
            {
                _trayService?.ShowOneTimeMinimizeNotice();
                _viewModel.HasShownTrayMinimizeNotice = true;
            }
            return;
        }

        base.OnClosing(e);
    }

    private void ExitApplication()
    {
        _isExplicitExit = true;
        _trayService?.Dispose();
        _trayService = null;
        _hotkeyService?.Dispose();
        _hotkeyService = null;
        System.Windows.Application.Current.Shutdown();
    }

    private void OpenSettingsFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        SettingsPanel.Visibility = Visibility.Visible;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hotkeyService = new GlobalHotkeyService(ToggleOverlayVisibility);
        _hotkeyService.Attach(this);
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _trayService?.Dispose();
        _trayService = null;
        _hotkeyService?.Dispose();
        _hotkeyService = null;
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

    private void RowHideButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string modelId })
        {
            _viewModel.ToggleModelHidden(modelId);
        }
    }

    private void RowRemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string modelId })
        {
            _viewModel.RemoveAddedModel(modelId);
        }
    }

    private void AddModelsButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsPanel.Visibility = Visibility.Collapsed;
        _viewModel.IsAddModelsOpen = !_viewModel.IsAddModelsOpen;
    }

    private void CloseAddModelsButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.IsAddModelsOpen = false;
    }

    private void CandidateActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string fullId })
        {
            if (!_viewModel.AddOpenRouterModel(fullId))
            {
                _viewModel.RemoveAddedModel(fullId);
            }
        }
    }

    private void ClearDiffsButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ClearPriceDiffs();
    }

    private void ToggleShowHiddenButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ShowHiddenModels = !_viewModel.ShowHiddenModels;
        ToggleShowHiddenButton.Foreground = new SolidColorBrush(_viewModel.ShowHiddenModels
            ? Color.FromRgb(0x58, 0xA6, 0xFF)
            : Color.FromRgb(0x8B, 0x94, 0x9E));
        ToggleShowHiddenButton.BorderBrush = new SolidColorBrush(_viewModel.ShowHiddenModels
            ? Color.FromRgb(0x58, 0xA6, 0xFF)
            : Color.FromRgb(0x30, 0x36, 0x3D));
    }

    private void UnhideAllButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.UnhideAllModels();
        ToggleShowHiddenButton.Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E));
        ToggleShowHiddenButton.BorderBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3D));
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.IsAlwaysOnTop = !_viewModel.IsAlwaysOnTop;
        PinButton.Content = _viewModel.IsAlwaysOnTop ? "PIN: ON" : "PIN: OFF";
        PinButton.Foreground = new SolidColorBrush(_viewModel.IsAlwaysOnTop
            ? Color.FromRgb(0x58, 0xA6, 0xFF)
            : Color.FromRgb(0x8B, 0x94, 0x9E));
    }

    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _pendingUpdate = await _updateService.CheckForUpdatesAsync();
            if (_pendingUpdate is not null)
            {
                UpdateButton.Content = $"UPDATE v{_pendingUpdate.TargetFullRelease.Version}";
                UpdateButton.Visibility = Visibility.Visible;
            }
        }
        catch
        {
            // Ignore offline or GitHub rate-limit errors during background update check.
        }
    }

    private async void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingUpdate is null)
        {
            return;
        }

        UpdateButton.IsEnabled = false;
        UpdateButton.Content = "UPDATING...";
        try
        {
            await _updateService.DownloadAndRestartAsync(_pendingUpdate);
        }
        catch
        {
            UpdateButton.Content = $"UPDATE v{_pendingUpdate.TargetFullRelease.Version}";
            UpdateButton.IsEnabled = true;
        }
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

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsPanel.Visibility = SettingsPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void CloseSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsPanel.Visibility = Visibility.Collapsed;
    }

    private void DismissOnboardingButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.HasDismissedOnboarding = true;
    }

    private void ShowOnboardingButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.HasDismissedOnboarding = false;
        SettingsPanel.Visibility = Visibility.Collapsed;
    }

    private void ResetFiltersButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SelectedProvider = "All";
        _viewModel.SelectedTier = null;
        _viewModel.SearchText = string.Empty;
        _viewModel.ShowHiddenModels = false;
        BuildProviderTabs();
    }

    private void StartWithWindowsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        bool enabled = StartWithWindowsCheckBox.IsChecked == true;
        bool applied = StartupService.ApplyStartWithWindows(enabled, out string? note);
        if (!string.IsNullOrWhiteSpace(note))
        {
            StartupNoteText.Text = note;
            StartupNoteText.Visibility = Visibility.Visible;
        }
        else if (applied)
        {
            StartupNoteText.Visibility = Visibility.Collapsed;
        }
    }

    private async void SettingsCheckUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        await CheckUpdatesManualAsync();
    }

    private async Task CheckUpdatesManualAsync()
    {
        if (_updateService.IsStoreManaged)
        {
            return;
        }

        SettingsCheckUpdateButton.IsEnabled = false;
        SettingsCheckUpdateButton.Content = "Checking...";
        try
        {
            _pendingUpdate = await _updateService.CheckForUpdatesAsync();
            if (_pendingUpdate is not null)
            {
                UpdateButton.Content = $"UPDATE v{_pendingUpdate.TargetFullRelease.Version}";
                UpdateButton.Visibility = Visibility.Visible;
                SettingsCheckUpdateButton.Content = $"Update available: v{_pendingUpdate.TargetFullRelease.Version}";
            }
            else
            {
                SettingsCheckUpdateButton.Content = _updateService.IsInstalled
                    ? "Up to date"
                    : "Portable/dev build (no auto-update)";
            }
        }
        catch
        {
            SettingsCheckUpdateButton.Content = "Update check failed (retry)";
        }
        finally
        {
            SettingsCheckUpdateButton.IsEnabled = true;
        }
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
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
            if (_viewModel.IsAddModelsOpen)
            {
                _viewModel.IsAddModelsOpen = false;
                e.Handled = true;
                return;
            }

            if (SettingsPanel.Visibility == Visibility.Visible)
            {
                SettingsPanel.Visibility = Visibility.Collapsed;
                e.Handled = true;
                return;
            }

            if (!string.IsNullOrEmpty(_viewModel.SearchText))
            {
                _viewModel.SearchText = string.Empty;
                e.Handled = true;
            }
        }
    }
}
