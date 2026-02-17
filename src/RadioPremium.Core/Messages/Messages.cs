using CommunityToolkit.Mvvm.Messaging.Messages;
using RadioPremium.Core.Models;

namespace RadioPremium.Core.Messages;

/// <summary>
/// Message to request playing a station
/// </summary>
public sealed class PlayStationMessage : ValueChangedMessage<Station>
{
    public PlayStationMessage(Station station) : base(station) { }
}

/// <summary>
/// Message to set playback queue with a list of stations
/// </summary>
public sealed class SetQueueMessage
{
    public IEnumerable<Station> Stations { get; }
    public Station? StartStation { get; }

    public SetQueueMessage(IEnumerable<Station> stations, Station? startStation = null)
    {
        Stations = stations;
        StartStation = startStation;
    }
}

/// <summary>
/// Message when playback state changes
/// </summary>
public sealed class PlaybackStateChangedMessage : ValueChangedMessage<PlaybackState>
{
    public Station? Station { get; }

    public PlaybackStateChangedMessage(PlaybackState state, Station? station = null) : base(state)
    {
        Station = station;
    }
}

/// <summary>
/// Message when a track is identified
/// </summary>
public sealed class TrackIdentifiedMessage : ValueChangedMessage<Track>
{
    public TrackIdentifiedMessage(Track track) : base(track) { }
}

/// <summary>
/// Message to request track identification
/// </summary>
public sealed class RequestIdentifyMessage
{
    public static RequestIdentifyMessage Instance { get; } = new();
}

/// <summary>
/// Message when identification state changes
/// </summary>
public sealed class IdentificationStateChangedMessage : ValueChangedMessage<CaptureState>
{
    public double Progress { get; }

    public IdentificationStateChangedMessage(CaptureState state, double progress = 0) : base(state)
    {
        Progress = progress;
    }
}

/// <summary>
/// Message to request adding track to Spotify
/// </summary>
public sealed class AddToSpotifyMessage : ValueChangedMessage<Track>
{
    public AddToSpotifyMessage(Track track) : base(track) { }
}

/// <summary>
/// Message when Spotify add operation completes
/// </summary>
public sealed class SpotifyAddedMessage
{
    public Track Track { get; }
    public bool Success { get; }
    public string? ErrorMessage { get; }

    public SpotifyAddedMessage(Track track, bool success, string? errorMessage = null)
    {
        Track = track;
        Success = success;
        ErrorMessage = errorMessage;
    }
}

/// <summary>
/// Message for navigation requests
/// </summary>
public sealed class NavigateMessage : ValueChangedMessage<PageType>
{
    public object? Parameter { get; }

    public NavigateMessage(PageType pageType, object? parameter = null) : base(pageType)
    {
        Parameter = parameter;
    }
}

/// <summary>
/// Message for showing track identify dialog
/// </summary>
public sealed class ShowIdentifyDialogMessage : ValueChangedMessage<Track?>
{
    public ShowIdentifyDialogMessage(Track? track) : base(track) { }
}

/// <summary>
/// Message for favorite status changes
/// </summary>
public sealed class FavoriteChangedMessage
{
    public Guid StationUuid { get; }
    public bool IsFavorite { get; }

    public FavoriteChangedMessage(Guid stationUuid, bool isFavorite)
    {
        StationUuid = stationUuid;
        IsFavorite = isFavorite;
    }
}

/// <summary>
/// Message for volume changes
/// </summary>
public sealed class VolumeChangedMessage : ValueChangedMessage<float>
{
    public VolumeChangedMessage(float volume) : base(volume) { }
}

/// <summary>
/// Message to show notification
/// </summary>
public sealed class ShowNotificationMessage
{
    public string Title { get; }
    public string Message { get; }
    public NotificationType Type { get; }

    public ShowNotificationMessage(string title, string message, NotificationType type = NotificationType.Info)
    {
        Title = title;
        Message = message;
        Type = type;
    }
}

public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error
}
