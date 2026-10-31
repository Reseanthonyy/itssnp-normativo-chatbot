using System.ClientModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using ItssnpNormativo.Api.Models;
using OpenAI.Conversations;

namespace ItssnpNormativo.Api.Services;

public class FoundryAgentService : IFoundryAgentService
{
    private readonly ILogger<FoundryAgentService> _logger;
    private readonly string? _projectEndpoint;
    private readonly string _agentName;
    private readonly AIProjectClient? _projectClient;
    private readonly ProjectOpenAIClient? _openAIClient;
    private readonly ProjectConversationsClient? _conversationsClient;
    private readonly ProjectResponsesClient? _responsesClient;

    public bool IsConfigured => _projectClient != null && _responsesClient != null && !string.IsNullOrWhiteSpace(_projectEndpoint);
    public string AgentName => _agentName;

    public FoundryAgentService(IConfiguration configuration, ILogger<FoundryAgentService> logger)
    {
        _logger = logger;

        var rawEndpoint = configuration["FOUNDRY_PROJECT_ENDPOINT"] 
            ?? configuration["Foundry:ProjectEndpoint"];

        _agentName = configuration["FOUNDRY_AGENT_NAME"] 
            ?? configuration["Foundry:AgentName"] 
            ?? "asistente-normativo-itssnp";

        _projectEndpoint = CleanProjectEndpoint(rawEndpoint);

        if (!string.IsNullOrWhiteSpace(_projectEndpoint))
        {
            try
            {
                // Asegurar ruta de Azure CLI en Windows para entorno de desarrollo local
                if (OperatingSystem.IsWindows())
                {
                    const string azCliPath = @"C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin";
                    var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
                    if (!currentPath.Contains(azCliPath, StringComparison.OrdinalIgnoreCase))
                    {
                        Environment.SetEnvironmentVariable("PATH", $"{azCliPath};{currentPath}");
                    }
                }

                _projectClient = new AIProjectClient(new Uri(_projectEndpoint), new DefaultAzureCredential());
                _openAIClient = _projectClient.ProjectOpenAIClient;
                _conversationsClient = _openAIClient.GetProjectConversationsClient();
                _responsesClient = _openAIClient.GetProjectResponsesClientForAgent(_agentName);

                _logger.LogInformation("FoundryAgentService inicializado correctamente para el agente '{AgentName}'.", _agentName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al inicializar el cliente de Azure AI Foundry.");
            }
        }
        else
        {
            _logger.LogWarning("FOUNDRY_PROJECT_ENDPOINT no configurado. El servicio responderá en modo local no conectado.");
        }
    }

    public async Task<ChatResponse> AskAgentAsync(string message, string? conversationId, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || _responsesClient == null || _conversationsClient == null)
        {
            throw new InvalidOperationException("El servicio de Microsoft AI Foundry no se encuentra configurado o disponible.");
        }

        string activeConversationId;
        if (!string.IsNullOrWhiteSpace(conversationId))
        {
            activeConversationId = conversationId.Trim();
        }
        else
        {
            _logger.LogInformation("Creando nueva conversación en Azure AI Foundry...");
            var convResult = await _conversationsClient.CreateConversationAsync(new ConversationCreationOptions(), cancellationToken);
            activeConversationId = convResult.Value.Id;
            _logger.LogInformation("Conversación creada con ID: {ConversationId}", activeConversationId);
        }

        // Directiva de concisión para evitar respuestas excesivamente largas o párrafos innecesarios
        var conciseMessage = $"{message.Trim()}\n\n[Directiva: Responde de forma concisa, breve y directa (máximo 1 o 2 párrafos cortos o 2-3 viñetas clave), fundamentando con la normativa oficial aplicable sin rodeos ni texto excesivo.]";

        var payload = new
        {
            conversation = activeConversationId,
            agent_reference = new
            {
                name = _agentName,
                type = "agent_reference"
            },
            input = new[]
            {
                new
                {
                    role = "user",
                    content = conciseMessage
                }
            }
        };

        var content = BinaryContent.Create(BinaryData.FromObjectAsJson(payload));

        try
        {
            var rawResult = await _responsesClient.CreateResponseAsync(content, options: null);
            using var doc = JsonDocument.Parse(rawResult.GetRawResponse().Content);

            var (replyText, citations) = ExtractAssistantReplyAndCitations(doc.RootElement);

            if (string.IsNullOrWhiteSpace(replyText))
            {
                replyText = "No se recibió texto en la respuesta del agente normativo.";
            }

            return new ChatResponse(replyText, activeConversationId, citations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al consultar el agente '{AgentName}' en Foundry.", _agentName);
            throw;
        }
    }

    public async IAsyncEnumerable<ChatStreamChunk> AskAgentStreamAsync(
        string message,
        string? conversationId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ChatResponse? fullResponse = null;
        string? errorMessage = null;
        try
        {
            fullResponse = await AskAgentAsync(message, conversationId, cancellationToken);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }

        if (errorMessage != null || fullResponse == null)
        {
            yield return new ChatStreamChunk("error", errorMessage ?? "Error al procesar la respuesta.");
            yield break;
        }

        var text = fullResponse.Reply;
        var words = text.Split(' ');
        var batchSize = 2;

        for (int i = 0; i < words.Length; i += batchSize)
        {
            if (cancellationToken.IsCancellationRequested) yield break;

            var chunk = string.Join(" ", words.Skip(i).Take(batchSize));
            if (i + batchSize < words.Length)
            {
                chunk += " ";
            }

            yield return new ChatStreamChunk("delta", chunk);
            await Task.Delay(35, cancellationToken);
        }

        yield return new ChatStreamChunk("done", null, fullResponse.ConversationId, fullResponse.Citations);
    }

    private static (string Reply, IReadOnlyList<string> Citations) ExtractAssistantReplyAndCitations(JsonElement root)
    {
        var citations = new List<string>();
        string reply = "";

        if (root.TryGetProperty("output", out var outputArray) && outputArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in outputArray.EnumerateArray())
            {
                if (item.TryGetProperty("type", out var typeEl) && typeEl.GetString() == "message" &&
                    item.TryGetProperty("role", out var roleEl) && roleEl.GetString() == "assistant" &&
                    item.TryGetProperty("content", out var contentArray) && contentArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var contentPart in contentArray.EnumerateArray())
                    {
                        if (contentPart.TryGetProperty("type", out var partType) && partType.GetString() == "output_text")
                        {
                            if (contentPart.TryGetProperty("text", out var textEl))
                            {
                                reply = textEl.GetString() ?? "";
                            }

                            if (contentPart.TryGetProperty("annotations", out var annotations) && annotations.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var ann in annotations.EnumerateArray())
                                {
                                    if (ann.TryGetProperty("filename", out var fnEl) && fnEl.GetString() is { } fn && !citations.Contains(fn))
                                    {
                                        citations.Add(fn);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // Respaldo por si el campo 'output_text' viene en la raíz
        if (string.IsNullOrWhiteSpace(reply) && root.TryGetProperty("output_text", out var rootOutput))
        {
            reply = rootOutput.GetString() ?? "";
        }

        return (reply, citations);
    }

    private static string? CleanProjectEndpoint(string? rawEndpoint)
    {
        if (string.IsNullOrWhiteSpace(rawEndpoint)) return rawEndpoint;
        var match = Regex.Match(rawEndpoint.Trim(), @"^(https://[^/]+/api/projects/[^/]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : rawEndpoint.Trim().TrimEnd('/');
    }
}
