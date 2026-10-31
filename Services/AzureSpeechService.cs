using System.Net.Http.Headers;
using System.Text.Json;

namespace ItssnpNormativo.Api.Services;

public class AzureSpeechService : IAzureSpeechService
{
    private readonly ILogger<AzureSpeechService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string? _subscriptionKey;
    private readonly string _region;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_subscriptionKey);
    public string Region => _region;

    public AzureSpeechService(IConfiguration configuration, HttpClient httpClient, ILogger<AzureSpeechService> logger)
    {
        _logger = logger;
        _httpClient = httpClient;

        _subscriptionKey = configuration["AZURE_SPEECH_KEY"] 
            ?? configuration["SPEECH_KEY"] 
            ?? configuration["AzureSpeech:SubscriptionKey"];

        _region = configuration["AZURE_SPEECH_REGION"] 
            ?? configuration["SPEECH_REGION"] 
            ?? configuration["AzureSpeech:Region"] 
            ?? "westus";

        if (IsConfigured)
        {
            _logger.LogInformation("AzureSpeechService inicializado para la región '{Region}'.", _region);
        }
        else
        {
            _logger.LogWarning("AZURE_SPEECH_KEY no configurado. El servicio de voz funcionará mediante fallback de navegador o transcripción local.");
        }
    }

    public async Task<string?> TranscribeAudioAsync(Stream audioStream, string contentType, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Intento de transcripción sin clave de Azure Speech configurada.");
            return null;
        }

        try
        {
            var requestUrl = $"https://{_region}.stt.speech.microsoft.com/speech/recognition/conversation/cognitiveservices/v1?language=es-MX&format=detailed";

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Add("Ocp-Apim-Subscription-Key", _subscriptionKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var streamContent = new StreamContent(audioStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(contentType) ? "audio/wav" : contentType);
            request.Content = streamContent;

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Error en Azure Speech API: {StatusCode} - {Error}", response.StatusCode, errorText);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("DisplayText", out var textEl) && textEl.GetString() is { } text)
            {
                return text;
            }

            if (root.TryGetProperty("NBest", out var nBestArray) && nBestArray.GetArrayLength() > 0)
            {
                var first = nBestArray[0];
                if (first.TryGetProperty("Display", out var displayEl) && displayEl.GetString() is { } displayText)
                {
                    return displayText;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción al procesar audio en Azure Speech Service.");
            return null;
        }
    }
}

