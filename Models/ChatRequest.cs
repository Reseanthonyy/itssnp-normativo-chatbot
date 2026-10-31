using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ItssnpNormativo.Api.Models;

/// <summary>
/// Modelo de solicitud para el endpoint /api/chat
/// </summary>
public record ChatRequest(
    [property: Required(ErrorMessage = "El mensaje es obligatorio.")]
    [property: StringLength(2000, MinimumLength = 1, ErrorMessage = "El mensaje debe tener entre 1 y 2000 caracteres.")]
    [property: JsonPropertyName("message")]
    string Message,

    [property: JsonPropertyName("conversation_id")]
    string? ConversationId
);
