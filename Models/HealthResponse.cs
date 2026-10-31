using System.Text.Json.Serialization;

namespace ItssnpNormativo.Api.Models;

/// <summary>
/// Modelo de salud y diagnóstico del servicio enterprise del ITSSNP
/// </summary>
public record HealthResponse(
    [property: JsonPropertyName("status")]
    string Status,

    [property: JsonPropertyName("service")]
    string Service,

    [property: JsonPropertyName("framework")]
    string Framework,

    [property: JsonPropertyName("foundryConfigured")]
    bool FoundryConfigured,

    [property: JsonPropertyName("agentName")]
    string AgentName,

    [property: JsonPropertyName("timestamp")]
    DateTimeOffset Timestamp,

    [property: JsonPropertyName("databaseReady")]
    bool DatabaseReady = true,

    [property: JsonPropertyName("databaseProvider")]
    string DatabaseProvider = "SQLite / PostgreSQL",

    [property: JsonPropertyName("speechConfigured")]
    bool SpeechConfigured = false
);
