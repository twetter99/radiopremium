using RadioPremium.Core.Models;

namespace RadioPremium.Core.Services;

/// <summary>
/// Service for navigation between pages
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Current page type
    /// </summary>
    PageType CurrentPage { get; }

    /// <summary>
    /// Navigate to a page
    /// </summary>
    void NavigateTo(PageType pageType, object? parameter = null);

    /// <summary>
    /// Navigate back
    /// </summary>
    void GoBack();

    /// <summary>
    /// Whether can navigate back
    /// </summary>
    bool CanGoBack { get; }

    /// <summary>
    /// Event raised when navigation occurs
    /// </summary>
    event EventHandler<PageType>? Navigated;
}
