using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RadioPremium.Core.Models;
using RadioPremium.Core.ViewModels;

namespace RadioPremium.App.Views;

/// <summary>
/// Favorites page
/// </summary>
public sealed partial class FavoritesPage : Page
{
    public FavoritesViewModel ViewModel { get; }

    public FavoritesPage()
    {
        InitializeComponent();
        ViewModel = App.GetService<FavoritesViewModel>();
        DataContext = ViewModel;

        Loaded += FavoritesPage_Loaded;
    }

    private async void FavoritesPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
    }

    private void PlayStation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Station station)
        {
            ViewModel.PlayStationCommand.Execute(station);
        }
    }

    private void RemoveFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Station station)
        {
            ViewModel.RemoveFavoriteCommand.Execute(station);
        }
    }
}
