using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using RadioPremium.Core.Messages;
using RadioPremium.Core.Models;
using RadioPremium.Core.Services;
using System.Collections.ObjectModel;

namespace RadioPremium.Core.ViewModels;

/// <summary>
/// ViewModel for favorites page
/// </summary>
public partial class FavoritesViewModel : ObservableRecipient
{
    private readonly IFavoritesRepository _favoritesRepository;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private Station? _selectedStation;

    public ObservableCollection<Station> Favorites { get; } = new();

    public bool HasFavorites => Favorites.Count > 0;

    public FavoritesViewModel(IFavoritesRepository favoritesRepository)
    {
        _favoritesRepository = favoritesRepository;
        _favoritesRepository.FavoritesChanged += OnFavoritesChanged;
        IsActive = true;
    }

    private async void OnFavoritesChanged(object? sender, FavoritesChangedEventArgs e)
    {
        if (e.IsFavorite)
        {
            // Reload to get the new favorite
            await LoadFavoritesAsync();
        }
        else
        {
            var station = Favorites.FirstOrDefault(s => s.StationUuid == e.StationUuid);
            if (station is not null)
            {
                Favorites.Remove(station);
                OnPropertyChanged(nameof(HasFavorites));
            }
        }
    }

    public async Task InitializeAsync()
    {
        await LoadFavoritesAsync();
    }

    [RelayCommand]
    private async Task LoadFavoritesAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var favorites = await _favoritesRepository.GetAllAsync();

            Favorites.Clear();
            foreach (var station in favorites)
            {
                station.IsFavorite = true;
                Favorites.Add(station);
            }

            OnPropertyChanged(nameof(HasFavorites));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al cargar favoritos: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void PlayStation(Station? station)
    {
        if (station is null) return;

        SelectedStation = station;

        // Send queue with all favorites and start with the selected station
        Messenger.Send(new SetQueueMessage(Favorites, station));
    }

    [RelayCommand]
    private async Task RemoveFavoriteAsync(Station? station)
    {
        if (station is null) return;

        await _favoritesRepository.RemoveAsync(station.StationUuid);
        Favorites.Remove(station);
        OnPropertyChanged(nameof(HasFavorites));

        Messenger.Send(new FavoriteChangedMessage(station.StationUuid, false));
    }

    protected override void OnActivated()
    {
        base.OnActivated();

        Messenger.Register<FavoriteChangedMessage>(this, async (r, m) =>
        {
            if (m.IsFavorite)
            {
                await LoadFavoritesAsync();
            }
        });
    }
}
