using RadioPremium.Core.Models;

namespace RadioPremium.Core.Services;

/// <summary>
/// Service for interacting with Radio Browser API
/// </summary>
public interface IRadioBrowserService
{
    /// <summary>
    /// Search stations by name
    /// </summary>
    Task<IReadOnlyList<Station>> SearchByNameAsync(string name, int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search stations by country
    /// </summary>
    Task<IReadOnlyList<Station>> SearchByCountryAsync(string country, int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search stations by tag/genre
    /// </summary>
    Task<IReadOnlyList<Station>> SearchByTagAsync(string tag, int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Advanced search with multiple criteria
    /// </summary>
    Task<IReadOnlyList<Station>> SearchAsync(
        string? name = null,
        string? country = null,
        string? tag = null,
        string? language = null,
        string orderBy = "clickcount",
        bool reverse = true,
        int limit = 50,
        int offset = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get top stations by click count
    /// </summary>
    Task<IReadOnlyList<Station>> GetTopStationsAsync(int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get station by UUID
    /// </summary>
    Task<Station?> GetStationByIdAsync(Guid stationUuid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Register a click on a station (for popularity tracking)
    /// </summary>
    Task<bool> ClickStationAsync(Guid stationUuid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get available countries with station count
    /// </summary>
    Task<IReadOnlyList<CountryInfo>> GetCountriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get available tags with station count
    /// </summary>
    Task<IReadOnlyList<TagInfo>> GetTagsAsync(int limit = 100, CancellationToken cancellationToken = default);
}

/// <summary>
/// Country information from Radio Browser
/// </summary>
public sealed class CountryInfo
{
    public string Name { get; set; } = string.Empty;
    public string Iso3166_1 { get; set; } = string.Empty;
    public int StationCount { get; set; }
}

/// <summary>
/// Tag information from Radio Browser
/// </summary>
public sealed class TagInfo
{
    public string Name { get; set; } = string.Empty;
    public int StationCount { get; set; }
}
