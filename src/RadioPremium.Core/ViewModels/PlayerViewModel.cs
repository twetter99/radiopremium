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

    public bool IsPlaying => PlaybackState == PlaybackState.Playing;
    public bool IsStopped => PlaybackState == PlaybackState.Stopped;
    public bool IsLoading => PlaybackState == PlaybackState.Loading;
    public bool CanPlay => CurrentStation is not null && PlaybackState != PlaybackState.Loading;

    public PlayerViewModel(
        IAudioPlayerService audioPlayerService,
        IFavoritesRepository favoritesRepository)
    {
        _audioPlayerService = audioPlayerService;
        _favoritesRepository = favoritesRepository;

        _audioPlayerService.StateChanged += OnPlaybackStateChanged;
        _audioPlayerService.ErrorOccurred += OnPlaybackError;

        IsActive = true;
    }

    private void OnPlaybackStateChanged(object? sender, PlaybackState state)
    {
        PlaybackState = state;
        OnPropertyChanged(nameof(IsPlaying));
        OnPropertyChanged(nameof(IsStopped));
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(CanPlay));

        Messenger.Send(new PlaybackStateChangedMessage(state, CurrentStation));
    }

    private void OnPlaybackError(object? sender, string error)
    {
        ErrorMessage = error;
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

    partial void OnVolumeChanged(float value)
    {
        _audioPlayerService.Volume = value;
        Messenger.Send(new VolumeChangedMessage(value));
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

        Messenger.Register<FavoriteChangedMessage>(this, (r, m) =>
        {
            if (CurrentStation?.StationUuid == m.StationUuid)
            {
                IsCurrentStationFavorite = m.IsFavorite;
            }
        });
    }
}
