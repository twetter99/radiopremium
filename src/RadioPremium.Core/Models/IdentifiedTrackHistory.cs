namespace RadioPremium.Core.Models;

/// <summary>
/// Represents a track identification in the history with station context
/// </summary>
public sealed class IdentifiedTrackHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Track Track { get; set; } = new();
    public DateTime IdentifiedAt { get; set; } = DateTime.Now;

    // Station context
    public Guid? StationUuid { get; set; }
    public string? StationName { get; set; }
    public string? StationCountry { get; set; }
    public string? StationFavicon { get; set; }

    /// <summary>
    /// Formatted date for display
    /// </summary>
    public string FormattedDate => IdentifiedAt.ToString("dd/MM/yyyy HH:mm");

    /// <summary>
    /// Relative time (e.g., "Hace 5 minutos")
    /// </summary>
    public string RelativeTime
    {
        get
        {
            var diff = DateTime.Now - IdentifiedAt;

            if (diff.TotalMinutes < 1)
                return "Hace un momento";
            if (diff.TotalMinutes < 60)
                return $"Hace {(int)diff.TotalMinutes} min";
            if (diff.TotalHours < 24)
                return $"Hace {(int)diff.TotalHours}h";
            if (diff.TotalDays < 7)
                return $"Hace {(int)diff.TotalDays}d";

            return FormattedDate;
        }
    }

    /// <summary>
    /// Station info for display
    /// </summary>
    public string StationInfo => string.IsNullOrEmpty(StationName)
        ? "Desconocida"
        : $"{StationName}{(string.IsNullOrEmpty(StationCountry) ? "" : $" • {StationCountry}")}";
}
