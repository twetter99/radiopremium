using Microsoft.UI.Xaml.Controls;
using RadioPremium.Core.Models;
using RadioPremium.Core.Services;

namespace RadioPremium.App.Helpers;

/// <summary>
/// Navigation service implementation for WinUI 3
/// </summary>
public sealed class NavigationService : INavigationService
{
    private Frame? _frame;

    public PageType CurrentPage { get; private set; } = PageType.Radio;

    public bool CanGoBack => _frame?.CanGoBack ?? false;

    public event EventHandler<PageType>? Navigated;

    public void Initialize(Frame frame)
    {
        _frame = frame;
        _frame.Navigated += (s, e) =>
        {
            Navigated?.Invoke(this, CurrentPage);
        };
    }

    public void NavigateTo(PageType pageType, object? parameter = null)
    {
        if (_frame is null) return;

        var pageTypeName = pageType switch
        {
            PageType.Radio => typeof(Views.RadioPage),
            PageType.Favorites => typeof(Views.FavoritesPage),
            PageType.Settings => typeof(Views.SettingsPage),
            _ => typeof(Views.RadioPage)
        };

        CurrentPage = pageType;
        _frame.Navigate(pageTypeName, parameter);
    }

    public void GoBack()
    {
        if (_frame?.CanGoBack == true)
        {
            _frame.GoBack();
        }
    }
}
