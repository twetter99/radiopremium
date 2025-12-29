using RadioPremium.Core.Models;

namespace RadioPremium.Core.Services;

/// <summary>
/// Service for managing application settings
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Get current settings
    /// </summary>
    AppSettings Settings { get; }

    /// <summary>
    /// Load settings from storage
    /// </summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Save settings to storage
    /// </summary>
    Task SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reset settings to defaults
    /// </summary>
    Task ResetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when settings change
    /// </summary>
    event EventHandler<AppSettings>? SettingsChanged;
}
