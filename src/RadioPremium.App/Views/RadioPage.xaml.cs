using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using RadioPremium.Core.Models;
using RadioPremium.Core.Services;
using RadioPremium.Core.ViewModels;
using Windows.UI;

namespace RadioPremium.App.Views;

/// <summary>
/// Radio browsing and search page - Apple-style design
/// </summary>
public sealed partial class RadioPage : Page
{
    public RadioViewModel ViewModel { get; }

    public RadioPage()
    {
        InitializeComponent();
        ViewModel = App.GetService<RadioViewModel>();
        DataContext = ViewModel;

        Loaded += RadioPage_Loaded;
    }

    private async void RadioPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
    }

    private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            ViewModel.SearchCommand.Execute(null);
        }
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SearchCommand.Execute(null);
    }

    private void CountryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CountryComboBox.SelectedItem is CountryInfo country)
        {
            ViewModel.SelectedCountry = country.Name;
            ViewModel.SearchCommand.Execute(null);
        }
    }

    private void TagComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TagComboBox.SelectedItem is TagInfo tag)
        {
            ViewModel.SelectedTag = tag.Name;
            ViewModel.SearchCommand.Execute(null);
        }
    }

    private void PopularButton_Click(object sender, RoutedEventArgs e)
    {
        CountryComboBox.SelectedItem = null;
        TagComboBox.SelectedItem = null;
        ViewModel.SelectedCountry = string.Empty;
        ViewModel.SelectedTag = string.Empty;
        ViewModel.SearchQuery = string.Empty;
        ViewModel.LoadTopStationsCommand.Execute(null);
    }

    private void NewButton_Click(object sender, RoutedEventArgs e)
    {
        // Could add sorting by new stations
        ViewModel.LoadTopStationsCommand.Execute(null);
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Station station)
        {
            ViewModel.PlayStationCommand.Execute(station);
        }
    }

    private void StationCard_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
        {
            border.Scale = new System.Numerics.Vector3(1.02f, 1.02f, 1f);
            border.Translation = new System.Numerics.Vector3(0, -2, 0);
        }
    }

    private void StationCard_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
        {
            border.Scale = new System.Numerics.Vector3(1f, 1f, 1f);
            border.Translation = new System.Numerics.Vector3(0, 0, 0);
        }
    }

    private void StationCard_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border && border.DataContext is Station station)
        {
            ViewModel.PlayStationCommand.Execute(station);
        }
    }
}
