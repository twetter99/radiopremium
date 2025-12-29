using RadioPremium.Core.Models;
using RadioPremium.Core.Services;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RadioPremium.Infrastructure.Services;

/// <summary>
/// ACRCloud music recognition service implementation
/// </summary>
public sealed class AcrCloudRecognitionService : IAcrCloudRecognitionService
{
    private readonly HttpClient _httpClient;
    private readonly AcrCloudSettings _settings;
    private readonly JsonSerializerOptions _jsonOptions;

    public AcrCloudRecognitionService(HttpClient httpClient, AcrCloudSettings settings)
    {
        _httpClient = httpClient;
        _settings = settings;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
    }

    public bool IsConfigured =>
        !string.IsNullOrEmpty(_settings.Host) &&
        !string.IsNullOrEmpty(_settings.AccessKey) &&
        !string.IsNullOrEmpty(_settings.AccessSecret);

    public async Task<IdentificationResult> IdentifyAsync(AudioCaptureResult captureResult, CancellationToken cancellationToken = default)
    {
        var logPath = Path.Combine(AppContext.BaseDirectory, "acrcloud.log");
        
        if (!IsConfigured)
        {
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] Error: ACRCloud no configurado\n");
            return IdentificationResult.FromError(-1, "ACRCloud no está configurado");
        }

        if (!captureResult.Success || captureResult.PcmData is null || captureResult.PcmData.Length == 0)
        {
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] Error: No audio data. Success={captureResult.Success}, DataLen={captureResult.PcmData?.Length ?? 0}\n");
            return IdentificationResult.FromError(-2, captureResult.ErrorMessage ?? "No hay datos de audio");
        }

        try
        {
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] Starting identification. Audio bytes: {captureResult.PcmData.Length}, SampleRate: {captureResult.SampleRate}\n");
            
            var timestamp = ((int)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds).ToString();
            var stringToSign = BuildStringToSign(timestamp);
            var signature = CreateSignature(stringToSign, _settings.AccessSecret);

            var url = $"https://{_settings.Host}/v1/identify";
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] URL: {url}\n");
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] AccessKey: {_settings.AccessKey}\n");
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] StringToSign: {stringToSign.Replace("\n", "\\n")}\n");
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] Signature: {signature}\n");

            using var content = new MultipartFormDataContent();

            // Add required fields - signature_version must be "1" not "fingerprint"
            content.Add(new StringContent(_settings.AccessKey), "access_key");
            content.Add(new StringContent("audio"), "data_type");
            content.Add(new StringContent("1"), "signature_version");
            content.Add(new StringContent(signature), "signature");
            content.Add(new StringContent(timestamp), "timestamp");
            content.Add(new StringContent(captureResult.SampleRate.ToString()), "sample_rate");

            // Convert PCM to WAV format (ACRCloud needs WAV with header)
            var wavData = ConvertPcmToWav(captureResult.PcmData, captureResult.SampleRate, captureResult.Channels, captureResult.BitsPerSample);
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] WAV data size: {wavData.Length} bytes\n");

            // Add audio data as WAV
            var sampleBytes = new ByteArrayContent(wavData);
            sampleBytes.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            content.Add(sampleBytes, "sample", "sample.wav");
            content.Add(new StringContent(wavData.Length.ToString()), "sample_bytes");

            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] HTTP Status: {response.StatusCode}\n");
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] Response: {responseBody}\n");

            if (!response.IsSuccessStatusCode)
            {
                return IdentificationResult.FromError((int)response.StatusCode, $"HTTP Error: {response.StatusCode}");
            }

            var acrResponse = JsonSerializer.Deserialize<AcrCloudResponse>(responseBody, _jsonOptions);

            if (acrResponse is null)
            {
                return IdentificationResult.FromError(-3, "Respuesta inválida de ACRCloud");
            }

            // Check status code
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] ACR Status: Code={acrResponse.Status.Code}, Msg={acrResponse.Status.Msg}\n");
            
            if (acrResponse.Status.Code != 0)
            {
                if (acrResponse.Status.Code == 1001)
                {
                    return IdentificationResult.NoMatch();
                }

                return IdentificationResult.FromError(acrResponse.Status.Code, acrResponse.Status.Msg);
            }

            // Parse music data
            var music = acrResponse.Metadata?.Music?.FirstOrDefault();
            if (music is null)
            {
                File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] No music in metadata\n");
                return IdentificationResult.NoMatch();
            }

            var track = MapToTrack(music);
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] Track found: {track.Title} - {track.Artist}\n");
            return IdentificationResult.FromSuccess(track);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] Exception: {ex.Message}\n{ex.StackTrace}\n");
            return IdentificationResult.FromError(-99, $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Build the string to sign for HMAC-SHA1
    /// </summary>
    private string BuildStringToSign(string timestamp)
    {
        return $"POST\n/v1/identify\n{_settings.AccessKey}\naudio\n1\n{timestamp}";
    }

    /// <summary>
    /// Create HMAC-SHA1 signature
    /// </summary>
    private static string CreateSignature(string stringToSign, string secretKey)
    {
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(secretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign));
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Convert raw PCM data to WAV format with proper header
    /// </summary>
    private static byte[] ConvertPcmToWav(byte[] pcmData, int sampleRate, int channels, int bitsPerSample)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        var byteRate = sampleRate * channels * (bitsPerSample / 8);
        var blockAlign = (short)(channels * (bitsPerSample / 8));

        // RIFF header
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + pcmData.Length); // File size - 8
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));

        // fmt subchunk
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16); // Subchunk size (16 for PCM)
        writer.Write((short)1); // Audio format (1 = PCM)
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write((short)bitsPerSample);

        // data subchunk
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(pcmData.Length);
        writer.Write(pcmData);

        return ms.ToArray();
    }

    /// <summary>
    /// Map ACRCloud music response to Track model
    /// </summary>
    private static Track MapToTrack(AcrCloudMusic music)
    {
        var track = new Track
        {
            Title = music.Title,
            Artist = music.PrimaryArtist,
            Album = music.AlbumName,
            Isrc = music.Isrc,
            ReleaseDate = music.ReleaseDate,
            Label = music.Label,
            DurationMs = music.DurationMs > 0 ? music.DurationMs : null,
            Score = music.Score,
            Genre = music.Genres?.FirstOrDefault()?.Name,
            RecognizedAt = DateTime.UtcNow
        };

        // Extract external IDs and artwork
        if (music.ExternalMetadata is not null)
        {
            // Spotify
            var spotify = music.ExternalMetadata.Spotify;
            if (spotify?.Track is not null)
            {
                track.SpotifyId = spotify.Track.Id;
            }

            // Deezer (for artwork)
            var deezer = music.ExternalMetadata.Deezer;
            if (deezer is not null)
            {
                track.DeezerTrackId = deezer.Id;
                track.ArtworkUrl = deezer.Album?.CoverBig;
            }

            // YouTube
            if (music.ExternalMetadata.Youtube?.Vid is not null)
            {
                track.YoutubeVideoId = music.ExternalMetadata.Youtube.Vid;
            }
        }

        // Fallback artwork from Spotify if available
        if (string.IsNullOrEmpty(track.ArtworkUrl) && !string.IsNullOrEmpty(track.SpotifyId))
        {
            // Will be fetched from Spotify API when needed
        }

        return track;
    }
}
