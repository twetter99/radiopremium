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
    private readonly string _logPath;
    private CancellationTokenSource? _identifyCts;
    private Station? _currentStation;

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

    public bool IsIdentifying => State == CaptureState.Capturing || State == CaptureState.Processing;
    public bool CanIdentify => State == CaptureState.Idle && _loopbackCaptureService.IsAvailable;

    public IdentifyViewModel(
        ILoopbackCaptureService loopbackCaptureService,
        IAcrCloudRecognitionService acrCloudService,
        IIdentifiedTracksRepository tracksRepository,
        INotificationService notificationService,
        ISettingsService settingsService)
    {
        _loopbackCaptureService = loopbackCaptureService;
        _acrCloudService = acrCloudService;
        _tracksRepository = tracksRepository;
        _notificationService = notificationService;
        _settingsService = settingsService;
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
            // Capture 10 seconds of audio
            State = CaptureState.Capturing;
            var captureResult = await _loopbackCaptureService.CaptureAsync(
                TimeSpan.FromSeconds(10),
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
