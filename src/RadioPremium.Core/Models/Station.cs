using System.Text.Json.Serialization;

namespace RadioPremium.Core.Models;

/// <summary>
/// Represents a radio station from Radio Browser API
/// </summary>
public sealed class Station
{
    [JsonPropertyName("stationuuid")]
    public Guid StationUuid { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
    
    [JsonPropertyName("url_resolved")]
    public string UrlResolved { get; set; } = string.Empty;
    
    [JsonPropertyName("homepage")]
    public string Homepage { get; set; } = string.Empty;
    
    [JsonPropertyName("favicon")]
    public string Favicon { get; set; } = string.Empty;
    
    [JsonPropertyName("tags")]
    public string Tags { get; set; } = string.Empty;
    
    [JsonPropertyName("country")]
    public string Country { get; set; } = string.Empty;
    
    [JsonPropertyName("countrycode")]
    public string CountryCode { get; set; } = string.Empty;
    
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;
    
    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;
    
    [JsonPropertyName("languagecodes")]
    public string LanguageCodes { get; set; } = string.Empty;
    
    [JsonPropertyName("votes")]
    public int Votes { get; set; }
    
    [JsonPropertyName("lastchangetime")]
    public string? LastChangeTime { get; set; }
    
    [JsonPropertyName("codec")]
    public string Codec { get; set; } = string.Empty;
    
    [JsonPropertyName("bitrate")]
    public int Bitrate { get; set; }
    
    [JsonPropertyName("hls")]
    public int Hls { get; set; }
    
    [JsonPropertyName("lastcheckok")]
    public int LastCheckOk { get; set; }
    
    [JsonPropertyName("lastchecktime")]
    public string? LastCheckTime { get; set; }
    
    [JsonPropertyName("lastcheckoktime")]
    public string? LastCheckOkTime { get; set; }
    
    [JsonPropertyName("lastlocalchecktime")]
    public string? LastLocalCheckTime { get; set; }
    
    [JsonPropertyName("clicktimestamp")]
    public string? ClickTimestamp { get; set; }
    
    [JsonPropertyName("clickcount")]
    public int ClickCount { get; set; }
    
    [JsonPropertyName("clicktrend")]
    public int ClickTrend { get; set; }
    
    [JsonPropertyName("ssl_error")]
    public int SslError { get; set; }
    
    [JsonPropertyName("geo_lat")]
    public double? GeoLat { get; set; }
    
    [JsonPropertyName("geo_long")]
    public double? GeoLong { get; set; }
    
    [JsonPropertyName("has_extended_info")]
    public bool HasExtendedInfo { get; set; }

    /// <summary>
    /// Indicates whether this station is a favorite (local state)
    /// </summary>
    [JsonIgnore]
    public bool IsFavorite { get; set; }

    /// <summary>
    /// Gets the best available stream URL
    /// </summary>
    public string StreamUrl => !string.IsNullOrEmpty(UrlResolved) ? UrlResolved : Url;

    /// <summary>
    /// Gets tags as a list
    /// </summary>
    public IReadOnlyList<string> TagList => 
        string.IsNullOrEmpty(Tags) 
            ? Array.Empty<string>() 
            : Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Gets whether the station is currently online
    /// </summary>
    public bool IsOnline => LastCheckOk == 1;

    public override string ToString() => $"{Name} ({Country})";

    public override bool Equals(object? obj) => obj is Station other && StationUuid == other.StationUuid;

    public override int GetHashCode() => StationUuid.GetHashCode();
}
