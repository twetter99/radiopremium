using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using RadioPremium.Core.Messages;
using RadioPremium.Core.Models;
using RadioPremium.Core.Services;

namespace RadioPremium.Core.ViewModels;

/// <summary>
/// ViewModel for the application shell
/// </summary>
public partial class ShellViewModel : ObservableRecipient
{
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private PageType _currentPage = PageType.Radio;

    [ObservableProperty]
    private bool _isNavigationPaneOpen = true;

    public ShellViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
        IsActive = true;
    }

    [RelayCommand]
    private void NavigateTo(PageType pageType)
    {
        CurrentPage = pageType;
        _navigationService.NavigateTo(pageType);
    }

    [RelayCommand]
    private void ToggleNavigationPane()
    {
        IsNavigationPaneOpen = !IsNavigationPaneOpen;
    }

    protected override void OnActivated()
    {
        base.OnActivated();
        Messenger.Register<NavigateMessage>(this, (r, m) =>
        {
            CurrentPage = m.Value;
        });
    }
}
