using RadioPremium.Core.Models;
using RadioPremium.Core.Services;
using System.Text.Json;

namespace RadioPremium.Infrastructure.Services;

/// <summary>
/// Repository for managing identified tracks history with JSON persistence
/// </summary>
public sealed class IdentifiedTracksRepository : IIdentifiedTracksRepository
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<IdentifiedTrackHistory>? _cache;

    public event EventHandler? HistoryChanged;

    public IdentifiedTracksRepository()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appData, "RadioPremium");
        Directory.CreateDirectory(appFolder);
        _filePath = Path.Combine(appFolder, "identified_tracks.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<IReadOnlyList<IdentifiedTrackHistory>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureCacheLoadedAsync(cancellationToken);
            return _cache!.OrderByDescending(t => t.IdentifiedAt).ToList().AsReadOnly();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AddAsync(IdentifiedTrackHistory track, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureCacheLoadedAsync(cancellationToken);
            _cache!.Add(track);
            await SaveCacheAsync(cancellationToken);

            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RemoveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureCacheLoadedAsync(cancellationToken);

            var track = _cache!.FirstOrDefault(t => t.Id == id);
            if (track is not null)
            {
                _cache.Remove(track);
                await SaveCacheAsync(cancellationToken);

                HistoryChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureCacheLoadedAsync(cancellationToken);
            _cache!.Clear();
            await SaveCacheAsync(cancellationToken);

            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<IdentifiedTrackHistory>> GetByStationAsync(Guid stationUuid, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureCacheLoadedAsync(cancellationToken);
            return _cache!
                .Where(t => t.StationUuid == stationUuid)
                .OrderByDescending(t => t.IdentifiedAt)
                .ToList()
                .AsReadOnly();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<IdentifiedTrackHistory>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureCacheLoadedAsync(cancellationToken);

            var lowerQuery = query.ToLowerInvariant();
            return _cache!
                .Where(t =>
                    t.Track.Title.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
                    t.Track.Artist.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
                    t.Track.Album.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
                    (t.StationName?.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ?? false))
                .OrderByDescending(t => t.IdentifiedAt)
                .ToList()
                .AsReadOnly();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task EnsureCacheLoadedAsync(CancellationToken cancellationToken)
    {
        if (_cache is not null) return;

        if (File.Exists(_filePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
                _cache = JsonSerializer.Deserialize<List<IdentifiedTrackHistory>>(json, _jsonOptions) ?? new List<IdentifiedTrackHistory>();
            }
            catch
            {
                // Corrupted file, create backup and start fresh
                var backupPath = _filePath + ".bak";
                if (File.Exists(_filePath))
                {
                    File.Copy(_filePath, backupPath, overwrite: true);
                }
                _cache = new List<IdentifiedTrackHistory>();
            }
        }
        else
        {
            _cache = new List<IdentifiedTrackHistory>();
        }
    }

    private async Task SaveCacheAsync(CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(_cache, _jsonOptions);
        await File.WriteAllTextAsync(_filePath, json, cancellationToken);
    }
}
