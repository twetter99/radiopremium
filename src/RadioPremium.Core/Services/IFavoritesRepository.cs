using RadioPremium.Core.Models;

namespace RadioPremium.Core.Services;

/// <summary>
/// Repository for managing favorite stations
/// </summary>
public interface IFavoritesRepository
{
    /// <summary>
    /// Get all favorite stations
    /// </summary>
    Task<IReadOnlyList<Station>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a station to favorites
    /// </summary>
    Task AddAsync(Station station, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove a station from favorites
    /// </summary>
    Task RemoveAsync(Guid stationUuid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a station is a favorite
    /// </summary>
    Task<bool> IsFavoriteAsync(Guid stationUuid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggle favorite status
    /// </summary>
    /// <returns>True if now a favorite, false if removed</returns>
    Task<bool> ToggleAsync(Station station, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get favorite station UUIDs for quick lookup
    /// </summary>
    Task<IReadOnlySet<Guid>> GetFavoriteIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when favorites change
    /// </summary>
    event EventHandler<FavoritesChangedEventArgs>? FavoritesChanged;
}

/// <summary>
/// Event args for favorites changes
/// </summary>
public sealed class FavoritesChangedEventArgs : EventArgs
{
    public Guid StationUuid { get; }
    public bool IsFavorite { get; }

    public FavoritesChangedEventArgs(Guid stationUuid, bool isFavorite)
    {
        StationUuid = stationUuid;
        IsFavorite = isFavorite;
    }
}
