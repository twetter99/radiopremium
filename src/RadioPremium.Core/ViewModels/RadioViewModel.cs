using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using RadioPremium.Core.Messages;
using RadioPremium.Core.Models;
using RadioPremium.Core.Services;
using System.Collections.ObjectModel;

namespace RadioPremium.Core.ViewModels;

/// <summary>
/// Navigation tabs for the radio page
/// </summary>
public enum RadioTab
{
    ForYou,
    Explore,
    Live,
    ByCountry
}

/// <summary>
/// Sort options for stations list
/// </summary>
public enum SortOption
{
    Popular,
    Recent,
    Alphabetical
}

/// <summary>
/// ViewModel for radio station browsing and search
/// </summary>
public partial class RadioViewModel : ObservableRecipient
{
    private readonly IRadioBrowserService _radioBrowserService;
    private readonly IFavoritesRepository _favoritesRepository;
    private readonly ISettingsService _settingsService;
    private CancellationTokenSource? _searchCts;

    // Tags to include for music-only filter
    private static readonly HashSet<string> MusicTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "music", "pop", "rock", "jazz", "classical", "electronic", "dance", "hip-hop", "hip hop",
        "rnb", "r&b", "soul", "blues", "country", "metal", "indie", "folk", "reggae", "latin",
        "latino", "salsa", "bachata", "cumbia", "tropical", "house", "techno", "trance", "edm",
        "ambient", "chill", "lounge", "acoustic", "alternative", "punk", "grunge", "disco",
        "funk", "gospel", "christian", "opera", "symphony", "80s", "90s", "70s", "60s", "oldies",
        "hits", "top 40", "charts", "contemporary", "adult contemporary", "easy listening"
    };

    // Tags to exclude (non-music content)
    private static readonly HashSet<string> NonMusicTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "news", "talk", "sports", "politics", "religion", "comedy", "podcast", "audiobook",
        "education", "weather", "traffic", "business", "finance", "government", "public radio",
        "talk radio", "spoken word", "documentary", "drama", "children", "kids"
    };

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

    // New navigation properties
    [ObservableProperty]
    private RadioTab _currentTab = RadioTab.ForYou;

    [ObservableProperty]
    private SortOption _currentSort = SortOption.Popular;

    [ObservableProperty]
    private string _activeGenreFilter = string.Empty;

    [ObservableProperty]
    private string _activeCountryFilter = string.Empty;

    [ObservableProperty]
    private string _sectionTitle = "Emisoras populares";

    [ObservableProperty]
    private bool _hasActiveFilters;

    public ObservableCollection<Station> Stations { get; } = new();
    public ObservableCollection<CountryInfo> Countries { get; } = new();
    public ObservableCollection<TagInfo> Tags { get; } = new();

    // Genre chips for quick filtering
    public ObservableCollection<string> GenreChips { get; } = new()
    {
        "Todos", "Pop", "Rock", "Jazz", "Electrónica", "Clásica", "Chill", "Hip Hop", "Latino"
    };

    // Country chips for quick filtering
    public ObservableCollection<string> CountryChips { get; } = new()
    {
        "Todos", "España", "USA", "UK", "México", "Argentina", "Colombia", "Francia", "Alemania"
    };

    public RadioViewModel(
        IRadioBrowserService radioBrowserService,
        IFavoritesRepository favoritesRepository,
        ISettingsService settingsService)
    {
        _radioBrowserService = radioBrowserService;
        _favoritesRepository = favoritesRepository;
        _settingsService = settingsService;
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

    /// <summary>
    /// Change the active navigation tab
    /// </summary>
    [RelayCommand]
    private async Task ChangeTabAsync(RadioTab tab)
    {
        CurrentTab = tab;
        ActiveGenreFilter = string.Empty;
        ActiveCountryFilter = string.Empty;
        HasActiveFilters = false;

        switch (tab)
        {
            case RadioTab.ForYou:
                SectionTitle = "Recomendadas para ti";
                await LoadTopStationsAsync();
                break;
            case RadioTab.Explore:
                SectionTitle = "Explorar emisoras";
                await LoadTopStationsAsync();
                break;
            case RadioTab.Live:
                SectionTitle = "En vivo ahora";
                await LoadLiveStationsAsync();
                break;
            case RadioTab.ByCountry:
                SectionTitle = "Por país";
                await LoadTopStationsAsync();
                break;
        }
    }

    /// <summary>
    /// Filter by genre chip
    /// </summary>
    [RelayCommand]
    private async Task FilterByGenreAsync(string genre)
    {
        if (genre == "Todos")
        {
            ActiveGenreFilter = string.Empty;
            HasActiveFilters = !string.IsNullOrEmpty(ActiveCountryFilter);
            SectionTitle = CurrentTab == RadioTab.Explore ? "Explorar emisoras" : "Todas las emisoras";
        }
        else
        {
            ActiveGenreFilter = genre;
            HasActiveFilters = true;
            SectionTitle = $"Emisoras de {genre}";
        }

        await ApplyFiltersAsync();
    }

    /// <summary>
    /// Filter by country chip
    /// </summary>
    [RelayCommand]
    private async Task FilterByCountryAsync(string country)
    {
        if (country == "Todos")
        {
            ActiveCountryFilter = string.Empty;
            HasActiveFilters = !string.IsNullOrEmpty(ActiveGenreFilter);
            SectionTitle = CurrentTab == RadioTab.ByCountry ? "Por país" : "Todas las emisoras";
        }
        else
        {
            ActiveCountryFilter = country;
            HasActiveFilters = true;
            SectionTitle = $"Emisoras de {country}";
        }

        await ApplyFiltersAsync();
    }

    /// <summary>
    /// Change sort order
    /// </summary>
    [RelayCommand]
    private async Task ChangeSortAsync(SortOption sort)
    {
        CurrentSort = sort;
        await ApplyFiltersAsync();
    }

    /// <summary>
    /// Clear all active filters
    /// </summary>
    [RelayCommand]
    private async Task ClearAllFiltersAsync()
    {
        ActiveGenreFilter = string.Empty;
        ActiveCountryFilter = string.Empty;
        HasActiveFilters = false;
        SectionTitle = GetDefaultTitleForTab();
        await ApplyFiltersAsync();
    }

    private string GetDefaultTitleForTab() => CurrentTab switch
    {
        RadioTab.ForYou => "Recomendadas para ti",
        RadioTab.Explore => "Explorar emisoras",
        RadioTab.Live => "En vivo ahora",
        RadioTab.ByCountry => "Por país",
        _ => "Emisoras"
    };

    private async Task ApplyFiltersAsync()
    {
        var orderBy = CurrentSort switch
        {
            SortOption.Popular => "clickcount",
            SortOption.Recent => "lastchangetime",
            SortOption.Alphabetical => "name",
            _ => "clickcount"
        };

        // Map display names to API names
        var countryForApi = ActiveCountryFilter switch
        {
            "España" => "Spain",
            "USA" => "United States of America",
            "UK" => "United Kingdom",
            "México" => "Mexico",
            _ => ActiveCountryFilter
        };

        var tagForApi = ActiveGenreFilter.ToLowerInvariant() switch
        {
            "electrónica" => "electronic",
            "clásica" => "classical",
            "hip hop" => "hip-hop",
            _ => ActiveGenreFilter.ToLowerInvariant()
        };

        await SearchStationsInternalAsync(
            name: null,
            country: string.IsNullOrEmpty(countryForApi) ? null : countryForApi,
            tag: string.IsNullOrEmpty(tagForApi) ? null : tagForApi,
            orderBy: orderBy);
    }

    private async Task LoadLiveStationsAsync()
    {
        // For "Live" tab, we prioritize stations with higher current listeners
        await SearchStationsInternalAsync(null, null, null, "clickcount");
    }

    /// <summary>
    /// Load trending/popular stations - called from sidebar navigation
    /// </summary>
    public async Task LoadTrendingStationsAsync()
    {
        SectionTitle = "Tendencias";
        await SearchStationsInternalAsync(null, null, null, "clicktrend");
    }

    [RelayCommand]
    private async Task LoadTopStationsAsync()
    {
        await SearchStationsInternalAsync(null, null, null, "clickcount");
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            SectionTitle = $"Resultados para \"{SearchQuery}\"";
        }
        
        await SearchStationsInternalAsync(
            string.IsNullOrWhiteSpace(SearchQuery) ? null : SearchQuery,
            string.IsNullOrWhiteSpace(SelectedCountry) ? null : SelectedCountry,
            string.IsNullOrWhiteSpace(SelectedTag) ? null : SelectedTag,
            "clickcount");
    }

    private async Task SearchStationsInternalAsync(string? name, string? country, string? tag, string orderBy)
    {
        // Cancel any previous search
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            System.Diagnostics.Debug.WriteLine($"[RadioVM] Starting search: name={name}, country={country}, tag={tag}, orderBy={orderBy}");
            
            var favoriteIds = await _favoritesRepository.GetFavoriteIdsAsync(token);
            System.Diagnostics.Debug.WriteLine($"[RadioVM] Got {favoriteIds.Count} favorite IDs");

            // Request more stations to have room for filtering
            var requestLimit = _settingsService.Settings.MusicOnlyFilter ? 300 : 100;

            var stations = await _radioBrowserService.SearchAsync(
                name: name,
                country: country,
                tag: tag,
                orderBy: orderBy,
                reverse: orderBy != "name", // Only reverse for non-alphabetical
                limit: requestLimit,
                cancellationToken: token);
            
            System.Diagnostics.Debug.WriteLine($"[RadioVM] Got {stations.Count} stations from API");

            token.ThrowIfCancellationRequested();

            // Apply music-only filter if enabled
            var filteredStations = stations.AsEnumerable();
            if (_settingsService.Settings.MusicOnlyFilter)
            {
                filteredStations = FilterMusicOnlyStations(stations);
                System.Diagnostics.Debug.WriteLine($"[RadioVM] After music filter: {filteredStations.Count()} stations");
            }

            Stations.Clear();
            var count = 0;
            foreach (var station in filteredStations.Take(100))
            {
                station.IsFavorite = favoriteIds.Contains(station.StationUuid);
                Stations.Add(station);
                count++;
            }

            TotalResults = count;
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

    /// <summary>
    /// Filter stations to only include music stations
    /// </summary>
    private IEnumerable<Station> FilterMusicOnlyStations(IReadOnlyList<Station> stations)
    {
        foreach (var station in stations)
        {
            var tags = station.Tags?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) 
                       ?? Array.Empty<string>();

            // Exclude if has any non-music tag
            var hasNonMusicTag = tags.Any(t => NonMusicTags.Contains(t));
            if (hasNonMusicTag)
                continue;

            // Include if has any music tag OR if no tags at all (give benefit of doubt)
            var hasMusicTag = tags.Any(t => MusicTags.Contains(t));
            if (hasMusicTag || tags.Length == 0)
            {
                yield return station;
            }
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
