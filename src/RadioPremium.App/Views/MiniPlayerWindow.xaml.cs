using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RadioPremium.Core.Messages;
using RadioPremium.Core.Services;
using RadioPremium.Core.ViewModels;
using System.Runtime.InteropServices;
using WinRT.Interop;

namespace RadioPremium.App.Views;

/// <summary>
/// Mini player window - compact always-on-top player
/// </summary>
public sealed partial class MiniPlayerWindow : Window
{
    private readonly AppWindow _appWindow;
    private readonly PlayerViewModel _playerViewModel;
    private readonly IdentifyViewModel _identifyViewModel;
    private readonly IFavoritesRepository _favoritesRepository;

    // Win32 interop for window styling
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    public event EventHandler? ExpandRequested;

    public MiniPlayerWindow()
    {
        InitializeComponent();

        _playerViewModel = App.GetService<PlayerViewModel>();
        _identifyViewModel = App.GetService<IdentifyViewModel>();
        _favoritesRepository = App.GetService<IFavoritesRepository>();
        
        // Ensure IdentifyViewModel is activated to receive messages
        _identifyViewModel.IsActive = true;

        // Get AppWindow for customization
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        // Configure mini window
        _appWindow.Title = "Radio Premium - Mini";

        // Set rounded corners
        int cornerPreference = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));

        // Set caption color to match background
        int captionColor = 0x001E1E1E; // BGR format
        DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));

        // Extend content into title bar for seamless look
        ExtendsContentIntoTitleBar = true;
        // Use specific drag region instead of entire window
        SetTitleBar(DragRegion);

        // Configure window presenter
        var presenter = _appWindow.Presenter as OverlappedPresenter;
        if (presenter is not null)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
        }

        // Set compact size
        _appWindow.Resize(new Windows.Graphics.SizeInt32(640, 400));

        // Position in bottom-right corner
        PositionInCorner();

        // Subscribe to ViewModel changes
        _playerViewModel.PropertyChanged += PlayerViewModel_PropertyChanged;

        // Initialize UI with current state
        UpdateUI();

        // Register keyboard shortcuts
        RegisterKeyboardShortcuts();
        
        // Set initial volume
        VolumeSlider.ValueChanged += VolumeSlider_ValueChanged;
        VolumeSlider.Value = _playerViewModel.Volume * 100;
        
        // Register for track identification results
        WeakReferenceMessenger.Default.Register<TrackIdentifiedMessage>(this, OnTrackIdentified);
    }

    private void OnTrackIdentified(object recipient, TrackIdentifiedMessage message)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            var track = message.Value;
            await ShowTrackIdentifiedDialogAsync(track);
        });
    }

    private async Task ShowTrackIdentifiedDialogAsync(RadioPremium.Core.Models.Track track)
    {
        // Create album art image if available
        var contentPanel = new StackPanel
        {
            Spacing = 12,
            Width = 280
        };

        // Add album art if available
        if (!string.IsNullOrEmpty(track.ArtworkUrl))
        {
            var albumArtBorder = new Border
            {
                Width = 120,
                Height = 120,
                CornerRadius = new CornerRadius(8),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new Microsoft.UI.Xaml.Controls.Image
                {
                    Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(track.ArtworkUrl)),
                    Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill
                }
            };
            contentPanel.Children.Add(albumArtBorder);
        }

        // Track info
        var infoPanel = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
        
        infoPanel.Children.Add(new TextBlock 
        { 
            Text = track.Title, 
            FontSize = 16, 
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            MaxWidth = 260
        });
        
        infoPanel.Children.Add(new TextBlock 
        { 
            Text = track.Artist, 
            FontSize = 14, 
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 215, 96)),
            TextAlignment = TextAlignment.Center
        });
        
        if (!string.IsNullOrEmpty(track.Album))
        {
            infoPanel.Children.Add(new TextBlock 
            { 
                Text = track.Album, 
                FontSize = 12, 
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 150, 150, 150)),
                TextAlignment = TextAlignment.Center
            });
        }

        contentPanel.Children.Add(infoPanel);

        var dialog = new ContentDialog
        {
            Title = "🎵 ¡Canción encontrada!",
            Content = contentPanel,
            PrimaryButtonText = "🎧 Abrir en Spotify",
            CloseButtonText = "Cerrar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        
        if (result == ContentDialogResult.Primary)
        {
            // Open Spotify search in a Windows browser (Parallels compatible)
            var searchQuery = Uri.EscapeDataString($"{track.Title} {track.Artist}");
            var spotifyUrl = $"https://open.spotify.com/search/{searchQuery}";
            Helpers.WindowsBrowserLauncher.OpenUrl(spotifyUrl);
        }
    }

    private void PositionInCorner()
    {
        var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
        var x = displayArea.WorkArea.Width - _appWindow.Size.Width - 20;
        var y = displayArea.WorkArea.Height - _appWindow.Size.Height - 20;
        _appWindow.Move(new Windows.Graphics.PointInt32(x, y));
    }

    private void PlayerViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() => UpdateUI());
    }

    private void UpdateUI()
    {
        // Update play/pause icon
        PlayPauseIcon.Glyph = _playerViewModel.IsPlaying ? "\uE769" : "\uE768"; // Pause : Play

        // Update station info
        if (_playerViewModel.CurrentStation is not null)
        {
            StationName.Text = _playerViewModel.CurrentStation.Name;
            StationCountry.Text = _playerViewModel.CurrentStation.Country;

            // Update favorite icon
            FavoriteIcon.Glyph = _playerViewModel.IsCurrentStationFavorite ? "\uE00B" : "\uE006";
            FavoriteIcon.Foreground = _playerViewModel.IsCurrentStationFavorite
                ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 215, 96))
                : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128));
        }
        else
        {
            StationName.Text = "Selecciona una emisora";
            StationCountry.Text = "";
        }
    }

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        _playerViewModel.TogglePlayPauseCommand.Execute(null);
    }

    private async void Identify_Click(object sender, RoutedEventArgs e)
    {
        // Call the singleton IdentifyViewModel directly
        if (_identifyViewModel.CanIdentify)
        {
            await _identifyViewModel.IdentifyCommand.ExecuteAsync(null);
        }
        else
        {
            await ShowMessageAsync("No disponible", 
                $"Estado: {_identifyViewModel.State}, Audio disponible: {_identifyViewModel.CanIdentify}");
        }
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.Content.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async void Favorite_Click(object sender, RoutedEventArgs e)
    {
        if (_playerViewModel.CurrentStation is null) return;

        var isFavorite = await _favoritesRepository.ToggleAsync(_playerViewModel.CurrentStation);
        _playerViewModel.IsCurrentStationFavorite = isFavorite;
        UpdateUI();
    }

    private void VolumeSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        _playerViewModel.Volume = (float)(e.NewValue / 100.0);
    }

    private void Expand_Click(object sender, RoutedEventArgs e)
    {
        ExpandRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RegisterKeyboardShortcuts()
    {
        // Space - Play/Pause
        var playPauseAccelerator = new Microsoft.UI.Xaml.Input.KeyboardAccelerator
        {
            Key = Windows.System.VirtualKey.Space,
            Modifiers = Windows.System.VirtualKeyModifiers.None
        };
        playPauseAccelerator.Invoked += (s, e) =>
        {
            _playerViewModel.TogglePlayPauseCommand.Execute(null);
            e.Handled = true;
        };

        // Ctrl+I - Identify
        var identifyAccelerator = new Microsoft.UI.Xaml.Input.KeyboardAccelerator
        {
            Key = Windows.System.VirtualKey.I,
            Modifiers = Windows.System.VirtualKeyModifiers.Control
        };
        identifyAccelerator.Invoked += (s, e) =>
        {
            Identify_Click(this, new RoutedEventArgs());
            e.Handled = true;
        };

        Content.KeyboardAccelerators.Add(playPauseAccelerator);
        Content.KeyboardAccelerators.Add(identifyAccelerator);
    }
}
