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
        WeakReferenceMessenger.Default.Register<OpenUrlMessage>(this, OnOpenUrl);

        // Watch IdentifyViewModel Spotify status changes for auto-save UI updates
        _identifyViewModel.PropertyChanged += OnIdentifyViewModelPropertyChanged;

        // Handle Spotify login URL opening in a Windows browser (Parallels compatible)
        _spotifyViewModel.LoginUrlGenerated += (s, url) =>
        {
            Helpers.WindowsBrowserLauncher.OpenUrl(url);
        };

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

    private void IdentifiedNav_Click(object sender, RoutedEventArgs e)
    {
        SelectNavButton(IdentifiedNavButton);
        ContentFrame.Navigate(typeof(IdentifiedTracksPage));
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

    private void OnOpenUrl(object recipient, OpenUrlMessage message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            Helpers.WindowsBrowserLauncher.OpenUrl(message.Value);
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

        // Reset Spotify auto-save UI
        SpotifyAutoSavePanel.Visibility = Visibility.Collapsed;
        SpotifySaveProgressRing.Visibility = Visibility.Collapsed;
        SpotifyReconnectButton.Visibility = Visibility.Collapsed;
        SpotifyReconnectButton.IsEnabled = true;
        AddToSpotifyButton.Visibility = Visibility.Visible;

        // If Spotify is authenticated, show saving status (auto-save will start)
        var authService = App.GetService<ISpotifyAuthService>();
        if (authService.IsAuthenticated)
        {
            AddToSpotifyButton.Visibility = Visibility.Collapsed;
            SpotifyAutoSavePanel.Visibility = Visibility.Visible;
            SpotifySaveProgressRing.Visibility = Visibility.Visible;
            SpotifySaveStatusText.Text = "Guardando en Spotify...";
            SpotifySaveStatusText.Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"];
        }

        TrackIdentifiedOverlay.Visibility = Visibility.Visible;
    }

    private void OnIdentifyViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(IdentifyViewModel.IsSavingToSpotify):
                    SpotifySaveProgressRing.Visibility = _identifyViewModel.IsSavingToSpotify
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                    break;

                case nameof(IdentifyViewModel.SavedToSpotify):
                    if (_identifyViewModel.SavedToSpotify)
                    {
                        SpotifySaveStatusText.Foreground = (Brush)Application.Current.Resources["SpotifyGreenBrush"];
                        AddToSpotifyButton.Visibility = Visibility.Collapsed;
                    }
                    break;

                case nameof(IdentifyViewModel.SpotifyStatusMessage):
                    if (_identifyViewModel.SpotifyStatusMessage is not null)
                    {
                        SpotifyAutoSavePanel.Visibility = Visibility.Visible;
                        SpotifySaveStatusText.Text = _identifyViewModel.SpotifyStatusMessage;

                        var isScopeError = _identifyViewModel.SpotifyStatusMessage.Contains("Permisos insuficientes");
                        SpotifyReconnectButton.Visibility = isScopeError ? Visibility.Visible : Visibility.Collapsed;

                        // If not saved, show fallback button and use warning color
                        if (!_identifyViewModel.SavedToSpotify && !_identifyViewModel.IsSavingToSpotify)
                        {
                            SpotifySaveStatusText.Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"];
                            AddToSpotifyButton.Visibility = isScopeError ? Visibility.Collapsed : Visibility.Visible;
                        }
                    }
                    break;

                case nameof(IdentifyViewModel.SpotifyArtworkUrl):
                    // Update artwork in dialog if we got a better one from Spotify
                    if (!string.IsNullOrEmpty(_identifyViewModel.SpotifyArtworkUrl))
                    {
                        TrackArtworkSmall.Source = new BitmapImage(new Uri(_identifyViewModel.SpotifyArtworkUrl));
                        TrackIconPlaceholder.Visibility = Visibility.Collapsed;
                    }
                    break;
            }
        });
    }

    private void CloseTrackCard_Click(object sender, RoutedEventArgs e)
    {
        TrackIdentifiedOverlay.Visibility = Visibility.Collapsed;
    }

    private async void SpotifyReconnect_Click(object sender, RoutedEventArgs e)
    {
        SpotifyReconnectButton.IsEnabled = false;
        SpotifySaveStatusText.Text = "Reconectando...";
        SpotifyReconnectButton.Visibility = Visibility.Collapsed;

        // Logout then trigger fresh login with updated scopes
        var authService = App.GetService<ISpotifyAuthService>();
        await authService.LogoutAsync();
        _spotifyViewModel.LoginCommand.Execute(null);
    }

    private void CloseOverlay_Click(object sender, RoutedEventArgs e)
    {
        TrackIdentifiedOverlay.Visibility = Visibility.Collapsed;
    }

    private void AddToSpotify_Click(object sender, RoutedEventArgs e)
    {
        if (_currentTrack is not null)
        {
            var searchQuery = Uri.EscapeDataString($"{_currentTrack.Title} {_currentTrack.Artist}");
            var spotifySearchUrl = $"https://open.spotify.com/search/{searchQuery}";
            Helpers.WindowsBrowserLauncher.OpenUrl(spotifySearchUrl);
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

    public void AddToSpotify()
    {
        if (_currentTrack is not null && TrackIdentifiedCard.Visibility == Visibility.Visible)
        {
            var searchQuery = Uri.EscapeDataString($"{_currentTrack.Title} {_currentTrack.Artist}");
            var spotifySearchUrl = $"https://open.spotify.com/search/{searchQuery}";
            Helpers.WindowsBrowserLauncher.OpenUrl(spotifySearchUrl);
        }
    }

    public void NavigateToSettings()
    {
        // Navigate to settings
        ContentFrame.Navigate(typeof(SettingsPage));
    }
}
