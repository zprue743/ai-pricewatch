using System.ComponentModel;
using System.Runtime.CompilerServices;
using AiPriceWatch.Core.Models;
using AiPriceWatch.Core.Services;

namespace AiPriceWatch.Core.ViewModels;

public sealed class HudViewModel : INotifyPropertyChanged
{
    private const double MinOpacity = 0.35;
    private const double MaxOpacity = 1.0;

    private readonly PricingCatalogService _catalog;
    private string _selectedProvider = "All";
    private TaskTier? _selectedTier;
    private string _searchText = string.Empty;
    private PricingSortField _selectedSort = PricingSortField.Featured;
    private TaskPreset _selectedPreset;
    private int _taskInputTokens;
    private int _taskCachedTokens;
    private int _taskOutputTokens;
    private bool _isFastMode;
    private double _backgroundOpacity = 0.86;
    private bool _isAlwaysOnTop = true;
    private string _statusText = "Curated catalog ready";
    private IReadOnlyList<ModelRowViewModel> _visibleModels = Array.Empty<ModelRowViewModel>();

    public HudViewModel(PricingCatalogService catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;

        var providerList = new List<string> { "All" };
        providerList.AddRange(_catalog.GetProviders());
        Providers = providerList;

        Presets =
        [
            new TaskPreset("Quick Prompt", 4_000, 0, 1_000, "4K in / 1K out"),
            new TaskPreset("Code Feature", 50_000, 0, 8_000, "50K in / 8K out"),
            new TaskPreset("Cached Agent Loop", 20_000, 100_000, 4_000, "20K in / 100K cached / 4K out"),
            new TaskPreset("Repo Audit", 250_000, 0, 16_000, "250K in / 16K out")
        ];

        _selectedPreset = Presets[1]; // Default to Code Feature (50K in / 8K out)
        _taskInputTokens = _selectedPreset.InputTokens;
        _taskCachedTokens = _selectedPreset.CachedInputTokens;
        _taskOutputTokens = _selectedPreset.OutputTokens;

        RefreshVisibleModels();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<string> Providers { get; }

    public IReadOnlyList<TaskPreset> Presets { get; }

    public string SelectedProvider
    {
        get => _selectedProvider;
        set
        {
            string normalized = string.IsNullOrWhiteSpace(value) ? "All" : value.Trim();
            if (_selectedProvider != normalized)
            {
                _selectedProvider = normalized;
                OnPropertyChanged();
                RefreshVisibleModels();
            }
        }
    }

    public TaskTier? SelectedTier
    {
        get => _selectedTier;
        set
        {
            if (_selectedTier != value)
            {
                _selectedTier = value;
                OnPropertyChanged();
                RefreshVisibleModels();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            string normalized = value ?? string.Empty;
            if (_searchText != normalized)
            {
                _searchText = normalized;
                OnPropertyChanged();
                RefreshVisibleModels();
            }
        }
    }

    public PricingSortField SelectedSort
    {
        get => _selectedSort;
        set
        {
            if (_selectedSort != value)
            {
                _selectedSort = value;
                OnPropertyChanged();
                RefreshVisibleModels();
            }
        }
    }

    public TaskPreset SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (value is not null && _selectedPreset != value)
            {
                _selectedPreset = value;
                _taskInputTokens = value.InputTokens;
                _taskCachedTokens = value.CachedInputTokens;
                _taskOutputTokens = value.OutputTokens;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TaskInputTokens));
                OnPropertyChanged(nameof(TaskCachedTokens));
                OnPropertyChanged(nameof(TaskOutputTokens));
                RefreshVisibleModels();
            }
        }
    }

    public int TaskInputTokens
    {
        get => _taskInputTokens;
        set
        {
            int clamped = Math.Max(0, value);
            if (_taskInputTokens != clamped)
            {
                _taskInputTokens = clamped;
                OnPropertyChanged();
                RefreshVisibleModels();
            }
        }
    }

    public int TaskCachedTokens
    {
        get => _taskCachedTokens;
        set
        {
            int clamped = Math.Max(0, value);
            if (_taskCachedTokens != clamped)
            {
                _taskCachedTokens = clamped;
                OnPropertyChanged();
                RefreshVisibleModels();
            }
        }
    }

    public int TaskOutputTokens
    {
        get => _taskOutputTokens;
        set
        {
            int clamped = Math.Max(0, value);
            if (_taskOutputTokens != clamped)
            {
                _taskOutputTokens = clamped;
                OnPropertyChanged();
                RefreshVisibleModels();
            }
        }
    }

    public bool IsFastMode
    {
        get => _isFastMode;
        set
        {
            if (_isFastMode != value)
            {
                _isFastMode = value;
                OnPropertyChanged();
                RefreshVisibleModels();
            }
        }
    }

    public double BackgroundOpacity
    {
        get => _backgroundOpacity;
        set
        {
            double clamped = Math.Clamp(value, MinOpacity, MaxOpacity);
            if (Math.Abs(_backgroundOpacity - clamped) > 0.0001)
            {
                _backgroundOpacity = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OpacityPercentageDisplay));
            }
        }
    }

    public string OpacityPercentageDisplay => $"{(int)Math.Round(_backgroundOpacity * 100)}%";

    public bool IsAlwaysOnTop
    {
        get => _isAlwaysOnTop;
        set
        {
            if (_isAlwaysOnTop != value)
            {
                _isAlwaysOnTop = value;
                OnPropertyChanged();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText != value)
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }
    }

    public IReadOnlyList<ModelRowViewModel> VisibleModels
    {
        get => _visibleModels;
        private set
        {
            _visibleModels = value;
            OnPropertyChanged();
        }
    }

    public async Task SyncLivePricesAsync(HttpClient httpClient, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        try
        {
            StatusText = "Syncing live prices from OpenRouter...";
            using HttpResponseMessage response = await httpClient.GetAsync("https://openrouter.ai/api/v1/models", ct);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync(ct);

            IReadOnlyList<ModelPricingEntry> merged = OpenRouterPriceParser.MergeLivePrices(_catalog.AllModels, json);
            _catalog.MergeEntries(merged);
            RefreshVisibleModels();
            StatusText = $"Synced live pricing ({DateTime.Now:HH:mm})";
        }
        catch (Exception ex)
        {
            StatusText = $"Offline catalog ({ex.GetType().Name})";
        }
    }

    private void RefreshVisibleModels()
    {
        IReadOnlyList<ModelPricingEntry> entries = _catalog.Query(
            provider: _selectedProvider,
            tier: _selectedTier,
            searchText: _searchText,
            sort: _selectedSort,
            taskInputTokens: _taskInputTokens,
            taskCachedTokens: _taskCachedTokens,
            taskOutputTokens: _taskOutputTokens,
            useFastMode: _isFastMode);

        if (entries.Count == 0)
        {
            VisibleModels = Array.Empty<ModelRowViewModel>();
            StatusText = "0 models match current filters";
            return;
        }

        decimal minTaskCost = entries.Min(e =>
            TaskCostCalculator.CalculateCost(e, _taskInputTokens, _taskCachedTokens, _taskOutputTokens, _isFastMode));

        bool cheapestAssigned = false;
        var rows = new List<ModelRowViewModel>(entries.Count);
        foreach (ModelPricingEntry entry in entries)
        {
            decimal cost = TaskCostCalculator.CalculateCost(entry, _taskInputTokens, _taskCachedTokens, _taskOutputTokens, _isFastMode);
            bool isCheapest = !cheapestAssigned && cost == minTaskCost;
            if (isCheapest)
            {
                cheapestAssigned = true;
            }

            rows.Add(new ModelRowViewModel(
                entry,
                _taskInputTokens,
                _taskCachedTokens,
                _taskOutputTokens,
                isCheapest,
                _isFastMode));
        }

        VisibleModels = rows;
        string modeLabel = _isFastMode ? "FAST MODE" : "STD MODE";
        StatusText = $"{rows.Count} models • {_selectedProvider} • {modeLabel}";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
