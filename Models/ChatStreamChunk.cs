using System.Text.Json.Serialization;

namespace ItssnpNormativo.Api.Models;

/// <summary>
/// Representa un fragmento o evento de streaming en tiempo real (SSE)
/// </summary>
public record ChatStreamChunk(
    [property: JsonPropertyName("type")]
    string Type, // "delta", "done", "error"

    [property: JsonPropertyName("content")]
    string? Content = null,

    [property: JsonPropertyName("conversation_id")]
    string? ConversationId = null,

    [property: JsonPropertyName("citations")]
    IReadOnlyList<string>? Citations = null,

    [property: JsonPropertyName("query_id")]
    int? QueryId = null
);
