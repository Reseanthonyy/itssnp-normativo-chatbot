namespace ItssnpNormativo.Api.Services;

public interface IAzureSpeechService
{
    bool IsConfigured { get; }
    string Region { get; }
    Task<string?> TranscribeAudioAsync(Stream audioStream, string contentType, CancellationToken cancellationToken = default);
}

