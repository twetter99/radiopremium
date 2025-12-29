using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using RadioPremium.Core.Messages;
using RadioPremium.Core.Models;
using RadioPremium.Core.Services;
using System.Collections.ObjectModel;

namespace RadioPremium.Core.ViewModels;

/// <summary>
/// ViewModel for radio station browsing and search
/// </summary>
public partial class RadioViewModel : ObservableRecipient
{
    private readonly IRadioBrowserService _radioBrowserService;
    private readonly IFavoritesRepository _favoritesRepository;
    private CancellationTokenSource? _searchCts;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedCountry = string.Empty;

    [ObservableProperty]
    private string _selectedTag = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private Station? _selectedStation;

    [ObservableProperty]
    private int _totalResults;

    public ObservableCollection<Station> Stations { get; } = new();
    public ObservableCollection<CountryInfo> Countries { get; } = new();
    public ObservableCollection<TagInfo> Tags { get; } = new();

    public RadioViewModel(
        IRadioBrowserService radioBrowserService,
        IFavoritesRepository favoritesRepository)
    {
        _radioBrowserService = radioBrowserService;
        _favoritesRepository = favoritesRepository;
        IsActive = true;
    }

    public async Task InitializeAsync()
    {
        await LoadTopStationsAsync();
        await LoadFiltersAsync();
    }

    private async Task LoadFiltersAsync()
    {
        try
        {
            var countries = await _radioBrowserService.GetCountriesAsync();
            Countries.Clear();
            foreach (var country in countries.OrderByDescending(c => c.StationCount).Take(50))
            {
                Countries.Add(country);
            }

            var tags = await _radioBrowserService.GetTagsAsync(50);
            Tags.Clear();
            foreach (var tag in tags)
            {
                Tags.Add(tag);
            }
        }
        catch
        {
            // Filters are optional, don't fail initialization
        }
    }

    [RelayCommand]
    private async Task LoadTopStationsAsync()
    {
        await SearchStationsAsync(null, null, null);
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        await SearchStationsAsync(
            string.IsNullOrWhiteSpace(SearchQuery) ? null : SearchQuery,
            string.IsNullOrWhiteSpace(SelectedCountry) ? null : SelectedCountry,
            string.IsNullOrWhiteSpace(SelectedTag) ? null : SelectedTag);
    }

    private async Task SearchStationsAsync(string? name, string? country, string? tag)
    {
        // Cancel any previous search
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            System.Diagnostics.Debug.WriteLine($"[RadioVM] Starting search: name={name}, country={country}, tag={tag}");
            
            var favoriteIds = await _favoritesRepository.GetFavoriteIdsAsync(token);
            System.Diagnostics.Debug.WriteLine($"[RadioVM] Got {favoriteIds.Count} favorite IDs");

            var stations = await _radioBrowserService.SearchAsync(
                name: name,
                country: country,
                tag: tag,
                orderBy: "clickcount",
                reverse: true,
                limit: 100,
                cancellationToken: token);
            
            System.Diagnostics.Debug.WriteLine($"[RadioVM] Got {stations.Count} stations from API");

            token.ThrowIfCancellationRequested();

            Stations.Clear();
            foreach (var station in stations)
            {
                station.IsFavorite = favoriteIds.Contains(station.StationUuid);
                Stations.Add(station);
            }

            TotalResults = Stations.Count;
        }
        catch (OperationCanceledException)
        {
            // Search was cancelled, ignore
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al buscar emisoras: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task PlayStationAsync(Station? station)
    {
        if (station is null) return;

        SelectedStation = station;
        await _radioBrowserService.ClickStationAsync(station.StationUuid);
        Messenger.Send(new PlayStationMessage(station));
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(Station? station)
    {
        if (station is null) return;

        var isFavorite = await _favoritesRepository.ToggleAsync(station);
        station.IsFavorite = isFavorite;

        // Update UI
        var index = Stations.IndexOf(station);
        if (index >= 0)
        {
            Stations[index] = station;
        }

        Messenger.Send(new FavoriteChangedMessage(station.StationUuid, isFavorite));
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchQuery = string.Empty;
        SelectedCountry = string.Empty;
        SelectedTag = string.Empty;
    }

    protected override void OnActivated()
    {
        base.OnActivated();

        Messenger.Register<FavoriteChangedMessage>(this, (r, m) =>
        {
            var station = Stations.FirstOrDefault(s => s.StationUuid == m.StationUuid);
            if (station is not null)
            {
                station.IsFavorite = m.IsFavorite;
            }
        });
    }
}
