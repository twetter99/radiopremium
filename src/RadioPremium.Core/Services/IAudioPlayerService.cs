using RadioPremium.Core.Models;

namespace RadioPremium.Core.Services;

/// <summary>
/// Service for audio playback
/// </summary>
public interface IAudioPlayerService : IDisposable
{
    /// <summary>
    /// Current playback state
    /// </summary>
    PlaybackState State { get; }

    /// <summary>
    /// Current volume (0.0 to 1.0)
    /// </summary>
    float Volume { get; set; }

    /// <summary>
    /// Whether audio is muted
    /// </summary>
    bool IsMuted { get; set; }

    /// <summary>
    /// Currently playing station
    /// </summary>
    Station? CurrentStation { get; }

    /// <summary>
    /// Event raised when playback state changes
    /// </summary>
    event EventHandler<PlaybackState>? StateChanged;

    /// <summary>
    /// Event raised when an error occurs
    /// </summary>
    event EventHandler<string>? ErrorOccurred;

    /// <summary>
    /// Start playing a station
    /// </summary>
    Task PlayAsync(Station station, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pause playback
    /// </summary>
    void Pause();

    /// <summary>
    /// Resume playback
    /// </summary>
    void Resume();

    /// <summary>
    /// Stop playback
    /// </summary>
    void Stop();

    /// <summary>
    /// Toggle play/pause
    /// </summary>
    Task TogglePlayPauseAsync();
}
