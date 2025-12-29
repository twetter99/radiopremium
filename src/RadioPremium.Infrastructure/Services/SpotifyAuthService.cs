using RadioPremium.Core.Models;
using RadioPremium.Core.Services;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;

namespace RadioPremium.Infrastructure.Services;

/// <summary>
/// Spotify OAuth2 authentication service with PKCE
/// </summary>
public sealed class SpotifyAuthService : ISpotifyAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ISecureStorageService _secureStorage;
    private readonly SpotifySettings _settings;
    private readonly JsonSerializerOptions _jsonOptions;

    private string? _codeVerifier;
    private SpotifyTokens? _tokens;

    private const string TokenUrl = "https://accounts.spotify.com/api/token";
    private const string AuthorizeUrl = "https://accounts.spotify.com/authorize";

    public bool IsAuthenticated => _tokens is not null && !string.IsNullOrEmpty(_tokens.RefreshToken);

    public event EventHandler<bool>? AuthenticationStateChanged;

    public SpotifyAuthService(
        HttpClient httpClient,
        ISecureStorageService secureStorage,
        SpotifySettings settings)
    {
        _httpClient = httpClient;
        _secureStorage = secureStorage;
        _settings = settings;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        _ = LoadTokensAsync();
    }

    private async Task LoadTokensAsync()
    {
        var accessToken = await _secureStorage.GetAsync(CredentialKeys.SpotifyAccessToken);
        var refreshToken = await _secureStorage.GetAsync(CredentialKeys.SpotifyRefreshToken);
        var expiryStr = await _secureStorage.GetAsync(CredentialKeys.SpotifyTokenExpiry);

        if (!string.IsNullOrEmpty(refreshToken))
        {
            _tokens = new SpotifyTokens
            {
                AccessToken = accessToken ?? string.Empty,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.TryParse(expiryStr, out var expiry) ? expiry : DateTime.MinValue
            };

            AuthenticationStateChanged?.Invoke(this, true);
        }
    }

    public string GetAuthorizationUrl()
    {
        // Generate PKCE code verifier (random 128 bytes, base64url encoded)
        _codeVerifier = GenerateCodeVerifier();

        // Store for later use
        _ = _secureStorage.SetAsync(CredentialKeys.SpotifyCodeVerifier, _codeVerifier);

        // Generate code challenge (SHA256 hash of verifier, base64url encoded)
        var codeChallenge = GenerateCodeChallenge(_codeVerifier);

        var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var queryParams = new Dictionary<string, string>
        {
            ["client_id"] = _settings.ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = _settings.RedirectUri,
            ["code_challenge_method"] = "S256",
            ["code_challenge"] = codeChallenge,
            ["state"] = state,
            ["scope"] = _settings.Scopes
        };

        var queryString = string.Join("&", queryParams.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        return $"{AuthorizeUrl}?{queryString}";
    }

    public async Task<bool> HandleCallbackAsync(Uri callbackUri, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = HttpUtility.ParseQueryString(callbackUri.Query);
            var code = query["code"];
            var error = query["error"];

            if (!string.IsNullOrEmpty(error))
            {
                return false;
            }

            if (string.IsNullOrEmpty(code))
            {
                return false;
            }

            // Retrieve code verifier
            var codeVerifier = _codeVerifier ?? await _secureStorage.GetAsync(CredentialKeys.SpotifyCodeVerifier);
            if (string.IsNullOrEmpty(codeVerifier))
            {
                return false;
            }

            // Exchange code for tokens
            var tokenRequest = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = _settings.RedirectUri,
                ["client_id"] = _settings.ClientId,
                ["code_verifier"] = codeVerifier
            };

            var content = new FormUrlEncodedContent(tokenRequest);
            var response = await _httpClient.PostAsync(TokenUrl, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseBody, _jsonOptions);

            if (tokenResponse is null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                return false;
            }

            _tokens = new SpotifyTokens
            {
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = tokenResponse.RefreshToken ?? string.Empty,
                TokenType = tokenResponse.TokenType ?? "Bearer",
                ExpiresIn = tokenResponse.ExpiresIn,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                Scope = tokenResponse.Scope ?? string.Empty
            };

            await SaveTokensAsync();
            await _secureStorage.RemoveAsync(CredentialKeys.SpotifyCodeVerifier);

            AuthenticationStateChanged?.Invoke(this, true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_tokens is null)
        {
            return null;
        }

        if (_tokens.IsExpired && !string.IsNullOrEmpty(_tokens.RefreshToken))
        {
            await RefreshTokenAsync(cancellationToken);
        }

        return _tokens?.AccessToken;
    }

    private async Task RefreshTokenAsync(CancellationToken cancellationToken)
    {
        if (_tokens is null || string.IsNullOrEmpty(_tokens.RefreshToken))
        {
            return;
        }

        try
        {
            var tokenRequest = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = _tokens.RefreshToken,
                ["client_id"] = _settings.ClientId
            };

            var content = new FormUrlEncodedContent(tokenRequest);
            var response = await _httpClient.PostAsync(TokenUrl, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Refresh failed, user needs to re-authenticate
                await LogoutAsync();
                return;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseBody, _jsonOptions);

            if (tokenResponse is not null)
            {
                _tokens.AccessToken = tokenResponse.AccessToken;
                _tokens.ExpiresIn = tokenResponse.ExpiresIn;
                _tokens.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

                // Update refresh token if provided
                if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
                {
                    _tokens.RefreshToken = tokenResponse.RefreshToken;
                }

                await SaveTokensAsync();
            }
        }
        catch
        {
            // Silently fail, will retry on next access
        }
    }

    public async Task LogoutAsync()
    {
        _tokens = null;

        await _secureStorage.RemoveAsync(CredentialKeys.SpotifyAccessToken);
        await _secureStorage.RemoveAsync(CredentialKeys.SpotifyRefreshToken);
        await _secureStorage.RemoveAsync(CredentialKeys.SpotifyTokenExpiry);

        AuthenticationStateChanged?.Invoke(this, false);
    }

    private async Task SaveTokensAsync()
    {
        if (_tokens is null) return;

        await _secureStorage.SetAsync(CredentialKeys.SpotifyAccessToken, _tokens.AccessToken);
        await _secureStorage.SetAsync(CredentialKeys.SpotifyRefreshToken, _tokens.RefreshToken);
        await _secureStorage.SetAsync(CredentialKeys.SpotifyTokenExpiry, _tokens.ExpiresAt.ToString("O"));
    }

    private static string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Base64UrlEncode(bytes);
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private sealed class TokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string? RefreshToken { get; set; }
        public string? TokenType { get; set; }
        public int ExpiresIn { get; set; }
        public string? Scope { get; set; }
    }
}
