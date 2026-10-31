using ItssnpNormativo.Api.Models;

namespace ItssnpNormativo.Api.Services;

/// <summary>
/// Contrato para el servicio de orquestación con Azure AI Foundry
/// </summary>
public interface IFoundryAgentService
{
    bool IsConfigured { get; }
    string AgentName { get; }

    Task<ChatResponse> AskAgentAsync(string message, string? conversationId, CancellationToken cancellationToken = default);

    IAsyncEnumerable<ChatStreamChunk> AskAgentStreamAsync(string message, string? conversationId, CancellationToken cancellationToken = default);
}
