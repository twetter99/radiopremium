using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using RadioPremium.Core.Messages;
using RadioPremium.Core.Models;
using RadioPremium.Core.Services;

namespace RadioPremium.Core.ViewModels;

/// <summary>
/// ViewModel for track identification
/// </summary>
public partial class IdentifyViewModel : ObservableRecipient
{
    private readonly ILoopbackCaptureService _loopbackCaptureService;
    private readonly IAcrCloudRecognitionService _acrCloudService;
    private readonly IIdentifiedTracksRepository _tracksRepository;
    private readonly INotificationService _notificationService;
    private readonly ISettingsService _settingsService;
    private readonly ISpotifyApiService _spotifyApiService;
    private readonly ISpotifyAuthService _spotifyAuthService;
    private readonly string _logPath;
    private CancellationTokenSource? _identifyCts;
    private Station? _currentStation;
    private Track? _pendingSaveTrack; // track waiting for re-auth before save

    [ObservableProperty]
    private CaptureState _state = CaptureState.Idle;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private Track? _identifiedTrack;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isDialogOpen;

    [ObservableProperty]
    private bool _isSavingToSpotify;

    [ObservableProperty]
    private bool _savedToSpotify;

    [ObservableProperty]
    private string? _spotifyStatusMessage;

    [ObservableProperty]
    private string? _spotifyArtworkUrl;

    [ObservableProperty]
    private string? _spotifyTrackName;

    [ObservableProperty]
    private string? _spotifyArtistName;

    public bool IsIdentifying => State == CaptureState.Capturing || State == CaptureState.Processing;
    public bool CanIdentify => State == CaptureState.Idle && _loopbackCaptureService.IsAvailable;

    public IdentifyViewModel(
        ILoopbackCaptureService loopbackCaptureService,
        IAcrCloudRecognitionService acrCloudService,
        IIdentifiedTracksRepository tracksRepository,
        INotificationService notificationService,
        ISettingsService settingsService,
        ISpotifyApiService spotifyApiService,
        ISpotifyAuthService spotifyAuthService)
    {
        _loopbackCaptureService = loopbackCaptureService;
        _acrCloudService = acrCloudService;
        _tracksRepository = tracksRepository;
        _notificationService = notificationService;
        _settingsService = settingsService;
        _spotifyApiService = spotifyApiService;
        _spotifyAuthService = spotifyAuthService;
        _logPath = Path.Combine(AppContext.BaseDirectory, "identify.log");

        File.AppendAllText(_logPath, $"\n[{DateTime.Now:HH:mm:ss}] IdentifyViewModel created\n");

        _loopbackCaptureService.StateChanged += OnCaptureStateChanged;
        _loopbackCaptureService.ProgressChanged += OnCaptureProgressChanged;

        File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] IsAvailable: {_loopbackCaptureService.IsAvailable}\n");

        IsActive = true;
        File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] IsActive set to true\n");
    }

    private void OnCaptureStateChanged(object? sender, CaptureState state)
    {
        State = state;
        OnPropertyChanged(nameof(IsIdentifying));
        OnPropertyChanged(nameof(CanIdentify));

        Messenger.Send(new IdentificationStateChangedMessage(state, Progress));
    }

    private void OnCaptureProgressChanged(object? sender, double progress)
    {
        Progress = progress;
        Messenger.Send(new IdentificationStateChangedMessage(State, progress));
    }

    [RelayCommand]
    private async Task IdentifyAsync()
    {
        // Check if already identifying
        if (State != CaptureState.Idle)
        {
            return;
        }

        // Check if audio device is available
        if (!_loopbackCaptureService.IsAvailable)
        {
            ErrorMessage = "No hay dispositivo de audio disponible";
            Messenger.Send(new ShowNotificationMessage("Error", ErrorMessage, NotificationType.Error));
            return;
        }

        _identifyCts?.Cancel();
        _identifyCts = new CancellationTokenSource();
        var token = _identifyCts.Token;

        ErrorMessage = null;
        IdentifiedTrack = null;
        Progress = 0;

        try
        {
            // Capture audio using configured duration
            var captureSecs = _settingsService.Settings.CaptureSeconds;
            if (captureSecs <= 0) captureSecs = 10;
            State = CaptureState.Capturing;
            var captureResult = await _loopbackCaptureService.CaptureAsync(
                TimeSpan.FromSeconds(captureSecs),
                token);

            if (!captureResult.Success)
            {
                ErrorMessage = captureResult.ErrorMessage ?? "Error al capturar audio";
                State = CaptureState.Error;
                return;
            }

            // Identify with ACRCloud
            State = CaptureState.Processing;
            Progress = 0.9;

            var identifyResult = await _acrCloudService.IdentifyAsync(captureResult, token);

            if (identifyResult.Success && identifyResult.Track is not null)
            {
                IdentifiedTrack = identifyResult.Track;
                IsDialogOpen = true;

                // Reset Spotify state for dialog
                IsSavingToSpotify = false;
                SavedToSpotify = false;
                SpotifyStatusMessage = null;
                SpotifyArtworkUrl = null;
                SpotifyTrackName = null;
                SpotifyArtistName = null;

                // Save to history
                var history = new IdentifiedTrackHistory
                {
                    Track = identifyResult.Track,
                    IdentifiedAt = DateTime.Now,
                    StationUuid = _currentStation?.StationUuid,
                    StationName = _currentStation?.Name,
                    StationCountry = _currentStation?.Country,
                    StationFavicon = _currentStation?.Favicon
                };
                await _tracksRepository.AddAsync(history, token);

                File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] Sending TrackIdentifiedMessage: {identifyResult.Track.Title}\n");

                // Show Windows notification if enabled
                if (_settingsService.Settings.ShowNotifications)
                {
                    _notificationService.ShowTrackIdentifiedNotification(
                        identifyResult.Track.Title,
                        identifyResult.Track.Artist,
                        identifyResult.Track.ArtworkUrl,
                        _currentStation?.Name);
                }

                WeakReferenceMessenger.Default.Send(new TrackIdentifiedMessage(identifyResult.Track));
                WeakReferenceMessenger.Default.Send(new ShowNotificationMessage(
                    "Canción identificada",
                    identifyResult.Track.DisplayText,
                    NotificationType.Success));

                // Auto-save to Spotify Liked Songs if authenticated
                _ = SaveToSpotifyLikedSongsAsync(identifyResult.Track, token);
            }
            else
            {
                ErrorMessage = identifyResult.ErrorMessage ?? "No se pudo identificar la canción";
                Messenger.Send(new ShowNotificationMessage(
                    "Sin resultados",
                    ErrorMessage,
                    NotificationType.Warning));
            }

            Progress = 1.0;
            State = CaptureState.Idle;
        }
        catch (OperationCanceledException)
        {
            State = CaptureState.Idle;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
            State = CaptureState.Error;
        }
    }

    [RelayCommand]
    private void CancelIdentify()
    {
        _identifyCts?.Cancel();
        State = CaptureState.Idle;
        Progress = 0;
    }

    [RelayCommand]
    private void CloseDialog()
    {
        IsDialogOpen = false;
    }

    /// <summary>
    /// Automatically saves the identified track to Spotify Liked Songs.
    /// Runs in the background after identification completes.
    /// </summary>
    private async Task SaveToSpotifyLikedSongsAsync(Track track, CancellationToken cancellationToken)
    {
        try
        {
            IsSavingToSpotify = true;
            SpotifyStatusMessage = "Guardando en Spotify...";
            File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] Auto-saving to Spotify Liked Songs: {track.DisplayText}\n");

            var (success, spotifyTrack, errorMessage) = await _spotifyApiService.SaveIdentifiedTrackToLikedSongsAsync(track, cancellationToken);

            if (success && spotifyTrack is not null)
            {
                ApplySpotifySuccess(track, spotifyTrack);
            }
            else if (errorMessage == "SCOPE_ERROR")
            {
                _pendingSaveTrack = track; // remember so ReconnectAndSave can retry
                SpotifyStatusMessage = "Permisos insuficientes. Pulsa 'Reconectar Spotify'.";
                File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] Spotify scope error - waiting for user to reconnect\n");
            }
            else
            {
                SpotifyStatusMessage = FriendlyError(errorMessage);
                File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] Failed to save to Spotify: {errorMessage}\n");
            }
        }
        catch (Exception ex)
        {
            SpotifyStatusMessage = "Error al guardar en Spotify";
            File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] Error saving to Spotify: {ex.Message}\n");
        }
        finally
        {
            IsSavingToSpotify = false;
        }
    }

    /// <summary>
    /// Logs out, starts a fresh OAuth flow, opens browser, waits for callback, then retries saving.
    /// Called from the 'Reconectar Spotify' button in the track dialog.
    /// </summary>
    [RelayCommand]
    private async Task ReconnectAndSaveAsync()
    {
        var track = _pendingSaveTrack ?? IdentifiedTrack;
        if (track is null) return;

        try
        {
            IsSavingToSpotify = true;
            SpotifyStatusMessage = "Abriendo Spotify...";
            File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] ReconnectAndSave started\n");

            // Clear stale token so StartLoginFlow gets a clean state
            await _spotifyAuthService.LogoutAsync();

            // Start OAuth flow — also cancels any previous stuck listener
            var (authUrl, completionTask) = _spotifyAuthService.StartLoginFlow();
            WeakReferenceMessenger.Default.Send(new OpenUrlMessage(authUrl));

            SpotifyStatusMessage = "Esperando autorización en el navegador...";
            var loginSuccess = await completionTask;

            if (!loginSuccess)
            {
                SpotifyStatusMessage = "Autorización cancelada. Vuelve a intentarlo.";
                File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] ReconnectAndSave: login cancelled/failed\n");
                return;
            }

            SpotifyStatusMessage = "Guardando en Spotify...";
            File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] ReconnectAndSave: login OK, retrying save\n");

            var (success, spotifyTrack, error) = await _spotifyApiService.SaveIdentifiedTrackToLikedSongsAsync(track);
            if (success && spotifyTrack is not null)
            {
                _pendingSaveTrack = null;
                ApplySpotifySuccess(track, spotifyTrack);
            }
            else if (error == "SCOPE_ERROR")
            {
                // Still can't save even after re-auth — show reconnect button again
                _pendingSaveTrack = track;
                SpotifyStatusMessage = "Permisos insuficientes. Pulsa 'Reconectar Spotify'.";
                File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] ReconnectAndSave: retry also got SCOPE_ERROR\n");
            }
            else
            {
                SpotifyStatusMessage = FriendlyError(error);
                File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] ReconnectAndSave: retry failed: {error}\n");
            }
        }
        catch (Exception ex)
        {
            SpotifyStatusMessage = "Error al reconectar";
            File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] ReconnectAndSave exception: {ex.Message}\n");
        }
        finally
        {
            IsSavingToSpotify = false;
        }
    }

    private static string FriendlyError(string? error) => error switch
    {
        "SCOPE_ERROR" => "Permisos insuficientes. Pulsa 'Reconectar Spotify'.",
        "FORBIDDEN_ERROR" => "Spotify rechazó la operación (403). Revisa en Spotify Dashboard > Users and Access que esta cuenta tenga acceso a la app.",
        null => "No se encontró en Spotify",
        _ => error
    };

    private void ApplySpotifySuccess(Track track, SpotifyTrack spotifyTrack)
    {
        SavedToSpotify = true;
        SpotifyStatusMessage = "♥ Guardada en tus Me gusta de Spotify";
        SpotifyArtworkUrl = spotifyTrack.ArtworkUrl;
        SpotifyTrackName = spotifyTrack.Name;
        SpotifyArtistName = spotifyTrack.PrimaryArtist;

        // Update the track's artwork if we got a better one from Spotify
        if (!string.IsNullOrEmpty(spotifyTrack.ArtworkUrl) && string.IsNullOrEmpty(track.ArtworkUrl))
        {
            track.ArtworkUrl = spotifyTrack.ArtworkUrl;
            IdentifiedTrack = null; // Force rebind
            IdentifiedTrack = track;
        }

        File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] Saved to Spotify Liked Songs: {spotifyTrack.Name} by {spotifyTrack.PrimaryArtist}\n");

        WeakReferenceMessenger.Default.Send(new ShowNotificationMessage(
            "Guardada en Spotify",
            $"{spotifyTrack.Name} - {spotifyTrack.PrimaryArtist} añadida a Me gusta",
            NotificationType.Success));
    }

    [RelayCommand]
    private void AddToSpotify()
    {
        if (IdentifiedTrack is not null)
        {
            Messenger.Send(new AddToSpotifyMessage(IdentifiedTrack));
        }
    }

    protected override void OnActivated()
    {
        base.OnActivated();
        File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] OnActivated called\n");

        // Listen for current station changes
        Messenger.Register<PlaybackStateChangedMessage>(this, (r, m) =>
        {
            _currentStation = m.Station;
        });

        Messenger.Register<RequestIdentifyMessage>(this, async (r, m) =>
        {
            File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] RequestIdentifyMessage received. CanIdentify: {CanIdentify}\n");
            if (CanIdentify)
            {
                File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] Starting IdentifyAsync\n");
                await IdentifyAsync();
            }
            else
            {
                File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] Cannot identify - State: {State}, IsAvailable: {_loopbackCaptureService.IsAvailable}\n");
            }
        });

        File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] Message registered\n");
    }
}
