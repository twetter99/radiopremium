using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RadioPremium.Core.Models;
using RadioPremium.Core.ViewModels;

namespace RadioPremium.App.Views;

public sealed partial class IdentifiedTracksPage : Page
{
    public IdentifiedTracksViewModel ViewModel { get; }

    public IdentifiedTracksPage()
    {
        InitializeComponent();
        ViewModel = App.GetService<IdentifiedTracksViewModel>();
        DataContext = ViewModel;
    }

    private async void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Limpiar historial",
            Content = "¿Estás seguro de que quieres eliminar todas las canciones identificadas? Esta acción no se puede deshacer.",
            PrimaryButtonText = "Eliminar todo",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.ClearAllCommand.ExecuteAsync(null);
        }
    }

    private void RemoveTrack_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is IdentifiedTrackHistory track)
        {
            ViewModel.RemoveTrackCommand.Execute(track);
        }
    }

    private void OpenInSpotify_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is IdentifiedTrackHistory track)
        {
            ViewModel.OpenInSpotifyCommand.Execute(track);
        }
    }

    private void CopyToClipboard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is IdentifiedTrackHistory track)
        {
            ViewModel.CopyToClipboardCommand.Execute(track);
        }
    }
}
