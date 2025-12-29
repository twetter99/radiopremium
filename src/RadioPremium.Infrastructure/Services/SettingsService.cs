using RadioPremium.Core.Models;
using RadioPremium.Core.Services;
using System.Text.Json;

namespace RadioPremium.Infrastructure.Services;

/// <summary>
/// Service for managing application settings with JSON persistence
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private AppSettings _settings = new();

    public AppSettings Settings => _settings;

    public event EventHandler<AppSettings>? SettingsChanged;

    public SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appData, "RadioPremium");
        Directory.CreateDirectory(appFolder);
        _filePath = Path.Combine(appFolder, "settings.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(_filePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
                _settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
            }
            catch
            {
                _settings = new AppSettings();
            }
        }
        else
        {
            _settings = new AppSettings();
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(_settings, _jsonOptions);
        await File.WriteAllTextAsync(_filePath, json, cancellationToken);
        SettingsChanged?.Invoke(this, _settings);
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        _settings = new AppSettings();
        await SaveAsync(cancellationToken);
    }
}
