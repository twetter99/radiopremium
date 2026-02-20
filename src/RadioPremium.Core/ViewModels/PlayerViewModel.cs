using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using RadioPremium.Core.Messages;
using RadioPremium.Core.Models;
using RadioPremium.Core.Services;

namespace RadioPremium.Core.ViewModels;

/// <summary>
/// ViewModel for audio playback controls
/// </summary>
public partial class PlayerViewModel : ObservableRecipient
{
    private readonly IAudioPlayerService _audioPlayerService;
    private readonly IFavoritesRepository _favoritesRepository;
    private readonly INotificationService _notificationService;
    private readonly ISettingsService _settingsService;
    private readonly IPlaybackQueueService _queueService;

    [ObservableProperty]
    private Station? _currentStation;

    [ObservableProperty]
    private PlaybackState _playbackState = PlaybackState.Stopped;

    [ObservableProperty]
    private float _volume = 0.8f;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private bool _isCurrentStationFavorite;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isShuffleEnabled;

    [ObservableProperty]
    private bool _isRepeatEnabled;

    public bool IsPlaying => PlaybackState == PlaybackState.Playing;
    public bool IsStopped => PlaybackState == PlaybackState.Stopped;
    public bool IsLoading => PlaybackState == PlaybackState.Loading;
    public bool CanPlay => CurrentStation is not null && PlaybackState != PlaybackState.Loading;
    public bool CanPlayNext => _queueService.HasNext;
    public bool CanPlayPrevious => _queueService.HasPrevious;

    private bool _suppressSettingsSave;

    public PlayerViewModel(
        IAudioPlayerService audioPlayerService,
        IFavoritesRepository favoritesRepository,
        INotificationService notificationService,
        ISettingsService settingsService,
        IPlaybackQueueService queueService)
    {
        _audioPlayerService = audioPlayerService;
        _favoritesRepository = favoritesRepository;
        _notificationService = notificationService;
        _settingsService = settingsService;
        _queueService = queueService;

        _audioPlayerService.StateChanged += OnPlaybackStateChanged;
        _audioPlayerService.ErrorOccurred += OnPlaybackError;
        _queueService.QueueChanged += OnQueueChanged;

        // React to settings changes made from the Settings page
        _settingsService.SettingsChanged += OnSettingsChanged;

        IsActive = true;
    }

    /// <summary>
    /// Applies persisted settings (called once at startup after LoadAsync).
    /// </summary>
    public void ApplySettings(AppSettings settings)
    {
        _suppressSettingsSave = true;
        Volume = settings.Volume;
        _suppressSettingsSave = false;
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        // Sync volume if it changed from the Settings page
        if (Math.Abs(Volume - settings.Volume) > 0.001f)
        {
            _suppressSettingsSave = true;
            Volume = settings.Volume;
            _suppressSettingsSave = false;
        }
    }

    private void OnQueueChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CanPlayNext));
        OnPropertyChanged(nameof(CanPlayPrevious));
    }

    private void OnPlaybackStateChanged(object? sender, PlaybackState state)
    {
        PlaybackState = state;
        OnPropertyChanged(nameof(IsPlaying));
        OnPropertyChanged(nameof(IsStopped));
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(CanPlay));

        Messenger.Send(new PlaybackStateChangedMessage(state, CurrentStation));

        // Show notification when playback starts (if enabled)
        if (state == PlaybackState.Playing && CurrentStation is not null && _settingsService.Settings.ShowNotifications)
        {
            _notificationService.ShowNotificationWithImage(
                "Reproduciendo",
                CurrentStation.Name,
                CurrentStation.Favicon ?? string.Empty,
                NotificationPriority.Normal);
        }
    }

    private void OnPlaybackError(object? sender, string error)
    {
        ErrorMessage = error;

        // Show Windows notification for errors (if enabled)
        if (_settingsService.Settings.ShowNotifications)
        {
            _notificationService.ShowNotification(
                "Error de reproducción",
                error,
                NotificationPriority.High);
        }

        Messenger.Send(new ShowNotificationMessage("Error de reproducción", error, NotificationType.Error));
    }

    [RelayCommand]
    private async Task PlayAsync(Station? station)
    {
        if (station is null) return;

        ErrorMessage = null;
        CurrentStation = station;
        IsCurrentStationFavorite = await _favoritesRepository.IsFavoriteAsync(station.StationUuid);

        await _audioPlayerService.PlayAsync(station);
    }

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private async Task TogglePlayPauseAsync()
    {
        if (CurrentStation is null) return;

        await _audioPlayerService.TogglePlayPauseAsync();
    }

    [RelayCommand]
    private void Stop()
    {
        _audioPlayerService.Stop();
        CurrentStation = null;
        ErrorMessage = null;
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        if (CurrentStation is null) return;

        IsCurrentStationFavorite = await _favoritesRepository.ToggleAsync(CurrentStation);
        CurrentStation.IsFavorite = IsCurrentStationFavorite;

        Messenger.Send(new FavoriteChangedMessage(CurrentStation.StationUuid, IsCurrentStationFavorite));
    }

    [RelayCommand]
    private void RequestIdentify()
    {
        Messenger.Send(RequestIdentifyMessage.Instance);
    }

    [RelayCommand(CanExecute = nameof(CanPlayNext))]
    private async Task PlayNextAsync()
    {
        var nextStation = _queueService.MoveNext();
        if (nextStation != null)
        {
            await PlayAsync(nextStation);
        }
    }

    [RelayCommand(CanExecute = nameof(CanPlayPrevious))]
    private async Task PlayPreviousAsync()
    {
        var previousStation = _queueService.MovePrevious();
        if (previousStation != null)
        {
            await PlayAsync(previousStation);
        }
    }

    [RelayCommand]
    private void ToggleShuffle()
    {
        IsShuffleEnabled = !IsShuffleEnabled;
        _queueService.IsShuffleEnabled = IsShuffleEnabled;
    }

    [RelayCommand]
    private void ToggleRepeat()
    {
        IsRepeatEnabled = !IsRepeatEnabled;
        _queueService.IsRepeatEnabled = IsRepeatEnabled;
    }

    partial void OnVolumeChanged(float value)
    {
        _audioPlayerService.Volume = value;
        Messenger.Send(new VolumeChangedMessage(value));

        // Persist volume to settings (unless we're applying settings to avoid infinite loop)
        if (!_suppressSettingsSave)
        {
            _settingsService.Settings.Volume = value;
            _ = _settingsService.SaveAsync();
        }
    }

    partial void OnIsMutedChanged(bool value)
    {
        _audioPlayerService.IsMuted = value;
    }

    protected override void OnActivated()
    {
        base.OnActivated();

        Messenger.Register<PlayStationMessage>(this, async (r, m) =>
        {
            await PlayAsync(m.Value);
        });

        Messenger.Register<SetQueueMessage>(this, async (r, m) =>
        {
            _queueService.SetQueue(m.Stations, m.StartStation);
            if (m.StartStation != null)
            {
                await PlayAsync(m.StartStation);
            }
        });

        Messenger.Register<FavoriteChangedMessage>(this, (r, m) =>
        {
            if (CurrentStation?.StationUuid == m.StationUuid)
            {
                IsCurrentStationFavorite = m.IsFavorite;
            }
        });
    }
}
