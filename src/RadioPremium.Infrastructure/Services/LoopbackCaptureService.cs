using NAudio.CoreAudioApi;
using NAudio.Wave;
using RadioPremium.Core.Models;
using RadioPremium.Core.Services;

using CoreCaptureState = RadioPremium.Core.Models.CaptureState;

namespace RadioPremium.Infrastructure.Services;

/// <summary>
/// Service for capturing system audio using WASAPI Loopback
/// </summary>
public sealed class LoopbackCaptureService : ILoopbackCaptureService
{
    private WasapiLoopbackCapture? _capture;
    private MemoryStream? _buffer;
    private CoreCaptureState _state = CoreCaptureState.Idle;
    private bool _disposed;

    public CoreCaptureState State
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

    public bool IsAvailable
    {
        get
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                return device is not null;
            }
            catch
            {
                return false;
            }
        }
    }

    public event EventHandler<CoreCaptureState>? StateChanged;
    public event EventHandler<double>? ProgressChanged;

    public async Task<AudioCaptureResult> CaptureAsync(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        var logPath = Path.Combine(AppContext.BaseDirectory, "capture.log");
        File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] CaptureAsync started. Duration: {duration}\n");
        
        if (!IsAvailable)
        {
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] No audio device available\n");
            return AudioCaptureResult.FromError("No hay dispositivo de audio disponible");
        }

        State = CoreCaptureState.Capturing;
        _buffer = new MemoryStream();

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] Device: {device.FriendlyName}\n");

            _capture = new WasapiLoopbackCapture(device);

            var captureFormat = _capture.WaveFormat;
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] Format: {captureFormat.SampleRate}Hz, {captureFormat.Channels}ch, {captureFormat.BitsPerSample}bit, {captureFormat.Encoding}\n");
            
            var startTime = DateTime.UtcNow;
            var targetBytes = (int)(duration.TotalSeconds * captureFormat.AverageBytesPerSecond);
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] Target bytes: {targetBytes}\n");

            var tcs = new TaskCompletionSource<bool>();

            _capture.DataAvailable += (s, e) =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _capture?.StopRecording();
                    return;
                }

                _buffer.Write(e.Buffer, 0, e.BytesRecorded);

                var progress = (double)_buffer.Length / targetBytes;
                ProgressChanged?.Invoke(this, Math.Min(progress, 1.0));

                if (_buffer.Length >= targetBytes)
                {
                    _capture?.StopRecording();
                }
            };

            _capture.RecordingStopped += (s, e) =>
            {
                tcs.TrySetResult(true);
            };

            _capture.StartRecording();

            // Wait for capture to complete or cancel
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(duration + TimeSpan.FromSeconds(2)); // Add 2s timeout

            try
            {
                await tcs.Task.WaitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                _capture.StopRecording();
                await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
            }

            _capture.Dispose();
            _capture = null;

            if (_buffer.Length == 0)
            {
                File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] No audio captured\n");
                return AudioCaptureResult.FromError("No se capturó audio. Asegúrate de que hay sonido reproduciéndose.");
            }

            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] Captured {_buffer.Length} bytes, converting...\n");
            
            // Convert to target format for ACRCloud (8000 Hz, Mono, 16-bit)
            var pcmData = ConvertToTargetFormat(_buffer.ToArray(), captureFormat);
            
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] Converted to {pcmData.Length} bytes\n");

            State = CoreCaptureState.Idle;

            return AudioCaptureResult.FromSuccess(
                pcmData,
                sampleRate: 8000,
                channels: 1,
                bitsPerSample: 16,
                duration: duration);
        }
        catch (OperationCanceledException)
        {
            State = CoreCaptureState.Idle;
            throw;
        }
        catch (Exception ex)
        {
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] Exception: {ex.Message}\n{ex.StackTrace}\n");
            State = CoreCaptureState.Error;
            return AudioCaptureResult.FromError($"Error al capturar audio: {ex.Message}");
        }
        finally
        {
            _buffer?.Dispose();
            _buffer = null;
        }
    }

    /// <summary>
    /// Convert captured audio to ACRCloud format (8000 Hz, Mono, 16-bit PCM)
    /// </summary>
    private byte[] ConvertToTargetFormat(byte[] sourceData, WaveFormat sourceFormat)
    {
        using var sourceStream = new RawSourceWaveStream(sourceData, 0, sourceData.Length, sourceFormat);

        // First convert to PCM if needed
        WaveStream pcmStream = sourceStream;

        if (sourceFormat.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            pcmStream = new Wave32To16Stream(sourceStream);
        }

        // Resample to 8000 Hz Mono
        var targetFormat = new WaveFormat(8000, 16, 1);

        using var resampler = new MediaFoundationResampler(pcmStream, targetFormat)
        {
            ResamplerQuality = 60
        };

        using var outputStream = new MemoryStream();
        var buffer = new byte[4096];
        int bytesRead;

        while ((bytesRead = resampler.Read(buffer, 0, buffer.Length)) > 0)
        {
            outputStream.Write(buffer, 0, bytesRead);
        }

        return outputStream.ToArray();
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _capture?.StopRecording();
        _capture?.Dispose();
        _buffer?.Dispose();
    }
}
