using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using RadioPremium.Core.Messages;
using RadioPremium.Core.Models;
using RadioPremium.Core.Services;

namespace RadioPremium.Core.ViewModels;

/// <summary>
/// ViewModel for Spotify integration
/// </summary>
public partial class SpotifyViewModel : ObservableRecipient
{
    private readonly ISpotifyAuthService _authService;
    private readonly ISpotifyApiService _apiService;

    [ObservableProperty]
    private bool _isAuthenticated;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private SpotifyUser? _user;

    [ObservableProperty]
    private SpotifyPlaylist? _radioLikesPlaylist;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private Track? _pendingTrack;

    [ObservableProperty]
    private bool _isAddingTrack;

    public SpotifyViewModel(
        ISpotifyAuthService authService,
        ISpotifyApiService apiService)
    {
        _authService = authService;
        _apiService = apiService;

        _authService.AuthenticationStateChanged += OnAuthStateChanged;
        IsAuthenticated = _authService.IsAuthenticated;

        IsActive = true;
    }

    private void OnAuthStateChanged(object? sender, bool isAuthenticated)
    {
        IsAuthenticated = isAuthenticated;
        if (!isAuthenticated)
        {
            User = null;
            RadioLikesPlaylist = null;
        }
    }

    public async Task InitializeAsync()
    {
        if (IsAuthenticated)
        {
            await LoadUserProfileAsync();
        }
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var authUrl = _authService.GetAuthorizationUrl();
            
            // Open browser for authentication
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = authUrl,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al iniciar autenticación: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task HandleCallbackAsync(Uri callbackUri)
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var success = await _authService.HandleCallbackAsync(callbackUri);
            if (success)
            {
                await LoadUserProfileAsync();
                Messenger.Send(new ShowNotificationMessage(
                    "Spotify conectado",
                    $"Bienvenido, {User?.DisplayName}",
                    NotificationType.Success));
            }
            else
            {
                ErrorMessage = "Error al autenticar con Spotify";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadUserProfileAsync()
    {
        try
        {
            User = await _apiService.GetCurrentUserAsync();
            RadioLikesPlaylist = await _apiService.GetOrCreateRadioLikesPlaylistAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al cargar perfil: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _authService.LogoutAsync();
        User = null;
        RadioLikesPlaylist = null;
        Messenger.Send(new ShowNotificationMessage(
            "Spotify desconectado",
            "Has cerrado sesión en Spotify",
            NotificationType.Info));
    }

    [RelayCommand]
    private async Task AddTrackToPlaylistAsync(Track? track)
    {
        if (track is null) return;

        if (!IsAuthenticated)
        {
            PendingTrack = track;
            await LoginAsync();
            return;
        }

        try
        {
            IsAddingTrack = true;
            ErrorMessage = null;

            var success = await _apiService.AddToRadioLikesAsync(track);

            if (success)
            {
                Messenger.Send(new SpotifyAddedMessage(track, true));
                Messenger.Send(new ShowNotificationMessage(
                    "Añadido a Spotify",
                    $"{track.DisplayText} añadido a Radio Likes",
                    NotificationType.Success));
            }
            else
            {
                Messenger.Send(new SpotifyAddedMessage(track, false, "No se encontró la canción en Spotify"));
                Messenger.Send(new ShowNotificationMessage(
                    "Error",
                    "No se pudo añadir la canción a Spotify",
                    NotificationType.Error));
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            Messenger.Send(new SpotifyAddedMessage(track, false, ex.Message));
        }
        finally
        {
            IsAddingTrack = false;
        }
    }

    protected override void OnActivated()
    {
        base.OnActivated();

        Messenger.Register<AddToSpotifyMessage>(this, async (r, m) =>
        {
            await AddTrackToPlaylistAsync(m.Value);
        });
    }
}
