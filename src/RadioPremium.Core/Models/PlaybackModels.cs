namespace RadioPremium.Core.Models;

/// <summary>
/// Playback state enumeration
/// </summary>
public enum PlaybackState
{
    Stopped,
    Loading,
    Playing,
    Paused,
    Error
}

/// <summary>
/// Current playback information
/// </summary>
public sealed class PlaybackInfo
{
    public Station? CurrentStation { get; set; }
    public PlaybackState State { get; set; }
    public TimeSpan Duration { get; set; }
    public TimeSpan Position { get; set; }
    public float Volume { get; set; } = 1.0f;
    public bool IsMuted { get; set; }
    public string? ErrorMessage { get; set; }

    public bool IsPlaying => State == PlaybackState.Playing;
    public bool CanPlay => State != PlaybackState.Loading;
}

/// <summary>
/// Audio capture state
/// </summary>
public enum CaptureState
{
    Idle,
    Capturing,
    Processing,
    Error
}

/// <summary>
/// Result of audio capture for identification
/// </summary>
public sealed class AudioCaptureResult
{
    public bool Success { get; set; }
    public byte[]? PcmData { get; set; }
    public int SampleRate { get; set; }
    public int Channels { get; set; }
    public int BitsPerSample { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }

    public static AudioCaptureResult FromSuccess(byte[] data, int sampleRate, int channels, int bitsPerSample, TimeSpan duration) => new()
    {
        Success = true,
        PcmData = data,
        SampleRate = sampleRate,
        Channels = channels,
        BitsPerSample = bitsPerSample,
        Duration = duration
    };

    public static AudioCaptureResult FromError(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };
}
