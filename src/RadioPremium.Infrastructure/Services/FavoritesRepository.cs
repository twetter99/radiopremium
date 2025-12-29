using RadioPremium.Core.Models;
using RadioPremium.Core.Services;
using System.Text.Json;

namespace RadioPremium.Infrastructure.Services;

/// <summary>
/// Repository for managing favorite stations with JSON persistence
/// </summary>
public sealed class FavoritesRepository : IFavoritesRepository
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<Station>? _cache;

    public event EventHandler<FavoritesChangedEventArgs>? FavoritesChanged;

    public FavoritesRepository()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appData, "RadioPremium");
        Directory.CreateDirectory(appFolder);
        _filePath = Path.Combine(appFolder, "favorites.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<IReadOnlyList<Station>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureCacheLoadedAsync(cancellationToken);
            return _cache!.ToList().AsReadOnly();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AddAsync(Station station, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureCacheLoadedAsync(cancellationToken);

            if (_cache!.Any(s => s.StationUuid == station.StationUuid))
            {
                return; // Already exists
            }

            station.IsFavorite = true;
            _cache.Add(station);
            await SaveCacheAsync(cancellationToken);

            FavoritesChanged?.Invoke(this, new FavoritesChangedEventArgs(station.StationUuid, true));
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RemoveAsync(Guid stationUuid, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureCacheLoadedAsync(cancellationToken);

            var station = _cache!.FirstOrDefault(s => s.StationUuid == stationUuid);
            if (station is not null)
            {
                _cache.Remove(station);
                await SaveCacheAsync(cancellationToken);

                FavoritesChanged?.Invoke(this, new FavoritesChangedEventArgs(stationUuid, false));
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> IsFavoriteAsync(Guid stationUuid, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureCacheLoadedAsync(cancellationToken);
            return _cache!.Any(s => s.StationUuid == stationUuid);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> ToggleAsync(Station station, CancellationToken cancellationToken = default)
    {
        var isFavorite = await IsFavoriteAsync(station.StationUuid, cancellationToken);

        if (isFavorite)
        {
            await RemoveAsync(station.StationUuid, cancellationToken);
            return false;
        }
        else
        {
            await AddAsync(station, cancellationToken);
            return true;
        }
    }

    public async Task<IReadOnlySet<Guid>> GetFavoriteIdsAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureCacheLoadedAsync(cancellationToken);
            return _cache!.Select(s => s.StationUuid).ToHashSet();
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
                _cache = JsonSerializer.Deserialize<List<Station>>(json, _jsonOptions) ?? new List<Station>();
            }
            catch
            {
                // Corrupted file, create backup and start fresh
                var backupPath = _filePath + ".bak";
                if (File.Exists(_filePath))
                {
                    File.Copy(_filePath, backupPath, overwrite: true);
                }
                _cache = new List<Station>();
            }
        }
        else
        {
            _cache = new List<Station>();
        }
    }

    private async Task SaveCacheAsync(CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(_cache, _jsonOptions);
        await File.WriteAllTextAsync(_filePath, json, cancellationToken);
    }
}
