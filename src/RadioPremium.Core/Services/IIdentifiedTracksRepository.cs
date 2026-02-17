using RadioPremium.Core.Models;

namespace RadioPremium.Core.Services;

/// <summary>
/// Repository for managing identified tracks history
/// </summary>
public interface IIdentifiedTracksRepository
{
    /// <summary>
    /// Get all identified tracks ordered by recognition date (newest first)
    /// </summary>
    Task<IReadOnlyList<IdentifiedTrackHistory>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a track to history
    /// </summary>
    Task AddAsync(IdentifiedTrackHistory track, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove a track from history
    /// </summary>
    Task RemoveAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear all history
    /// </summary>
    Task ClearAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get tracks identified from a specific station
    /// </summary>
    Task<IReadOnlyList<IdentifiedTrackHistory>> GetByStationAsync(Guid stationUuid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search tracks by title or artist
    /// </summary>
    Task<IReadOnlyList<IdentifiedTrackHistory>> SearchAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when history changes
    /// </summary>
    event EventHandler? HistoryChanged;
}
