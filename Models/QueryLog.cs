using System.ComponentModel.DataAnnotations;

namespace ItssnpNormativo.Api.Models;

/// <summary>
/// Registro de auditoría y telemetría de una consulta realizada al asistente
/// </summary>
public class QueryLog
{
    [Key]
    public int Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [MaxLength(2000)]
    public string UserMessage { get; set; } = "";

    public string BotReply { get; set; } = "";

    [MaxLength(100)]
    public string ConversationId { get; set; } = "";

    public string? CitationsJson { get; set; }

    /// <summary>
    /// 1 = Útil (Like), -1 = No útil (Dislike), null = Sin calificar
    /// </summary>
    public int? Rating { get; set; }

    [MaxLength(100)]
    public string Topic { get; set; } = "General";
}

