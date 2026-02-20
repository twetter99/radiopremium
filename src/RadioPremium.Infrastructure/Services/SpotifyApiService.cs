using RadioPremium.Core.Models;
using RadioPremium.Core.Services;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace RadioPremium.Infrastructure.Services;

/// <summary>
/// Spotify Web API service implementation
/// </summary>
public sealed class SpotifyApiService : ISpotifyApiService
{
    private readonly HttpClient _httpClient;
    private readonly ISpotifyAuthService _authService;
    private readonly JsonSerializerOptions _jsonOptions;

    private const string ApiBaseUrl = "https://api.spotify.com/v1";

    public SpotifyApiService(HttpClient httpClient, ISpotifyAuthService authService)
    {
        _httpClient = httpClient;
        _authService = authService;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
    }

    private async Task<HttpRequestMessage> CreateAuthorizedRequestAsync(
        HttpMethod method,
        string endpoint,
        CancellationToken cancellationToken)
    {
        var token = await _authService.GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrEmpty(token))
        {
            throw new InvalidOperationException("No authenticated with Spotify");
        }

        var request = new HttpRequestMessage(method, $"{ApiBaseUrl}{endpoint}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    public async Task<SpotifyUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, "/me", cancellationToken);
        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var userResponse = JsonSerializer.Deserialize<SpotifyUserResponse>(json, _jsonOptions);

        if (userResponse is null) return null;

        return new SpotifyUser
        {
            Id = userResponse.Id,
            DisplayName = userResponse.DisplayName ?? userResponse.Id,
            Email = userResponse.Email ?? string.Empty,
            ImageUrl = userResponse.Images?.FirstOrDefault()?.Url,
            Country = userResponse.Country ?? string.Empty,
            Product = userResponse.Product ?? string.Empty,
            Uri = userResponse.Uri ?? string.Empty
        };
    }

    public async Task<IReadOnlyList<SpotifyTrack>> SearchTracksAsync(string query, int limit = 10, CancellationToken cancellationToken = default)
    {
        var encodedQuery = Uri.EscapeDataString(query);
        var request = await CreateAuthorizedRequestAsync(
            HttpMethod.Get,
            $"/search?q={encodedQuery}&type=track&limit={limit}",
            cancellationToken);

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<SpotifyTrack>();
        }

        var result = await response.Content.ReadFromJsonAsync<SpotifySearchResult>(_jsonOptions, cancellationToken);
        return result?.Tracks?.Items ?? new List<SpotifyTrack>();
    }

    public async Task<SpotifyTrack?> FindTrackAsync(string title, string artist, CancellationToken cancellationToken = default)
    {
        // First try exact search
        var query = $"track:{title} artist:{artist}";
        var tracks = await SearchTracksAsync(query, 5, cancellationToken);

        if (tracks.Count > 0)
        {
            return tracks.FirstOrDefault(t =>
                t.Name.Contains(title, StringComparison.OrdinalIgnoreCase) &&
                t.Artists.Any(a => a.Name.Contains(artist, StringComparison.OrdinalIgnoreCase)))
                ?? tracks[0];
        }

        // Fallback to simpler search
        query = $"{title} {artist}";
        tracks = await SearchTracksAsync(query, 5, cancellationToken);

        return tracks.FirstOrDefault();
    }

    public async Task<SpotifyTrack?> FindTrackByIsrcAsync(string isrc, CancellationToken cancellationToken = default)
    {
        var query = $"isrc:{isrc}";
        var tracks = await SearchTracksAsync(query, 1, cancellationToken);
        return tracks.FirstOrDefault();
    }

    public async Task<IReadOnlyList<SpotifyPlaylist>> GetUserPlaylistsAsync(CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, "/me/playlists?limit=50", cancellationToken);
        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<SpotifyPlaylist>();
        }

        var result = await response.Content.ReadFromJsonAsync<PlaylistsResponse>(_jsonOptions, cancellationToken);
        return result?.Items ?? new List<SpotifyPlaylist>();
    }

    public async Task<SpotifyPlaylist?> GetOrCreateRadioLikesPlaylistAsync(CancellationToken cancellationToken = default)
    {
        // Check if playlist already exists
        var playlists = await GetUserPlaylistsAsync(cancellationToken);
        var existing = playlists.FirstOrDefault(p =>
            p.Name.Equals(SpotifySettings.PlaylistName, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            return existing;
        }

        // Create new playlist
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null) return null;

        var request = await CreateAuthorizedRequestAsync(
            HttpMethod.Post,
            $"/users/{user.Id}/playlists",
            cancellationToken);

        var body = new
        {
            name = SpotifySettings.PlaylistName,
            description = SpotifySettings.PlaylistDescription,
            @public = false
        };

        request.Content = new StringContent(
            JsonSerializer.Serialize(body, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<SpotifyPlaylist>(_jsonOptions, cancellationToken);
    }

    public async Task<bool> AddTrackToPlaylistAsync(string playlistId, string trackUri, CancellationToken cancellationToken = default)
    {
        // Check if already in playlist
        if (await IsTrackInPlaylistAsync(playlistId, trackUri, cancellationToken))
        {
            return true; // Already exists, consider success
        }

        var request = await CreateAuthorizedRequestAsync(
            HttpMethod.Post,
            $"/playlists/{playlistId}/tracks",
            cancellationToken);

        var body = new { uris = new[] { trackUri } };
        request.Content = new StringContent(
            JsonSerializer.Serialize(body, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> AddToRadioLikesAsync(Track track, CancellationToken cancellationToken = default)
    {
        // Get or create playlist
        var playlist = await GetOrCreateRadioLikesPlaylistAsync(cancellationToken);
        if (playlist is null)
        {
            return false;
        }

        // Find track in Spotify
        SpotifyTrack? spotifyTrack = null;

        // Try by ISRC first (most accurate)
        if (!string.IsNullOrEmpty(track.Isrc))
        {
            spotifyTrack = await FindTrackByIsrcAsync(track.Isrc, cancellationToken);
        }

        // Fallback to title/artist search
        if (spotifyTrack is null)
        {
            spotifyTrack = await FindTrackAsync(track.Title, track.Artist, cancellationToken);
        }

        if (spotifyTrack is null)
        {
            return false;
        }

        // Add to playlist
        return await AddTrackToPlaylistAsync(playlist.Id, spotifyTrack.Uri, cancellationToken);
    }

    public async Task<bool> IsTrackInPlaylistAsync(string playlistId, string trackUri, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthorizedRequestAsync(
            HttpMethod.Get,
            $"/playlists/{playlistId}/tracks?limit=100",
            cancellationToken);

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var result = await response.Content.ReadFromJsonAsync<PlaylistTracksResponse>(_jsonOptions, cancellationToken);
        return result?.Items?.Any(i => i.Track?.Uri == trackUri) ?? false;
    }

    public async Task<bool> SaveToLikedSongsAsync(string trackId, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = await CreateAuthorizedRequestAsync(
                HttpMethod.Put,
                "/me/tracks",
                cancellationToken);

            var body = JsonSerializer.Serialize(new { ids = new[] { trackId } }, _jsonOptions);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                System.Diagnostics.Debug.WriteLine($"[Spotify] SaveToLikedSongs failed: {response.StatusCode} - {errorBody}");
                File.WriteAllText(
                    Path.Combine(AppContext.BaseDirectory, "spotify_error.log"),
                    $"[{DateTime.Now:HH:mm:ss}] PUT /me/tracks - {response.StatusCode}\nBody: {body}\nResponse: {errorBody}\n");
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Spotify] SaveToLikedSongs exception: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> IsTrackSavedAsync(string trackId, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = await CreateAuthorizedRequestAsync(
                HttpMethod.Get,
                $"/me/tracks/contains?ids={trackId}",
                cancellationToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"[Spotify] IsTrackSaved failed: {response.StatusCode}");
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<bool[]>(_jsonOptions, cancellationToken);
            return result?.FirstOrDefault() ?? false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<SpotifyTrack?> FindSpotifyTrackAsync(Track track, CancellationToken cancellationToken = default)
    {
        SpotifyTrack? spotifyTrack = null;

        // Try by ISRC first (most accurate)
        if (!string.IsNullOrEmpty(track.Isrc))
        {
            spotifyTrack = await FindTrackByIsrcAsync(track.Isrc, cancellationToken);
        }

        // Fallback to title/artist search
        if (spotifyTrack is null)
        {
            spotifyTrack = await FindTrackAsync(track.Title, track.Artist, cancellationToken);
        }

        return spotifyTrack;
    }

    public async Task<(bool Success, SpotifyTrack? SpotifyTrack, string? ErrorMessage)> SaveIdentifiedTrackToLikedSongsAsync(
        Track track, CancellationToken cancellationToken = default)
    {
        try
        {
            // Find the track on Spotify
            var spotifyTrack = await FindSpotifyTrackAsync(track, cancellationToken);

            if (spotifyTrack is null)
            {
                return (false, null, "No se encontró la canción en Spotify");
            }

            // Save to Liked Songs
            var saved = await SaveToLikedSongsAsync(spotifyTrack.Id, cancellationToken);

            if (!saved)
            {
                return (false, spotifyTrack, "Error al guardar en Liked Songs");
            }

            return (true, spotifyTrack, null);
        }
        catch (Exception ex)
        {
            return (false, null, $"Error: {ex.Message}");
        }
    }

    #region Response DTOs

    private sealed class SpotifyUserResponse
    {
        public string Id { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string? Email { get; set; }
        public string? Country { get; set; }
        public string? Product { get; set; }
        public string? Uri { get; set; }
        public List<SpotifyImage>? Images { get; set; }
    }

    private sealed class PlaylistsResponse
    {
        public List<SpotifyPlaylist> Items { get; set; } = new();
        public int Total { get; set; }
    }

    private sealed class PlaylistTracksResponse
    {
        public List<PlaylistTrackItem>? Items { get; set; }
    }

    private sealed class PlaylistTrackItem
    {
        public SpotifyTrack? Track { get; set; }
    }

    #endregion
}
