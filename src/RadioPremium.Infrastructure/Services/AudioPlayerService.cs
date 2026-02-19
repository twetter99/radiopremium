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
    private BufferedWaveProvider? _bufferedProvider;
    private VolumeWaveProvider16? _volumeProvider;
    private Thread? _bufferThread;
    private volatile bool _stopBuffering;
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

                // Convert to 16-bit PCM for consistent processing
                var pcm16 = _mediaReader.ToSampleProvider().ToWaveProvider16();

                // Buffered provider absorbs network jitter to prevent underruns
                _bufferedProvider = new BufferedWaveProvider(pcm16.WaveFormat)
                {
                    BufferDuration = TimeSpan.FromSeconds(5),
                    DiscardOnBufferOverflow = true
                };

                // Start background thread to feed the buffer from the network stream
                _stopBuffering = false;
                _bufferThread = new Thread(() => FillBuffer(pcm16))
                {
                    IsBackground = true,
                    Name = "AudioBuffer",
                    Priority = ThreadPriority.AboveNormal
                };
                _bufferThread.Start();

                // Pre-buffer: wait until we have at least 1 second of audio
                var preBufferBytes = pcm16.WaveFormat.AverageBytesPerSecond;
                var preBufferDeadline = DateTime.UtcNow.AddSeconds(6);
                while (_bufferedProvider.BufferedBytes < preBufferBytes
                       && DateTime.UtcNow < preBufferDeadline
                       && !cancellationToken.IsCancellationRequested)
                {
                    Thread.Sleep(50);
                }

                // Wrap with volume control
                _volumeProvider = new VolumeWaveProvider16(_bufferedProvider)
                {
                    Volume = _isMuted ? 0f : _volume
                };

                // Create wave player with larger buffers for smooth playback
                _wavePlayer = new WaveOutEvent
                {
                    DesiredLatency = 500,
                    NumberOfBuffers = 3
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

    /// <summary>
    /// Background thread that reads from the network stream and fills the buffer.
    /// Decouples network I/O from audio rendering to avoid underruns.
    /// </summary>
    private void FillBuffer(IWaveProvider source)
    {
        var readBuffer = new byte[8192];
        try
        {
            while (!_stopBuffering)
            {
                var bytesRead = source.Read(readBuffer, 0, readBuffer.Length);
                if (bytesRead == 0)
                {
                    // Stream ended
                    Thread.Sleep(50);
                    continue;
                }
                _bufferedProvider?.AddSamples(readBuffer, 0, bytesRead);

                // Throttle if buffer is getting full to avoid wasting CPU
                if (_bufferedProvider is not null &&
                    _bufferedProvider.BufferedBytes > _bufferedProvider.WaveFormat.AverageBytesPerSecond * 4)
                {
                    Thread.Sleep(100);
                }
            }
        }
        catch
        {
            // Stream error – playback will stop naturally when buffer drains
        }
    }

    private void CleanupResources()
    {
        _stopBuffering = true;

        if (_wavePlayer is not null)
        {
            _wavePlayer.PlaybackStopped -= OnPlaybackStopped;
            _wavePlayer.Stop();
            _wavePlayer.Dispose();
            _wavePlayer = null;
        }

        _volumeProvider = null;

        // Wait for buffer thread to finish
        if (_bufferThread is not null)
        {
            _bufferThread.Join(timeout: TimeSpan.FromSeconds(2));
            _bufferThread = null;
        }

        _bufferedProvider = null;

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
                // Clamp to prevent integer overflow → distortion
                var scaled = Math.Clamp((int)(sample * Volume), short.MinValue, short.MaxValue);
                var bytes = BitConverter.GetBytes((short)scaled);
                buffer[offset + i] = bytes[0];
                buffer[offset + i + 1] = bytes[1];
            }
        }

        return bytesRead;
    }
}
