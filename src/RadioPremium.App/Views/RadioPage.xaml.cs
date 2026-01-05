using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using RadioPremium.Core.Models;
using RadioPremium.Core.Services;
using RadioPremium.Core.ViewModels;
using Windows.UI;
using Microsoft.UI.Xaml.Media.Animation;

namespace RadioPremium.App.Views;

/// <summary>
/// Parameter for navigation to RadioPage with specific tab/filter
/// </summary>
public record RadioNavigationParameter(RadioTab Tab, string? Filter = null);

/// <summary>
/// Radio browsing and search page - Apple-style design with 3-level navigation
/// </summary>
public sealed partial class RadioPage : Page
{
    public RadioViewModel ViewModel { get; }
    private RadioTab _currentTab = RadioTab.ForYou;
    private RadioNavigationParameter? _navigationParameter;

    public RadioPage()
    {
        InitializeComponent();
        ViewModel = App.GetService<RadioViewModel>();
        DataContext = ViewModel;

        Loaded += RadioPage_Loaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _navigationParameter = e.Parameter as RadioNavigationParameter;
    }

    private async void RadioPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
        
        // Apply navigation parameter if present
        if (_navigationParameter != null)
        {
            await ApplyNavigationParameterAsync(_navigationParameter);
            _navigationParameter = null; // Clear after use
        }
        else
        {
            UpdateTabVisuals(RadioTab.ForYou);
        }
        
        UpdateNavButtonStates(); // Initialize nav button states
    }

    private async Task ApplyNavigationParameterAsync(RadioNavigationParameter param)
    {
        _currentTab = param.Tab;
        UpdateTabVisuals(param.Tab);
        UpdateChipsVisibility(param.Tab);
        
        await ViewModel.ChangeTabCommand.ExecuteAsync(param.Tab);
        
        // Apply additional filter if specified
        if (!string.IsNullOrEmpty(param.Filter))
        {
            if (param.Filter == "trending")
            {
                // Load trending/popular stations
                await ViewModel.LoadTrendingStationsAsync();
            }
            else if (param.Filter == "genre")
            {
                // Already on Explore tab, genre chips are visible
                // User can select specific genre from chips
            }
        }
    }

    // ========== LEVEL 1: TAB NAVIGATION ==========

    private async void Tab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string tabName)
        {
            var tab = tabName switch
            {
                "ForYou" => RadioTab.ForYou,
                "Explore" => RadioTab.Explore,
                "Live" => RadioTab.Live,
                "ByCountry" => RadioTab.ByCountry,
                _ => RadioTab.ForYou
            };

            _currentTab = tab;
            UpdateTabVisuals(tab);
            UpdateChipsVisibility(tab);
            ResetChipSelection();
            
            await ViewModel.ChangeTabCommand.ExecuteAsync(tab);
        }
    }

    private void UpdateTabVisuals(RadioTab activeTab)
    {
        // Reset all tabs to inactive style
        TabForYou.Style = (Style)App.Current.Resources["TabButtonStyle"];
        TabExplore.Style = (Style)App.Current.Resources["TabButtonStyle"];
        TabLive.Style = (Style)App.Current.Resources["TabButtonStyle"];
        TabByCountry.Style = (Style)App.Current.Resources["TabButtonStyle"];

        // Set active tab
        var activeButton = activeTab switch
        {
            RadioTab.ForYou => TabForYou,
            RadioTab.Explore => TabExplore,
            RadioTab.Live => TabLive,
            RadioTab.ByCountry => TabByCountry,
            _ => TabForYou
        };
        activeButton.Style = (Style)App.Current.Resources["TabButtonActiveStyle"];

        // Show/hide featured section (only in "Para ti" tab)
        FeaturedSection.Visibility = activeTab == RadioTab.ForYou ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateChipsVisibility(RadioTab tab)
    {
        // Show genre chips for ForYou, Explore, Live
        // Show country chips for ByCountry
        GenreChipsScroll.Visibility = tab != RadioTab.ByCountry ? Visibility.Visible : Visibility.Collapsed;
        CountryChipsScroll.Visibility = tab == RadioTab.ByCountry ? Visibility.Visible : Visibility.Collapsed;
    }

    // ========== LEVEL 2: FILTER CHIPS ==========

    private async void GenreChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string genre)
        {
            UpdateGenreChipSelection(button);
            await ViewModel.FilterByGenreCommand.ExecuteAsync(genre);
        }
    }

    private async void CountryChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string country)
        {
            UpdateCountryChipSelection(button);
            await ViewModel.FilterByCountryCommand.ExecuteAsync(country);
        }
    }

    private void UpdateGenreChipSelection(Button activeChip)
    {
        // Reset all genre chips
        foreach (var child in GenreChipsPanel.Children)
        {
            if (child is Button chip)
            {
                chip.Style = (Style)App.Current.Resources["FilterChipStyle"];
            }
        }
        // Set active chip
        activeChip.Style = (Style)App.Current.Resources["FilterChipActiveStyle"];
    }

    private void UpdateCountryChipSelection(Button activeChip)
    {
        // Reset all country chips
        foreach (var child in CountryChipsPanel.Children)
        {
            if (child is Button chip)
            {
                chip.Style = (Style)App.Current.Resources["FilterChipStyle"];
            }
        }
        // Set active chip
        activeChip.Style = (Style)App.Current.Resources["FilterChipActiveStyle"];
    }

    private void ResetChipSelection()
    {
        // Reset genre chips, activate "Todos"
        foreach (var child in GenreChipsPanel.Children)
        {
            if (child is Button chip)
            {
                chip.Style = chip.Tag?.ToString() == "Todos" 
                    ? (Style)App.Current.Resources["FilterChipActiveStyle"]
                    : (Style)App.Current.Resources["FilterChipStyle"];
            }
        }
        // Reset country chips, activate "Todos"
        foreach (var child in CountryChipsPanel.Children)
        {
            if (child is Button chip)
            {
                chip.Style = chip.Tag?.ToString() == "Todos" 
                    ? (Style)App.Current.Resources["FilterChipActiveStyle"]
                    : (Style)App.Current.Resources["FilterChipStyle"];
            }
        }
    }

    // ========== LEVEL 3: SORT DROPDOWN ==========

    private async void Sort_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SortDropdown.SelectedItem is ComboBoxItem item && item.Tag is string sortTag)
        {
            var sort = sortTag switch
            {
                "Popular" => SortOption.Popular,
                "Recent" => SortOption.Recent,
                "Alphabetical" => SortOption.Alphabetical,
                _ => SortOption.Popular
            };
            await ViewModel.ChangeSortCommand.ExecuteAsync(sort);
        }
    }

    private async void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        ResetChipSelection();
        await ViewModel.ClearAllFiltersCommand.ExecuteAsync(null);
    }

    // ========== STATION INTERACTION ==========

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Station station)
        {
            ViewModel.PlayStationCommand.Execute(station);
        }
    }

    private void StationCard_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
        {
            // Scale and elevate
            border.Scale = new System.Numerics.Vector3(1.02f, 1.02f, 1f);
            border.Translation = new System.Numerics.Vector3(0, -2, 0);
            
            // Show play button
            var playButton = FindChild<Border>(border, "PlayButtonOverlay");
            if (playButton != null)
            {
                playButton.Opacity = 1;
            }
        }
    }

    private void StationCard_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
        {
            // Reset scale and position
            border.Scale = new System.Numerics.Vector3(1f, 1f, 1f);
            border.Translation = new System.Numerics.Vector3(0, 0, 0);
            
            // Hide play button
            var playButton = FindChild<Border>(border, "PlayButtonOverlay");
            if (playButton != null)
            {
                playButton.Opacity = 0;
            }
        }
    }

    // Helper to find named child in visual tree
    private T? FindChild<T>(DependencyObject parent, string childName) where T : DependencyObject
    {
        int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childrenCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            
            if (child is FrameworkElement fe && fe.Name == childName && child is T typedChild)
            {
                return typedChild;
            }
            
            var result = FindChild<T>(child, childName);
            if (result != null) return result;
        }
        return null;
    }

    private void StationCard_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border && border.DataContext is Station station)
        {
            ViewModel.PlayStationCommand.Execute(station);
        }
    }

    private void FeaturedCard_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
        {
            // Scale and elevate
            border.Scale = new System.Numerics.Vector3(1.03f, 1.03f, 1f);
            border.Translation = new System.Numerics.Vector3(0, -4, 0);

            // Show play icon
            if (border.Child is Grid grid)
            {
                foreach (var child in grid.Children)
                {
                    if (child is Border playIcon && playIcon.Name?.StartsWith("PlayIcon") == true)
                    {
                        playIcon.Opacity = 1;
                    }
                }
            }
        }
    }

    private void FeaturedCard_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
        {
            // Reset scale and position
            border.Scale = new System.Numerics.Vector3(1f, 1f, 1f);
            border.Translation = new System.Numerics.Vector3(0, 0, 0);

            // Hide play icon
            if (border.Child is Grid grid)
            {
                foreach (var child in grid.Children)
                {
                    if (child is Border playIcon && playIcon.Name?.StartsWith("PlayIcon") == true)
                    {
                        playIcon.Opacity = 0;
                    }
                }
            }
        }
    }

    private async void FeaturedCard_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border && border.Tag is string genre)
        {
            // Press feedback
            border.Scale = new System.Numerics.Vector3(0.97f, 0.97f, 1f);
            await Task.Delay(100);
            border.Scale = new System.Numerics.Vector3(1f, 1f, 1f);
            border.Translation = new System.Numerics.Vector3(0, 0, 0);

            // Filter by the genre tag
            await ViewModel.FilterByGenreCommand.ExecuteAsync(genre.Substring(0, 1).ToUpper() + genre.Substring(1));
        }
    }

    // Navigation for Featured cards carousel
    private int _featuredScrollPosition = 0;
    private const int FeaturedCardWidth = 176; // 160 + 16 spacing

    private void FeaturedPrev_Click(object sender, RoutedEventArgs e)
    {
        if (_featuredScrollPosition > 0)
        {
            _featuredScrollPosition--;
            AnimateFeaturedCards();
            UpdateNavButtonStates();
        }
    }

    private void FeaturedNext_Click(object sender, RoutedEventArgs e)
    {
        // Max scroll position (6 cards - visible cards)
        if (_featuredScrollPosition < 3)
        {
            _featuredScrollPosition++;
            AnimateFeaturedCards();
            UpdateNavButtonStates();
        }
    }

    private void UpdateNavButtonStates()
    {
        // Disable prev at start, disable next at end
        FeaturedPrevButton.Opacity = _featuredScrollPosition > 0 ? 1.0 : 0.3;
        FeaturedPrevButton.IsEnabled = _featuredScrollPosition > 0;
        
        FeaturedNextButton.Opacity = _featuredScrollPosition < 3 ? 1.0 : 0.3;
        FeaturedNextButton.IsEnabled = _featuredScrollPosition < 3;
    }

    private void AnimateFeaturedCards()
    {
        var offset = -(_featuredScrollPosition * FeaturedCardWidth);
        FeaturedCardsPanel.Translation = new System.Numerics.Vector3(offset, 0, 0);
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(RadioPremium.Core.Models.PageType.Settings);
    }

    // ========== SPOTLIGHT SEARCH HANDLERS ==========

    private DispatcherTimer? _searchDebounceTimer;
    private bool _isSearching = false;
    private int _selectedSearchIndex = -1;

    private void SearchTrigger_Click(object sender, RoutedEventArgs e)
    {
        OpenSearch();
    }

    private void OpenSearch()
    {
        SearchOverlay.Visibility = Visibility.Visible;
        SearchBox.Focus(FocusState.Programmatic);
        
        // Reset search state
        ShowQuickFilters();
    }

    private void CloseSearch_Click(object sender, RoutedEventArgs e)
    {
        CloseSearch();
    }

    private void SearchOverlay_Tapped(object sender, TappedRoutedEventArgs e)
    {
        // Close when clicking outside the panel
        CloseSearch();
    }

    private void SearchPanel_Tapped(object sender, TappedRoutedEventArgs e)
    {
        // Prevent closing when clicking inside the panel
        e.Handled = true;
    }

    private void CloseSearch()
    {
        SearchOverlay.Visibility = Visibility.Collapsed;
        SearchBox.Text = string.Empty;
        ViewModel.SearchQuery = string.Empty;
        _selectedSearchIndex = -1;
        ShowQuickFilters();
    }

    private void ShowQuickFilters()
    {
        QuickFiltersPanel.Visibility = Visibility.Visible;
        SearchResultsPanel.Visibility = Visibility.Collapsed;
        NoResultsPanel.Visibility = Visibility.Collapsed;
    }

    private void ShowSearchResults(int count)
    {
        QuickFiltersPanel.Visibility = Visibility.Collapsed;
        _selectedSearchIndex = -1; // Reset selection
        
        if (count > 0)
        {
            SearchResultsPanel.Visibility = Visibility.Visible;
            NoResultsPanel.Visibility = Visibility.Collapsed;
            ResultsCountText.Text = $"{count} RESULTADO{(count != 1 ? "S" : "")}";
        }
        else
        {
            SearchResultsPanel.Visibility = Visibility.Collapsed;
            NoResultsPanel.Visibility = Visibility.Visible;
            NoResultsText.Text = $"No se encontraron emisoras para \"{SearchBox.Text}\"";
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Debounce search - wait 300ms after user stops typing
        _searchDebounceTimer?.Stop();
        
        if (string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            ShowQuickFilters();
            return;
        }

        _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _searchDebounceTimer.Tick += async (s, args) =>
        {
            _searchDebounceTimer?.Stop();
            await PerformSearchAsync();
        };
        _searchDebounceTimer.Start();
    }

    private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            _searchDebounceTimer?.Stop();
            
            // Si hay un item seleccionado, reproducirlo
            if (_selectedSearchIndex >= 0 && _selectedSearchIndex < ViewModel.Stations.Count)
            {
                var station = ViewModel.Stations[_selectedSearchIndex];
                ViewModel.PlayStationCommand.Execute(station);
                CloseSearch();
            }
            else
            {
                _ = PerformSearchAsync();
            }
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            CloseSearch();
        }
        else if (e.Key == Windows.System.VirtualKey.Down)
        {
            // Navegar hacia abajo
            if (ViewModel.Stations.Count > 0)
            {
                _selectedSearchIndex = Math.Min(_selectedSearchIndex + 1, ViewModel.Stations.Count - 1);
                UpdateSearchSelectionVisual();
            }
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Up)
        {
            // Navegar hacia arriba
            if (ViewModel.Stations.Count > 0)
            {
                _selectedSearchIndex = Math.Max(_selectedSearchIndex - 1, 0);
                UpdateSearchSelectionVisual();
            }
            e.Handled = true;
        }
    }

    private void UpdateSearchSelectionVisual()
    {
        // Actualizar visual de selección en los resultados
        for (int i = 0; i < SearchResultsRepeater.ItemsSourceView?.Count; i++)
        {
            var element = SearchResultsRepeater.TryGetElement(i);
            if (element is Button button)
            {
                if (i == _selectedSearchIndex)
                {
                    button.Background = new SolidColorBrush(Microsoft.UI.Colors.LightGray);
                    button.Background.Opacity = 0.3;
                }
                else
                {
                    button.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                }
            }
        }
    }

    private async Task PerformSearchAsync()
    {
        if (_isSearching) return;
        
        _isSearching = true;
        SearchIcon.Visibility = Visibility.Collapsed;
        SearchSpinner.Visibility = Visibility.Visible;

        try
        {
            await ViewModel.SearchCommand.ExecuteAsync(null);
            ShowSearchResults(ViewModel.Stations.Count);
        }
        finally
        {
            SearchIcon.Visibility = Visibility.Visible;
            SearchSpinner.Visibility = Visibility.Collapsed;
            _isSearching = false;
        }
    }

    private void SearchResult_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Station station)
        {
            ViewModel.PlayStationCommand.Execute(station);
            CloseSearch();
        }
    }

    // Quick Filter Handlers
    private async void QuickFilter_Popular(object sender, RoutedEventArgs e)
    {
        CloseSearch();
        await ViewModel.LoadTopStationsCommand.ExecuteAsync(null);
    }

    private async void QuickFilter_New(object sender, RoutedEventArgs e)
    {
        CloseSearch();
        await ViewModel.LoadTopStationsCommand.ExecuteAsync(null);
    }

    private async void QuickFilter_Spain(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedCountry = "Spain";
        await PerformQuickSearch();
    }

    private async void QuickFilter_USA(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedCountry = "United States";
        await PerformQuickSearch();
    }

    private async void QuickFilter_UK(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedCountry = "United Kingdom";
        await PerformQuickSearch();
    }

    private async void QuickFilter_Genre(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string genre)
        {
            ViewModel.SearchQuery = genre;
            await PerformQuickSearch();
        }
    }

    private async Task PerformQuickSearch()
    {
        CloseSearch();
        await ViewModel.SearchCommand.ExecuteAsync(null);
    }
}
