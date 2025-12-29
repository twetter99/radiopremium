using RadioPremium.Core.Models;

namespace RadioPremium.Core.Services;

/// <summary>
/// Service for capturing system audio (loopback)
/// </summary>
public interface ILoopbackCaptureService : IDisposable
{
    /// <summary>
    /// Current capture state
    /// </summary>
    CaptureState State { get; }

    /// <summary>
    /// Event raised when capture state changes
    /// </summary>
    event EventHandler<CaptureState>? StateChanged;

    /// <summary>
    /// Event raised when capture progress updates
    /// </summary>
    event EventHandler<double>? ProgressChanged;

    /// <summary>
    /// Capture audio for specified duration
    /// </summary>
    /// <param name="duration">Duration to capture</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Captured audio data</returns>
    Task<AudioCaptureResult> CaptureAsync(TimeSpan duration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if loopback capture is available
    /// </summary>
    bool IsAvailable { get; }
}
