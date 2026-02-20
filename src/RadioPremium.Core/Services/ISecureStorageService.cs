namespace RadioPremium.Core.Services;

/// <summary>
/// Service for secure storage of sensitive data using Windows Credential Manager
/// </summary>
public interface ISecureStorageService
{
    /// <summary>
    /// Store a value securely
    /// </summary>
    /// <param name="key">Key identifier</param>
    /// <param name="value">Value to store</param>
    Task SetAsync(string key, string value);

    /// <summary>
    /// Retrieve a stored value
    /// </summary>
    /// <param name="key">Key identifier</param>
    /// <returns>Stored value or null if not found</returns>
    Task<string?> GetAsync(string key);

    /// <summary>
    /// Remove a stored value
    /// </summary>
    /// <param name="key">Key identifier</param>
    Task RemoveAsync(string key);

    /// <summary>
    /// Check if a key exists
    /// </summary>
    /// <param name="key">Key identifier</param>
    Task<bool> ContainsKeyAsync(string key);

    /// <summary>
    /// Clear all stored credentials for this app
    /// </summary>
    Task ClearAllAsync();
}

/// <summary>
/// Credential keys used by the application
/// </summary>
public static class CredentialKeys
{
    public const string SpotifyAccessToken = "RadioPremium_Spotify_AccessToken";
    public const string SpotifyRefreshToken = "RadioPremium_Spotify_RefreshToken";
    public const string SpotifyTokenExpiry = "RadioPremium_Spotify_TokenExpiry";
    public const string SpotifyGrantedScopes = "RadioPremium_Spotify_GrantedScopes";
    public const string SpotifyCodeVerifier = "RadioPremium_Spotify_CodeVerifier";
}
