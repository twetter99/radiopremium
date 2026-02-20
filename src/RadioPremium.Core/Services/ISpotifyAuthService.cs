using RadioPremium.Core.Models;

namespace RadioPremium.Core.Services;

/// <summary>
/// Service for Spotify OAuth2 authentication with PKCE
/// </summary>
public interface ISpotifyAuthService
{
    /// <summary>
    /// Whether user is authenticated
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Current access token (refreshed automatically if expired)
    /// </summary>
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Start OAuth2 PKCE authentication flow
    /// </summary>
    /// <returns>Authorization URL to open in browser</returns>
    string GetAuthorizationUrl();

    /// <summary>
    /// Handle OAuth callback and exchange code for tokens
    /// </summary>
    /// <param name="callbackUri">Full callback URI with code</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<bool> HandleCallbackAsync(Uri callbackUri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Logout and clear stored tokens
    /// </summary>
    Task LogoutAsync();

    /// <summary>
    /// Start the full OAuth2 login flow: generate URL, start a localhost HTTP listener,
    /// and wait for the callback. Returns the auth URL to open in a browser.
    /// After the user authorizes, the callback is automatically handled.
    /// </summary>
    /// <returns>Tuple of (authUrl, completion task that resolves to true if login succeeded)</returns>
    (string AuthUrl, Task<bool> CompletionTask) StartLoginFlow(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the current authenticated session has all required Spotify scopes.
    /// </summary>
    bool HasScopes(params string[] requiredScopes);

    /// <summary>
    /// Event raised when authentication state changes
    /// </summary>
    event EventHandler<bool>? AuthenticationStateChanged;
}
