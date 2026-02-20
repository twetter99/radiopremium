using RadioPremium.Core.Models;

namespace RadioPremium.Core.Services;

/// <summary>
/// Service for Spotify Web API operations
/// </summary>
public interface ISpotifyApiService
{
    /// <summary>
    /// Get current user profile
    /// </summary>
    Task<SpotifyUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Search for tracks
    /// </summary>
    Task<IReadOnlyList<SpotifyTrack>> SearchTracksAsync(string query, int limit = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search for a track by title and artist
    /// </summary>
    Task<SpotifyTrack?> FindTrackAsync(string title, string artist, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search for a track by ISRC
    /// </summary>
    Task<SpotifyTrack?> FindTrackByIsrcAsync(string isrc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get user's playlists
    /// </summary>
    Task<IReadOnlyList<SpotifyPlaylist>> GetUserPlaylistsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get or create the "Radio Likes" playlist
    /// </summary>
    Task<SpotifyPlaylist?> GetOrCreateRadioLikesPlaylistAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Add track to playlist
    /// </summary>
    Task<bool> AddTrackToPlaylistAsync(string playlistId, string trackUri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add recognized track to Radio Likes playlist
    /// </summary>
    Task<bool> AddToRadioLikesAsync(Track track, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if track is already in playlist
    /// </summary>
    Task<bool> IsTrackInPlaylistAsync(string playlistId, string trackUri, CancellationToken cancellationToken = default);

}
