using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using RadioPremium.Core.Messages;
using RadioPremium.Core.Models;
using RadioPremium.Core.ViewModels;

namespace RadioPremium.App.Controls;

/// <summary>
/// Premium Player bar control with Spotify-like design
/// </summary>
public sealed partial class PlayerBar : UserControl
{
    public PlayerViewModel ViewModel { get; }

    public PlayerBar()
    {
        InitializeComponent();
        ViewModel = App.GetService<PlayerViewModel>();
        DataContext = ViewModel;

        // Update UI when properties change
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
        else if (e.PropertyName == nameof(ViewModel.PlaybackState))
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdatePlayingState();
            });
        }
    }

    private void UpdatePlayingState()
    {
        var isPlaying = ViewModel.PlaybackState == PlaybackState.Playing;
        
        // Show/hide glow effect
        if (isPlaying)
        {
            ArtworkGlow.Opacity = 0.3;
        }
        else
        {
            ArtworkGlow.Opacity = 0;
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
            VolumeIcon.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.Colors.Gray);
        }
        else if (ViewModel.Volume < 0.33)
        {
            VolumeIcon.Glyph = "\uE993"; // Low - one wave
            VolumeIcon.Foreground = (Microsoft.UI.Xaml.Media.Brush)
                Application.Current.Resources["TextSecondaryBrush"];
        }
        else if (ViewModel.Volume < 0.66)
        {
            VolumeIcon.Glyph = "\uE994"; // Medium - two waves
            VolumeIcon.Foreground = (Microsoft.UI.Xaml.Media.Brush)
                Application.Current.Resources["TextPrimaryBrush"];
        }
        else
        {
            VolumeIcon.Glyph = "\uE767"; // High - three waves
            // Tinte rojo sutil para volumen alto
            VolumeIcon.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 220, 60, 60));
        }
    }

    // ========== BUTTON HANDLERS ==========

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.TogglePlayPauseCommand.Execute(null);
    }

    private void PlayButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button button)
        {
            button.Scale = new System.Numerics.Vector3(1.08f, 1.08f, 1f);
        }
    }

    private void PlayButton_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button button)
        {
            button.Scale = new System.Numerics.Vector3(1f, 1f, 1f);
        }
    }

    private void ActionButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button button)
        {
            button.Scale = new System.Numerics.Vector3(1.05f, 1.05f, 1f);
            button.Opacity = 0.9;
        }
    }

    private void ActionButton_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button button)
        {
            button.Scale = new System.Numerics.Vector3(1f, 1f, 1f);
            button.Opacity = 1.0;
        }
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

    private void Previous_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.PlayPreviousCommand.Execute(null);
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.PlayNextCommand.Execute(null);
    }

    private void Shuffle_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleShuffleCommand.Execute(null);
    }

    private void Repeat_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleRepeatCommand.Execute(null);
    }

    private void MiniPlayer_Click(object sender, RoutedEventArgs e)
    {
        App.SwitchToMiniPlayer();
    }
}
