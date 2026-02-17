using RadioPremium.Core.Models;

namespace RadioPremium.Core.Services;

/// <summary>
/// Service for managing playback queue
/// </summary>
public interface IPlaybackQueueService
{
    /// <summary>
    /// Current queue of stations
    /// </summary>
    IReadOnlyList<Station> Queue { get; }

    /// <summary>
    /// Current station index in queue
    /// </summary>
    int CurrentIndex { get; }

    /// <summary>
    /// Current station being played
    /// </summary>
    Station? CurrentStation { get; }

    /// <summary>
    /// Whether shuffle is enabled
    /// </summary>
    bool IsShuffleEnabled { get; set; }

    /// <summary>
    /// Whether repeat is enabled
    /// </summary>
    bool IsRepeatEnabled { get; set; }

    /// <summary>
    /// Whether there's a next station available
    /// </summary>
    bool HasNext { get; }

    /// <summary>
    /// Whether there's a previous station available
    /// </summary>
    bool HasPrevious { get; }

    /// <summary>
    /// Set the queue with a list of stations
    /// </summary>
    void SetQueue(IEnumerable<Station> stations, Station? startStation = null);

    /// <summary>
    /// Add station to the end of queue
    /// </summary>
    void AddToQueue(Station station);

    /// <summary>
    /// Add station after current position
    /// </summary>
    void PlayNext(Station station);

    /// <summary>
    /// Remove station from queue
    /// </summary>
    void RemoveFromQueue(Station station);

    /// <summary>
    /// Clear the queue
    /// </summary>
    void ClearQueue();

    /// <summary>
    /// Move to next station in queue
    /// </summary>
    Station? MoveNext();

    /// <summary>
    /// Move to previous station in queue
    /// </summary>
    Station? MovePrevious();

    /// <summary>
    /// Get history of played stations
    /// </summary>
    IReadOnlyList<Station> GetHistory(int count = 10);

    /// <summary>
    /// Event raised when queue changes
    /// </summary>
    event EventHandler? QueueChanged;

    /// <summary>
    /// Event raised when current station changes
    /// </summary>
    event EventHandler<Station?>? CurrentStationChanged;
}
