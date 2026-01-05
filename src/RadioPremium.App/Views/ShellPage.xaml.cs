using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using RadioPremium.Core.Messages;
using RadioPremium.Core.Models;
using RadioPremium.Core.Services;
using RadioPremium.Core.ViewModels;

namespace RadioPremium.App.Views;

/// <summary>
/// Shell page with Apple-style navigation and player bar
/// </summary>
public sealed partial class ShellPage : Page
{
    private readonly ShellViewModel _viewModel;
    private readonly PlayerViewModel _playerViewModel;
    private readonly IdentifyViewModel _identifyViewModel;
    private readonly SpotifyViewModel _spotifyViewModel;
    private Track? _currentTrack;
    private Button? _selectedNavButton;

    /// <summary>
    /// Gets the content frame for navigation
    /// </summary>
    public Frame NavigationFrame => ContentFrame;

    public ShellPage()
    {
        InitializeComponent();

        _viewModel = App.GetService<ShellViewModel>();
        _playerViewModel = App.GetService<PlayerViewModel>();
        _identifyViewModel = App.GetService<IdentifyViewModel>();
        _spotifyViewModel = App.GetService<SpotifyViewModel>();

        DataContext = _viewModel;

        // Register for messages
        WeakReferenceMessenger.Default.Register<TrackIdentifiedMessage>(this, OnTrackIdentified);
        WeakReferenceMessenger.Default.Register<IdentificationStateChangedMessage>(this, OnIdentificationStateChanged);
        WeakReferenceMessenger.Default.Register<ShowNotificationMessage>(this, OnShowNotification);
        WeakReferenceMessenger.Default.Register<SpotifyAddedMessage>(this, OnSpotifyAdded);

        // Navigate to Radio page by default
        ContentFrame.Navigate(typeof(RadioPage));
        _selectedNavButton = RadioNavButton;

        // Update Spotify status
        UpdateSpotifyStatus();
        App.GetService<ISpotifyAuthService>().AuthenticationStateChanged += (s, e) =>
        {
            DispatcherQueue.TryEnqueue(UpdateSpotifyStatus);
        };
    }

    private void SelectNavButton(Button button)
    {
        // Reset previous button (but preserve RadioNavButton's special red style)
        if (_selectedNavButton != null && _selectedNavButton != RadioNavButton)
        {
            _selectedNavButton.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            if (_selectedNavButton.Content is StackPanel prevPanel && prevPanel.Children.Count > 1 && prevPanel.Children[1] is TextBlock prevText)
            {
                prevText.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
            }
            else if (_selectedNavButton.Content is Grid prevGrid)
            {
                // Handle buttons with Grid content (like FavoritesNavButton)
                if (prevGrid.Children[0] is StackPanel innerPanel && innerPanel.Children.Count > 1 && innerPanel.Children[1] is TextBlock innerText)
                {
                    innerText.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
                }
            }
        }

        // Highlight new button (RadioNavButton keeps its red style)
        if (button == RadioNavButton)
        {
            button.Background = (Brush)Application.Current.Resources["AppleRedBrush"];
        }
        else
        {
            button.Background = (Brush)Application.Current.Resources["BackgroundSecondaryBrush"];
            if (button.Content is StackPanel panel && panel.Children.Count > 1 && panel.Children[1] is TextBlock text)
            {
                text.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            }
            else if (button.Content is Grid grid)
            {
                // Handle buttons with Grid content (like FavoritesNavButton)
                if (grid.Children[0] is StackPanel innerPanel && innerPanel.Children.Count > 1 && innerPanel.Children[1] is TextBlock innerText)
                {
                    innerText.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
                }
            }
        }

        _selectedNavButton = button;
    }

    private void RadioNav_Click(object sender, RoutedEventArgs e)
    {
        SelectNavButton(RadioNavButton);
        ContentFrame.Navigate(typeof(RadioPage));
    }

    private void FavoritesNav_Click(object sender, RoutedEventArgs e)
    {
        SelectNavButton(FavoritesNavButton);
        ContentFrame.Navigate(typeof(FavoritesPage));
    }

    private void SettingsNav_Click(object sender, RoutedEventArgs e)
    {
        SelectNavButton(SettingsNavButton);
        ContentFrame.Navigate(typeof(SettingsPage));
    }

    private void GenresNav_Click(object sender, RoutedEventArgs e)
    {
        SelectNavButton(GenresNavButton);
        ContentFrame.Navigate(typeof(RadioPage), new RadioNavigationParameter(RadioTab.Explore, "genre"));
    }

    private void CountriesNav_Click(object sender, RoutedEventArgs e)
    {
        SelectNavButton(CountriesNavButton);
        ContentFrame.Navigate(typeof(RadioPage), new RadioNavigationParameter(RadioTab.ByCountry));
    }

    private void TrendingNav_Click(object sender, RoutedEventArgs e)
    {
        SelectNavButton(TrendingNavButton);
        ContentFrame.Navigate(typeof(RadioPage), new RadioNavigationParameter(RadioTab.ForYou, "trending"));
    }

    private void OnTrackIdentified(object recipient, TrackIdentifiedMessage message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _currentTrack = message.Value;
            ShowTrackOverlay(message.Value);
        });
    }

    private void OnIdentificationStateChanged(object recipient, IdentificationStateChangedMessage message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var state = message.Value;
            var progress = message.Progress;

            switch (state)
            {
                case CaptureState.Capturing:
                    IdentifyProgressOverlay.Visibility = Visibility.Visible;
                    IdentifyStatusText.Text = "Escuchando...";
                    IdentifyProgressBar.Value = progress * 100;
                    break;

                case CaptureState.Processing:
                    IdentifyStatusText.Text = "Identificando...";
                    IdentifyProgressBar.Value = 90;
                    break;

                case CaptureState.Idle:
                case CaptureState.Error:
                    IdentifyProgressOverlay.Visibility = Visibility.Collapsed;
                    break;
            }
        });
    }

    private void OnShowNotification(object recipient, ShowNotificationMessage message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            NotificationBar.Title = message.Title;
            NotificationBar.Message = message.Message;
            NotificationBar.Severity = message.Type switch
            {
                NotificationType.Success => InfoBarSeverity.Success,
                NotificationType.Warning => InfoBarSeverity.Warning,
                NotificationType.Error => InfoBarSeverity.Error,
                _ => InfoBarSeverity.Informational
            };
            NotificationBar.IsOpen = true;

            // Auto-close after 3 seconds
            var timer = DispatcherQueue.CreateTimer();
            timer.Interval = TimeSpan.FromSeconds(3);
            timer.Tick += (s, e) =>
            {
                NotificationBar.IsOpen = false;
                timer.Stop();
            };
            timer.Start();
        });
    }

    private void OnSpotifyAdded(object recipient, SpotifyAddedMessage message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (message.Success)
            {
                // Solo mostrar notificación, no cerrar el modal
                NotificationBar.Title = "Añadido a Spotify";
                NotificationBar.Message = "La canción se ha guardado en tu biblioteca";
                NotificationBar.Severity = InfoBarSeverity.Success;
                NotificationBar.IsOpen = true;
            }
        });
    }

    private void ShowTrackOverlay(Track track)
    {
        TrackTitle.Text = track.Title ?? "Título desconocido";
        TrackArtist.Text = track.Artist ?? "Artista desconocido";
        TrackAlbum.Text = !string.IsNullOrEmpty(track.Album) ? track.Album : "";

        if (!string.IsNullOrEmpty(track.ArtworkUrl))
        {
            TrackArtworkSmall.Source = new BitmapImage(new Uri(track.ArtworkUrl));
            TrackIconPlaceholder.Visibility = Visibility.Collapsed;
        }
        else
        {
            TrackArtworkSmall.Source = null;
            TrackIconPlaceholder.Visibility = Visibility.Visible;
        }

        TrackIdentifiedOverlay.Visibility = Visibility.Visible;
    }

    private void CloseTrackCard_Click(object sender, RoutedEventArgs e)
    {
        TrackIdentifiedOverlay.Visibility = Visibility.Collapsed;
    }

    private void CloseOverlay_Click(object sender, RoutedEventArgs e)
    {
        TrackIdentifiedOverlay.Visibility = Visibility.Collapsed;
    }

    private async void AddToSpotify_Click(object sender, RoutedEventArgs e)
    {
        if (_currentTrack is not null)
        {
            // Abrir Spotify con la búsqueda de la canción exacta
            var searchQuery = Uri.EscapeDataString($"{_currentTrack.Title} {_currentTrack.Artist}");
            var spotifySearchUrl = $"https://open.spotify.com/search/{searchQuery}";
            
            try
            {
                // Intentar abrir la app de Spotify primero, si no, abre el navegador
                await Windows.System.Launcher.LaunchUriAsync(new Uri(spotifySearchUrl));
            }
            catch
            {
                // Fallback: abrir en navegador
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = spotifySearchUrl,
                    UseShellExecute = true
                });
            }
        }
    }

    private void CancelIdentify_Click(object sender, RoutedEventArgs e)
    {
        _identifyViewModel.CancelIdentifyCommand.Execute(null);
    }

    private void SpotifyButton_Click(object sender, RoutedEventArgs e)
    {
        var authService = App.GetService<ISpotifyAuthService>();
        if (authService.IsAuthenticated)
        {
            // Show logout option
            _ = authService.LogoutAsync();
        }
        else
        {
            _ = _spotifyViewModel.LoginCommand.ExecuteAsync(null);
        }
    }

    private void UpdateSpotifyStatus()
    {
        var authService = App.GetService<ISpotifyAuthService>();
        SpotifyStatusText.Text = authService.IsAuthenticated ? "Spotify conectado" : "Conectar Spotify";
    }

    // Public methods for keyboard shortcuts
    public void TogglePlayPause()
    {
        _playerViewModel.TogglePlayPauseCommand.Execute(null);
    }

    public void StartIdentification()
    {
        if (_identifyViewModel.CanIdentify)
        {
            _ = _identifyViewModel.IdentifyCommand.ExecuteAsync(null);
        }
    }

    public async void AddToSpotify()
    {
        if (_currentTrack is not null && TrackIdentifiedCard.Visibility == Visibility.Visible)
        {
            // Abrir Spotify con la búsqueda de la canción exacta
            var searchQuery = Uri.EscapeDataString($"{_currentTrack.Title} {_currentTrack.Artist}");
            var spotifySearchUrl = $"https://open.spotify.com/search/{searchQuery}";
            
            try
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri(spotifySearchUrl));
            }
            catch
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = spotifySearchUrl,
                    UseShellExecute = true
                });
            }
        }
    }

    public void NavigateToSettings()
    {
        // Navigate to settings
        ContentFrame.Navigate(typeof(SettingsPage));
    }
}
