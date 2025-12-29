using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RadioPremium.Core.ViewModels;

namespace RadioPremium.App.Views;

/// <summary>
/// Settings page
/// </summary>
public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        InitializeComponent();
        ViewModel = App.GetService<SettingsViewModel>();
        DataContext = ViewModel;

        Loaded += SettingsPage_Loaded;
    }

    private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
    }

    private void SpotifyDisconnect_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.DisconnectSpotifyCommand.Execute(null);
    }

    private void ResetSettings_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ResetSettingsCommand.Execute(null);
    }
}
