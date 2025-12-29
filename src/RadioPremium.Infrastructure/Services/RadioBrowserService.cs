using RadioPremium.Core.Models;
using RadioPremium.Core.Services;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RadioPremium.Infrastructure.Services;

/// <summary>
/// Implementation of Radio Browser API service
/// </summary>
public sealed class RadioBrowserService : IRadioBrowserService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    // Use DNS round-robin endpoint that automatically routes to available servers
    private static readonly string _primaryServer = "https://all.api.radio-browser.info/json/";
    
    private static readonly string[] _backupServers = new[]
    {
        "https://de1.api.radio-browser.info/json/",
        "https://nl1.api.radio-browser.info/json/",
        "https://at1.api.radio-browser.info/json/"
    };

    public RadioBrowserService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(_primaryServer);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "RadioPremium/1.0");
        _httpClient.Timeout = TimeSpan.FromSeconds(15);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };
    }

    public async Task<IReadOnlyList<Station>> SearchByNameAsync(string name, int limit = 50, CancellationToken cancellationToken = default)
    {
        return await SearchAsync(name: name, limit: limit, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<Station>> SearchByCountryAsync(string country, int limit = 50, CancellationToken cancellationToken = default)
    {
        return await SearchAsync(country: country, limit: limit, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<Station>> SearchByTagAsync(string tag, int limit = 50, CancellationToken cancellationToken = default)
    {
        return await SearchAsync(tag: tag, limit: limit, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<Station>> SearchAsync(
        string? name = null,
        string? country = null,
        string? tag = null,
        string? language = null,
        string orderBy = "clickcount",
        bool reverse = true,
        int limit = 50,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string>
        {
            $"limit={limit}",
            $"offset={offset}",
            $"order={orderBy}",
            $"reverse={reverse.ToString().ToLower()}",
            "hidebroken=true"
        };

        if (!string.IsNullOrWhiteSpace(name))
            queryParams.Add($"name={Uri.EscapeDataString(name)}");

        if (!string.IsNullOrWhiteSpace(country))
            queryParams.Add($"country={Uri.EscapeDataString(country)}");

        if (!string.IsNullOrWhiteSpace(tag))
            queryParams.Add($"tag={Uri.EscapeDataString(tag)}");

        if (!string.IsNullOrWhiteSpace(language))
            queryParams.Add($"language={Uri.EscapeDataString(language)}");

        var url = $"stations/search?{string.Join("&", queryParams)}";
        var logPath = Path.Combine(AppContext.BaseDirectory, "api.log");

        try
        {
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] Request: {_httpClient.BaseAddress}{url}\n");
            var response = await _httpClient.GetAsync(url, cancellationToken);
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] Status: {response.StatusCode}\n");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] Response length: {content.Length}\n");
            
            var stations = JsonSerializer.Deserialize<List<Station>>(content, _jsonOptions);
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] Deserialized: {stations?.Count ?? 0} stations\n");
            if (stations?.Count > 0)
            {
                File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] First station: {stations[0].Name}, UUID: {stations[0].StationUuid}\n");
            }
            return stations ?? new List<Station>();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] Error: {ex.Message}\n{ex.StackTrace}\n");
            // Try backup server
            return await TryBackupServerAsync(url, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<Station>> GetTopStationsAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        var url = $"stations/topclick/{limit}";

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var stations = await response.Content.ReadFromJsonAsync<List<Station>>(_jsonOptions, cancellationToken);
            return stations ?? new List<Station>();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return await TryBackupServerAsync(url, cancellationToken);
        }
    }

    public async Task<Station?> GetStationByIdAsync(Guid stationUuid, CancellationToken cancellationToken = default)
    {
        var url = $"stations/byuuid/{stationUuid}";

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var stations = await response.Content.ReadFromJsonAsync<List<Station>>(_jsonOptions, cancellationToken);
            return stations?.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> ClickStationAsync(Guid stationUuid, CancellationToken cancellationToken = default)
    {
        var url = $"url/{stationUuid}";

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<CountryInfo>> GetCountriesAsync(CancellationToken cancellationToken = default)
    {
        var url = "countries";

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var countries = await response.Content.ReadFromJsonAsync<List<CountryInfoDto>>(_jsonOptions, cancellationToken);
            return countries?.Select(c => new CountryInfo
            {
                Name = c.Name,
                Iso3166_1 = c.Iso31661,
                StationCount = c.Stationcount
            }).ToList() ?? new List<CountryInfo>();
        }
        catch
        {
            return Array.Empty<CountryInfo>();
        }
    }

    public async Task<IReadOnlyList<TagInfo>> GetTagsAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        var url = $"tags?order=stationcount&reverse=true&limit={limit}";

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var tags = await response.Content.ReadFromJsonAsync<List<TagInfoDto>>(_jsonOptions, cancellationToken);
            return tags?.Select(t => new TagInfo
            {
                Name = t.Name,
                StationCount = t.Stationcount
            }).ToList() ?? new List<TagInfo>();
        }
        catch
        {
            return Array.Empty<TagInfo>();
        }
    }

    private async Task<IReadOnlyList<Station>> TryBackupServerAsync(string url, CancellationToken cancellationToken)
    {
        foreach (var server in _backupServers)
        {
            try
            {
                using var backupClient = new HttpClient 
                { 
                    BaseAddress = new Uri(server),
                    Timeout = TimeSpan.FromSeconds(10)
                };
                backupClient.DefaultRequestHeaders.Add("User-Agent", "RadioPremium/1.0");

                var response = await backupClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();

                var stations = await response.Content.ReadFromJsonAsync<List<Station>>(_jsonOptions, cancellationToken);
                return stations ?? new List<Station>();
            }
            catch
            {
                continue;
            }
        }

        return Array.Empty<Station>();
    }

    private sealed class CountryInfoDto
    {
        public string Name { get; set; } = string.Empty;
        public string Iso31661 { get; set; } = string.Empty;
        public int Stationcount { get; set; }
    }

    private sealed class TagInfoDto
    {
        public string Name { get; set; } = string.Empty;
        public int Stationcount { get; set; }
    }
}
