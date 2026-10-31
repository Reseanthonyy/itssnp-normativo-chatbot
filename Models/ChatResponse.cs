using System.Text.Json.Serialization;

namespace ItssnpNormativo.Api.Models;

/// <summary>
/// Modelo de respuesta del asistente normativo
/// </summary>
public record ChatResponse(
    [property: JsonPropertyName("reply")]
    string Reply,

    [property: JsonPropertyName("conversation_id")]
    string ConversationId,

    [property: JsonPropertyName("citations")]
    IReadOnlyList<string>? Citations = null,

    [property: JsonPropertyName("query_id")]
    int? QueryId = null
);
