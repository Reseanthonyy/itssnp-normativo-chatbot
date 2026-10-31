using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ItssnpNormativo.Api.Models;

/// <summary>
/// Solicitud para registrar calificación de respuesta (👍 = 1, 👎 = -1)
/// </summary>
public record FeedbackRequest(
    [property: JsonPropertyName("query_id")]
    int? QueryId,

    [property: JsonPropertyName("conversation_id")]
    string? ConversationId,

    [property: Range(-1, 1)]
    [property: JsonPropertyName("rating")]
    int Rating
);

/// <summary>
/// Métricas para el panel administrativo del ITSSNP
/// </summary>
public record AdminMetricsResponse(
    [property: JsonPropertyName("total_queries")]
    int TotalQueries,

    [property: JsonPropertyName("rated_queries")]
    int RatedQueries,

    [property: JsonPropertyName("satisfaction_rate")]
    int SatisfactionRate,

    [property: JsonPropertyName("positive_ratings")]
    int PositiveRatings,

    [property: JsonPropertyName("negative_ratings")]
    int NegativeRatings,

    [property: JsonPropertyName("top_categories")]
    IReadOnlyList<CategoryMetric> TopCategories,

    [property: JsonPropertyName("recent_queries")]
    IReadOnlyList<QueryLog> RecentQueries
);

public record CategoryMetric(
    [property: JsonPropertyName("category")]
    string Category,

    [property: JsonPropertyName("count")]
    int Count
);

/// <summary>
/// Resultado del servicio de transcripción de voz con Azure Speech
/// </summary>
public record SpeechTranscribeResponse(
    [property: JsonPropertyName("success")]
    bool Success,

    [property: JsonPropertyName("text")]
    string? Text,

    [property: JsonPropertyName("error")]
    string? Error = null
);
