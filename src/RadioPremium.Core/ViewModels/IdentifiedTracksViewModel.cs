using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using RadioPremium.Core.Messages;
using RadioPremium.Core.Models;
using RadioPremium.Core.Services;

namespace RadioPremium.Core.ViewModels;

/// <summary>
/// ViewModel for identified tracks history page
/// </summary>
public partial class IdentifiedTracksViewModel : ObservableRecipient
{
    private readonly IIdentifiedTracksRepository _repository;

    [ObservableProperty]
    private ObservableCollection<IdentifiedTrackHistory> _tracks = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _filterOption = "Todas";

    public bool HasTracks => Tracks.Count > 0;
    public bool IsEmpty => !HasTracks && !IsLoading;

    public IdentifiedTracksViewModel(IIdentifiedTracksRepository repository)
    {
        _repository = repository;
        _repository.HistoryChanged += OnHistoryChanged;

        IsActive = true;
    }

    protected override async void OnActivated()
    {
        base.OnActivated();
        await LoadTracksAsync();
    }

    [RelayCommand]
    private async Task LoadTracksAsync()
    {
        IsLoading = true;
        try
        {
            var tracks = await _repository.GetAllAsync();
            Tracks.Clear();
            foreach (var track in tracks)
            {
                Tracks.Add(track);
            }

            OnPropertyChanged(nameof(HasTracks));
            OnPropertyChanged(nameof(IsEmpty));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            await LoadTracksAsync();
            return;
        }

        IsLoading = true;
        try
        {
            var tracks = await _repository.SearchAsync(SearchQuery);
            Tracks.Clear();
            foreach (var track in tracks)
            {
                Tracks.Add(track);
            }

            OnPropertyChanged(nameof(HasTracks));
            OnPropertyChanged(nameof(IsEmpty));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RemoveTrackAsync(IdentifiedTrackHistory track)
    {
        await _repository.RemoveAsync(track.Id);
        Tracks.Remove(track);

        OnPropertyChanged(nameof(HasTracks));
        OnPropertyChanged(nameof(IsEmpty));

        Messenger.Send(new ShowNotificationMessage(
            "Eliminado",
            "Canción eliminada del historial",
            NotificationType.Info));
    }

    [RelayCommand]
    private async Task ClearAllAsync()
    {
        await _repository.ClearAllAsync();
        Tracks.Clear();

        OnPropertyChanged(nameof(HasTracks));
        OnPropertyChanged(nameof(IsEmpty));

        Messenger.Send(new ShowNotificationMessage(
            "Historial limpio",
            "Se eliminaron todas las canciones identificadas",
            NotificationType.Info));
    }

    [RelayCommand]
    private void OpenInSpotify(IdentifiedTrackHistory track)
    {
        Messenger.Send(new AddToSpotifyMessage(track.Track));
    }

    [RelayCommand]
    private void CopyToClipboard(IdentifiedTrackHistory track)
    {
        var text = $"{track.Track.Artist} - {track.Track.Title}";

        // Copy to clipboard (Windows.ApplicationModel.DataTransfer)
        var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
        dataPackage.SetText(text);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);

        Messenger.Send(new ShowNotificationMessage(
            "Copiado",
            "Copiado al portapapeles",
            NotificationType.Success));
    }

    partial void OnSearchQueryChanged(string value)
    {
        _ = SearchAsync();
    }

    private void OnHistoryChanged(object? sender, EventArgs e)
    {
        _ = LoadTracksAsync();
    }
}
