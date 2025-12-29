using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Imaging;
using RadioPremium.Core.Messages;
using RadioPremium.Core.Models;
using RadioPremium.Core.ViewModels;

namespace RadioPremium.App.Controls;

/// <summary>
/// Player bar control for playback controls
/// </summary>
public sealed partial class PlayerBar : UserControl
{
    public PlayerViewModel ViewModel { get; }

    public PlayerBar()
    {
        InitializeComponent();
        ViewModel = App.GetService<PlayerViewModel>();
        DataContext = ViewModel;

        // Update UI when station changes
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.CurrentStation))
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateStationLogo();
            });
        }
        else if (e.PropertyName == nameof(ViewModel.IsMuted) || e.PropertyName == nameof(ViewModel.Volume))
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateVolumeIcon();
            });
        }
    }

    private void UpdateStationLogo()
    {
        if (ViewModel.CurrentStation?.Favicon is not null && 
            !string.IsNullOrEmpty(ViewModel.CurrentStation.Favicon))
        {
            try
            {
                StationLogo.Source = new BitmapImage(new Uri(ViewModel.CurrentStation.Favicon));
            }
            catch
            {
                StationLogo.Source = null;
            }
        }
        else
        {
            StationLogo.Source = null;
        }
    }

    private void UpdateVolumeIcon()
    {
        if (ViewModel.IsMuted || ViewModel.Volume == 0)
        {
            VolumeIcon.Glyph = "\uE74F"; // Muted
        }
        else if (ViewModel.Volume < 0.3)
        {
            VolumeIcon.Glyph = "\uE993"; // Low
        }
        else if (ViewModel.Volume < 0.7)
        {
            VolumeIcon.Glyph = "\uE994"; // Medium
        }
        else
        {
            VolumeIcon.Glyph = "\uE767"; // High
        }
    }

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.TogglePlayPauseCommand.Execute(null);
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.StopCommand.Execute(null);
    }

    private void Favorite_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleFavoriteCommand.Execute(null);
    }

    private void Identify_Click(object sender, RoutedEventArgs e)
    {
        System.IO.File.AppendAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "identify.log"), 
            $"[{DateTime.Now:HH:mm:ss}] Identify_Click - sending message\n");
        WeakReferenceMessenger.Default.Send(RequestIdentifyMessage.Instance);
        System.IO.File.AppendAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "identify.log"), 
            $"[{DateTime.Now:HH:mm:ss}] Message sent\n");
    }

    private void Mute_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.IsMuted = !ViewModel.IsMuted;
        UpdateVolumeIcon();
    }

    private void VolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        UpdateVolumeIcon();
    }
}
