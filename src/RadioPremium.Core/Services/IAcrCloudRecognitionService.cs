using RadioPremium.Core.Models;

namespace RadioPremium.Core.Services;

/// <summary>
/// Service for identifying songs using ACRCloud
/// </summary>
public interface IAcrCloudRecognitionService
{
    /// <summary>
    /// Identify a track from PCM audio data
    /// </summary>
    /// <param name="captureResult">Captured audio data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Identification result</returns>
    Task<IdentificationResult> IdentifyAsync(AudioCaptureResult captureResult, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the service is configured correctly
    /// </summary>
    bool IsConfigured { get; }
}
