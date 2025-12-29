using NAudio.Wave;
using RadioPremium.Core.Models;
using RadioPremium.Core.Services;
using CorePlaybackState = RadioPremium.Core.Models.PlaybackState;
using NAudioPlaybackState = NAudio.Wave.PlaybackState;

namespace RadioPremium.Infrastructure.Services;

/// <summary>
/// Audio player service using NAudio with MediaFoundation for HTTP streams
/// </summary>
public sealed class AudioPlayerService : IAudioPlayerService
{
    private IWavePlayer? _wavePlayer;
    private MediaFoundationReader? _mediaReader;
    private VolumeWaveProvider16? _volumeProvider;
    private Station? _currentStation;
    private CorePlaybackState _state = CorePlaybackState.Stopped;
    private float _volume = 0.8f;
    private bool _isMuted;
    private bool _disposed;

    public CorePlaybackState State
    {
        get => _state;
        private set
        {
            if (_state != value)
            {
                _state = value;
                StateChanged?.Invoke(this, value);
            }
        }
    }

    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            if (_volumeProvider is not null && !_isMuted)
            {
                _volumeProvider.Volume = _volume;
            }
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            _isMuted = value;
            if (_volumeProvider is not null)
            {
                _volumeProvider.Volume = value ? 0f : _volume;
            }
        }
    }

    public Station? CurrentStation => _currentStation;

    public event EventHandler<CorePlaybackState>? StateChanged;
    public event EventHandler<string>? ErrorOccurred;

    public async Task PlayAsync(Station station, CancellationToken cancellationToken = default)
    {
        Stop();

        _currentStation = station;
        State = CorePlaybackState.Loading;

        try
        {
            await Task.Run(() =>
            {
                // Create media reader for streaming
                _mediaReader = new MediaFoundationReader(station.StreamUrl);

                // Wrap with volume control
                _volumeProvider = new VolumeWaveProvider16(_mediaReader.ToSampleProvider().ToWaveProvider16())
                {
                    Volume = _isMuted ? 0f : _volume
                };

                // Create wave player
                _wavePlayer = new WaveOutEvent
                {
                    DesiredLatency = 300
                };

                _wavePlayer.PlaybackStopped += OnPlaybackStopped;
                _wavePlayer.Init(_volumeProvider);
                _wavePlayer.Play();

            }, cancellationToken);

            State = CorePlaybackState.Playing;
        }
        catch (OperationCanceledException)
        {
            State = CorePlaybackState.Stopped;
            throw;
        }
        catch (Exception ex)
        {
            State = CorePlaybackState.Error;
            ErrorOccurred?.Invoke(this, $"Error al reproducir: {ex.Message}");
            CleanupResources();
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
        {
            State = CorePlaybackState.Error;
            ErrorOccurred?.Invoke(this, $"Stream interrumpido: {e.Exception.Message}");
        }
        else if (State == CorePlaybackState.Playing)
        {
            // Stream ended unexpectedly, try to reconnect
            State = CorePlaybackState.Stopped;
        }
    }

    public void Pause()
    {
        if (_wavePlayer?.PlaybackState == NAudio.Wave.PlaybackState.Playing)
        {
            _wavePlayer.Pause();
            State = CorePlaybackState.Paused;
        }
    }

    public void Resume()
    {
        if (_wavePlayer?.PlaybackState == NAudio.Wave.PlaybackState.Paused)
        {
            _wavePlayer.Play();
            State = CorePlaybackState.Playing;
        }
    }

    public void Stop()
    {
        State = CorePlaybackState.Stopped;
        _currentStation = null;
        CleanupResources();
    }

    public async Task TogglePlayPauseAsync()
    {
        switch (State)
        {
            case CorePlaybackState.Playing:
                Pause();
                break;
            case CorePlaybackState.Paused:
                Resume();
                break;
            case CorePlaybackState.Stopped when _currentStation is not null:
                await PlayAsync(_currentStation);
                break;
        }
    }

    private void CleanupResources()
    {
        if (_wavePlayer is not null)
        {
            _wavePlayer.PlaybackStopped -= OnPlaybackStopped;
            _wavePlayer.Stop();
            _wavePlayer.Dispose();
            _wavePlayer = null;
        }

        _volumeProvider = null;

        if (_mediaReader is not null)
        {
            _mediaReader.Dispose();
            _mediaReader = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        CleanupResources();
    }
}

/// <summary>
/// Simple volume provider wrapper
/// </summary>
internal sealed class VolumeWaveProvider16 : IWaveProvider
{
    private readonly IWaveProvider _source;

    public float Volume { get; set; } = 1.0f;

    public WaveFormat WaveFormat => _source.WaveFormat;

    public VolumeWaveProvider16(IWaveProvider source)
    {
        _source = source;
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        var bytesRead = _source.Read(buffer, offset, count);

        if (Math.Abs(Volume - 1.0f) > 0.001f)
        {
            for (int i = 0; i < bytesRead; i += 2)
            {
                var sample = BitConverter.ToInt16(buffer, offset + i);
                sample = (short)(sample * Volume);
                var bytes = BitConverter.GetBytes(sample);
                buffer[offset + i] = bytes[0];
                buffer[offset + i + 1] = bytes[1];
            }
        }

        return bytesRead;
    }
}
