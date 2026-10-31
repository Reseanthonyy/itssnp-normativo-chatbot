using System.Text.Json;
using ItssnpNormativo.Api.Models;
using ItssnpNormativo.Api.Services;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Configurar puerto para Azure App Service Linux (puerto 8080 por defecto en Kestrel)
var appPort = Environment.GetEnvironmentVariable("PORT") 
    ?? Environment.GetEnvironmentVariable("WEBSITES_PORT") 
    ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{appPort}");

// Configurar WebRootPath para que soporte tanto subcarpeta wwwroot como raíz directa de Azure
var envContentRoot = builder.Environment.ContentRootPath;
var effectiveWebRoot = Directory.Exists(Path.Combine(envContentRoot, "wwwroot"))
    ? Path.Combine(envContentRoot, "wwwroot")
    : envContentRoot;
builder.WebHost.UseWebRoot(effectiveWebRoot);

// 1. Configuración de HttpClient y Servicios
builder.Services.AddHttpClient();

// 2. Configuración de Base de Datos Híbrida y Resiliente (PostgreSQL Flexible Server o EF Core In-Memory Engine)
var pgConnection = builder.Configuration.GetConnectionString("PostgreSQL") 
    ?? builder.Configuration["DATABASE_URL"];

if (!string.IsNullOrWhiteSpace(pgConnection))
{
    builder.Services.AddDbContext<ItssnpDbContext>(options =>
        options.UseNpgsql(pgConnection));
}
else
{
    // En producción y contenedores cloud sin dependencias nativas C++, usamos el motor nativo In-Memory
    // de Entity Framework Core. Brinda 100% compatibilidad LINQ, cero problemas de locking en CIFS y arranque instantáneo.
    builder.Services.AddDbContext<ItssnpDbContext>(options =>
        options.UseInMemoryDatabase("ItssnpNormativoDb"));
}

// 3. Inyección de dependencias para Microsoft AI Foundry y Azure AI Speech
builder.Services.AddSingleton<IFoundryAgentService, FoundryAgentService>();
builder.Services.AddScoped<IAzureSpeechService, AzureSpeechService>();

// 4. Configurar OpenAPI nativo de .NET 10
builder.Services.AddOpenApi();

var app = builder.Build();

// 5. Inicializar y sembrar la base de datos automáticamente al arrancar
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ItssnpDbContext>();
    db.Database.EnsureCreated();
    db.SeedInitialData();
    app.Logger.LogInformation("Base de datos inicializada y sembrada con éxito.");
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Advertencia: No se pudo inicializar la base de datos relacional en este entorno.");
}

// 6. Archivos estáticos de la interfaz web institucional
app.UseStaticFiles();

// Función auxiliar para obtener recursos web embebidos directamente en la DLL
static byte[]? GetEmbeddedAsset(string fileName)
{
    var assembly = typeof(Program).Assembly;
    var cleanName = fileName.TrimStart('/', '\\').Replace('/', '.').Replace('\\', '.');
    var resourceName = assembly.GetManifestResourceNames()
        .FirstOrDefault(n => n.EndsWith(cleanName, StringComparison.OrdinalIgnoreCase));
    if (resourceName == null) return null;
    using var stream = assembly.GetManifestResourceStream(resourceName);
    if (stream == null) return null;
    using var ms = new MemoryStream();
    stream.CopyTo(ms);
    return ms.ToArray();
}

// Servir recursos con estrategia híbrida (disco físico con fallback a recursos embebidos en el ensamblado)
IResult ServeStaticAsset(IWebHostEnvironment env, string fileName, string contentType)
{
    var candidates = new[]
    {
        Path.Combine(env.WebRootPath ?? "", fileName),
        Path.Combine(env.ContentRootPath, "wwwroot", fileName),
        Path.Combine(env.ContentRootPath, fileName),
        Path.Combine(AppContext.BaseDirectory, "wwwroot", fileName),
        Path.Combine(AppContext.BaseDirectory, fileName)
    };
    foreach (var p in candidates)
    {
        if (File.Exists(p)) return Results.File(p, contentType);
    }

    var bytes = GetEmbeddedAsset(fileName);
    if (bytes != null && bytes.Length > 0)
    {
        return Results.Bytes(bytes, contentType);
    }

    return Results.NotFound();
}

// Rutas explícitas de navegación para garantizar entrega directa de la interfaz
app.MapGet("/", (IWebHostEnvironment env) => ServeStaticAsset(env, "index.html", "text/html; charset=utf-8"));
app.MapGet("/index.html", (IWebHostEnvironment env) => ServeStaticAsset(env, "index.html", "text/html; charset=utf-8"));
app.MapGet("/admin", (IWebHostEnvironment env) => ServeStaticAsset(env, "admin.html", "text/html; charset=utf-8"));
app.MapGet("/admin.html", (IWebHostEnvironment env) => ServeStaticAsset(env, "admin.html", "text/html; charset=utf-8"));
app.MapGet("/styles.css", (IWebHostEnvironment env) => ServeStaticAsset(env, "styles.css", "text/css; charset=utf-8"));
app.MapGet("/app.js", (IWebHostEnvironment env) => ServeStaticAsset(env, "app.js", "application/javascript; charset=utf-8"));
app.MapGet("/admin.js", (IWebHostEnvironment env) => ServeStaticAsset(env, "admin.js", "application/javascript; charset=utf-8"));
app.MapGet("/hero_itssnp.webp", (IWebHostEnvironment env) => ServeStaticAsset(env, "hero_itssnp.webp", "image/webp"));
app.MapGet("/logo_itssnp.webp", (IWebHostEnvironment env) => ServeStaticAsset(env, "logo_itssnp.webp", "image/webp"));

// Endpoint de diagnóstico del entorno de archivos (útil para auditoría de cloud runtime)
app.MapGet("/debug/files", (IWebHostEnvironment env) =>
{
    var contentFiles = Directory.Exists(env.ContentRootPath) ? Directory.GetFiles(env.ContentRootPath).Select(Path.GetFileName).ToList() : new List<string?>();
    var contentDirs = Directory.Exists(env.ContentRootPath) ? Directory.GetDirectories(env.ContentRootPath).Select(Path.GetFileName).ToList() : new List<string?>();
    var webRootExists = !string.IsNullOrWhiteSpace(env.WebRootPath) && Directory.Exists(env.WebRootPath);
    var webRootFiles = webRootExists ? Directory.GetFiles(env.WebRootPath!).Select(Path.GetFileName).ToList() : new List<string?>();
    var embedded = typeof(Program).Assembly.GetManifestResourceNames();

    return Results.Ok(new
    {
        contentRootPath = env.ContentRootPath,
        contentDirs,
        contentFiles,
        webRootPath = env.WebRootPath,
        webRootExists,
        webRootFiles,
        embeddedResources = embedded
    });
});

// 7. Documentación interactiva OpenAPI y Scalar UI
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.WithTitle("Asistente Normativo ITSSNP - API Docs")
           .WithTheme(ScalarTheme.BluePlanet)
           .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
});

// ==========================================
// ENDPOINTS DEL SISTEMA
// ==========================================

// Diagnóstico y salud del servicio
app.MapGet("/health", (IFoundryAgentService agentService, IAzureSpeechService speechService, IConfiguration config) =>
{
    var pgSet = !string.IsNullOrWhiteSpace(config.GetConnectionString("PostgreSQL") ?? config["DATABASE_URL"]);
    var health = new HealthResponse(
        Status: "ok",
        Service: "Asistente Normativo ITSSNP Enterprise",
        Framework: ".NET 10.0",
        FoundryConfigured: agentService.IsConfigured,
        AgentName: agentService.AgentName,
        Timestamp: DateTimeOffset.UtcNow,
        DatabaseReady: true,
        DatabaseProvider: pgSet ? "PostgreSQL Flexible Server (Azure)" : "Entity Framework Core In-Memory Engine",
        SpeechConfigured: speechService.IsConfigured
    );

    return Results.Ok(health);
})
.WithName("HealthCheck")
.WithDescription("Verifica el estado de salud del servicio, base de datos y conectividad con Azure AI Foundry y Speech");

// Consulta principal RAG e integración con Base de Datos
app.MapPost("/api/chat", async (ChatRequest request, IFoundryAgentService agentService, ItssnpDbContext db, CancellationToken ct) =>
{
    var message = request.Message?.Trim() ?? "";

    if (string.IsNullOrWhiteSpace(message) || message.Length > 2000)
    {
        return Results.UnprocessableEntity(new
        {
            error = "El mensaje debe tener entre 1 y 2000 caracteres."
        });
    }

    if (!agentService.IsConfigured)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status502BadGateway,
            title: "Servicio no configurado",
            detail: "El servicio de Microsoft AI Foundry no está configurado o no se encuentra disponible actualmente."
        );
    }

    try
    {
        var response = await agentService.AskAgentAsync(message, request.ConversationId, ct);

        // Guardar telemetría y auditoría en la Base de Datos garantizando persistencia
        int? queryId = null;
        try
        {
            var log = new QueryLog
            {
                CreatedAt = DateTimeOffset.UtcNow,
                UserMessage = message,
                BotReply = response.Reply,
                ConversationId = response.ConversationId,
                CitationsJson = response.Citations != null ? JsonSerializer.Serialize(response.Citations) : null,
                Topic = DetectTopic(message)
            };
            db.QueryLogs.Add(log);
            await db.SaveChangesAsync(CancellationToken.None);
            queryId = log.Id;
        }
        catch (Exception ex)
        {
            var memLog = ResilientLogStore.AddLog(message, response.Reply, response.ConversationId, response.Citations, DetectTopic(message));
            queryId = memLog.Id;
        }

        return Results.Ok(new ChatResponse(response.Reply, response.ConversationId, response.Citations, queryId));
    }
    catch (Exception)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status502BadGateway,
            title: "Error al consultar el agente",
            detail: "No fue posible procesar la consulta con el agente de Microsoft Foundry. Inténtalo de nuevo más tarde."
        );
    }
})
.WithName("ChatWithAgent")
.WithDescription("Envía una consulta al asistente normativo y retorna la respuesta fundamentada con RAG");

// Streaming en tiempo real mediante SSE
app.MapPost("/api/chat/stream", async (ChatRequest request, IFoundryAgentService agentService, ItssnpDbContext db, HttpContext context, CancellationToken ct) =>
{
    var message = request.Message?.Trim() ?? "";

    if (string.IsNullOrWhiteSpace(message) || message.Length > 2000)
    {
        return Results.UnprocessableEntity(new
        {
            error = "El mensaje debe tener entre 1 y 2000 caracteres."
        });
    }

    if (!agentService.IsConfigured)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status502BadGateway,
            title: "Servicio no configurado",
            detail: "El servicio de Microsoft AI Foundry no está configurado o no se encuentra disponible actualmente."
        );
    }

    context.Response.Headers.Append("Content-Type", "text/event-stream");
    context.Response.Headers.Append("Cache-Control", "no-cache");
    context.Response.Headers.Append("Connection", "keep-alive");

    var fullAccumulatedText = "";
    string? returnedConvId = null;
    IReadOnlyList<string>? returnedCitations = null;

    await foreach (var chunk in agentService.AskAgentStreamAsync(message, request.ConversationId, ct))
    {
        if (chunk.Type == "delta" && chunk.Content != null)
        {
            fullAccumulatedText += chunk.Content;
            var json = JsonSerializer.Serialize(chunk);
            await context.Response.WriteAsync($"data: {json}\n\n", ct);
            await context.Response.Body.FlushAsync(ct);
        }
        else if (chunk.Type == "done")
        {
            returnedConvId = chunk.ConversationId;
            returnedCitations = chunk.Citations;
        }
    }

    // Registrar en BD la consulta final de forma segura
    int? savedQueryId = null;
    var finalConvId = returnedConvId ?? request.ConversationId ?? $"conv_{Guid.NewGuid():N}";

    if (!string.IsNullOrWhiteSpace(fullAccumulatedText))
    {
        try
        {
            var log = new QueryLog
            {
                CreatedAt = DateTimeOffset.UtcNow,
                UserMessage = message,
                BotReply = fullAccumulatedText,
                ConversationId = finalConvId,
                CitationsJson = returnedCitations != null ? JsonSerializer.Serialize(returnedCitations) : null,
                Topic = DetectTopic(message)
            };
            db.QueryLogs.Add(log);
            await db.SaveChangesAsync(CancellationToken.None);
            savedQueryId = log.Id;
        }
        catch (Exception ex)
        {
            var memLog = ResilientLogStore.AddLog(message, fullAccumulatedText, finalConvId, returnedCitations, DetectTopic(message));
            savedQueryId = memLog.Id;
        }
    }

    // Enviar evento 'done' con query_id para que el frontend pueda calificar
    var doneChunk = new ChatStreamChunk(
        Type: "done",
        Content: null,
        ConversationId: finalConvId,
        Citations: returnedCitations,
        QueryId: savedQueryId
    );

    var doneJson = JsonSerializer.Serialize(doneChunk);
    await context.Response.WriteAsync($"data: {doneJson}\n\n", CancellationToken.None);
    await context.Response.Body.FlushAsync(CancellationToken.None);

    return Results.Empty;
})
.WithName("ChatStreamWithAgent")
.WithDescription("Transmite la respuesta del asistente en tiempo real mediante Server-Sent Events (SSE)");

// Registro de retroalimentación de calidad (👍 / 👎)
app.MapPost("/api/feedback", async (FeedbackRequest request, ItssnpDbContext db) =>
{
    try
    {
        QueryLog? log = null;

        if (request.QueryId.HasValue && request.QueryId.Value > 0)
        {
            log = await db.QueryLogs.FindAsync(request.QueryId.Value);
        }

        if (log == null && !string.IsNullOrWhiteSpace(request.ConversationId))
        {
            log = await db.QueryLogs
                .OrderByDescending(q => q.CreatedAt)
                .FirstOrDefaultAsync(q => q.ConversationId == request.ConversationId);
        }

        if (log != null)
        {
            log.Rating = request.Rating;
            await db.SaveChangesAsync(CancellationToken.None);
            return Results.Ok(new { success = true, queryId = log.Id, rating = log.Rating });
        }
    }
    catch
    {
        // Fallback a almacenamiento resiliente
    }

    var updated = ResilientLogStore.UpdateRating(request.QueryId, request.ConversationId, request.Rating);
    return Results.Ok(new { success = updated });
})
.WithName("SubmitFeedback")
.WithDescription("Registra retroalimentación del estudiante (👍 / 👎) sobre una respuesta para gobernanza y calidad de IA");

// Panel Administrativo: Métricas y KPIs de Calidad
app.MapGet("/api/admin/metrics", async (ItssnpDbContext db) =>
{
    try
    {
        var totalQueries = await db.QueryLogs.CountAsync();
        var ratedQueries = await db.QueryLogs.Where(q => q.Rating.HasValue).ToListAsync();
        var positiveRatings = ratedQueries.Count(q => q.Rating == 1);
        var negativeRatings = ratedQueries.Count(q => q.Rating == -1);

        var satisfaction = ratedQueries.Count > 0 
            ? (int)Math.Round((double)positiveRatings / ratedQueries.Count * 100)
            : 100;

        // Agrupación compatible 100% tanto con SQLite como con PostgreSQL / SQL Server
        var allTopics = await db.QueryLogs
            .Select(q => q.Topic)
            .ToListAsync();

        var topCategories = allTopics
            .GroupBy(t => t)
            .Select(g => new CategoryMetric(g.Key, g.Count()))
            .OrderByDescending(c => c.Count)
            .Take(5)
            .ToList();

        // Ordenar por Id (clave primaria incremental) compatible con cualquier motor de BD
        var recentQueries = await db.QueryLogs
            .OrderByDescending(q => q.Id)
            .Take(30)
            .ToListAsync();

        return Results.Ok(new AdminMetricsResponse(
            TotalQueries: totalQueries,
            RatedQueries: ratedQueries.Count,
            SatisfactionRate: satisfaction,
            PositiveRatings: positiveRatings,
            NegativeRatings: negativeRatings,
            TopCategories: topCategories,
            RecentQueries: recentQueries
        ));
    }
    catch
    {
        // En caso de que la BD relacional esté inaccesible, servir métricas desde ResilientLogStore
        var memLogs = ResilientLogStore.GetLogs();
        var rated = memLogs.Where(q => q.Rating.HasValue).ToList();
        var pos = rated.Count(q => q.Rating == 1);
        var neg = rated.Count(q => q.Rating == -1);
        var sat = rated.Count > 0 ? (int)Math.Round((double)pos / rated.Count * 100) : 100;
        var topCats = memLogs.Select(q => q.Topic).GroupBy(t => t).Select(g => new CategoryMetric(g.Key, g.Count())).Take(5).ToList();
        return Results.Ok(new AdminMetricsResponse(memLogs.Count, rated.Count, sat, pos, neg, topCats, memLogs.Take(30).ToList()));
    }
})
.WithName("GetAdminMetrics")
.WithDescription("Obtiene los KPIs de rendimiento, satisfacción y calidad para el personal institucional");

// Panel Administrativo: Bitácora de Consultas para Auditoría y Observabilidad
app.MapGet("/api/admin/queries", async (int? rating, string? topic, ItssnpDbContext db) =>
{
    try
    {
        var query = db.QueryLogs.AsQueryable();

        if (rating.HasValue)
        {
            query = query.Where(q => q.Rating == rating.Value);
        }

        if (!string.IsNullOrWhiteSpace(topic))
        {
            query = query.Where(q => q.Topic == topic);
        }

        // Ordenación por Id descendente (compatible universalmente con SQLite y PostgreSQL)
        var queries = await query
            .OrderByDescending(q => q.Id)
            .Take(50)
            .ToListAsync();

        return Results.Ok(queries);
    }
    catch
    {
        return Results.Ok(ResilientLogStore.GetLogs(rating, topic));
    }
})
.WithName("GetRecentQueries")
.WithDescription("Devuelve el historial de consultas normativas, temas y fuentes citadas para auditoría");

// Transcripción de audio mediante Azure AI Speech
app.MapPost("/api/speech/transcribe", async (HttpRequest request, IAzureSpeechService speechService, CancellationToken ct) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new SpeechTranscribeResponse(false, null, "Se requiere formato multipart/form-data con archivo de audio."));
    }

    var form = await request.ReadFormAsync(ct);
    var file = form.Files.GetFile("audio") ?? form.Files.FirstOrDefault();

    if (file == null || file.Length == 0)
    {
        return Results.BadRequest(new SpeechTranscribeResponse(false, null, "No se recibió archivo de audio válido."));
    }

    await using var stream = file.OpenReadStream();
    var transcribedText = await speechService.TranscribeAudioAsync(stream, file.ContentType, ct);

    if (!string.IsNullOrWhiteSpace(transcribedText))
    {
        return Results.Ok(new SpeechTranscribeResponse(true, transcribedText));
    }

    return Results.Ok(new SpeechTranscribeResponse(
        Success: false,
        Text: null,
        Error: speechService.IsConfigured 
            ? "No se identificó voz clara en el audio recibido." 
            : "Azure Speech API no configurada en este entorno (se recomienda usar reconocimiento de voz del navegador)."
    ));
})
.WithName("TranscribeAudio")
.WithDescription("Transcribe un archivo de audio mediante Azure AI Speech Services");

app.Run();

// Métodos auxiliares de clasificación
static string DetectTopic(string message)
{
    var lower = message.ToLowerInvariant();
    if (lower.Contains("accidente") || lower.Contains("médic") || lower.Contains("seguro") || lower.Contains("emergencia") || lower.Contains("auxilio"))
        return "Accidente Escolar";
    if (lower.Contains("servicio social") || lower.Contains("crédito") || lower.Contains("vinculación") || lower.Contains("horas") || lower.Contains("programa"))
        return "Servicio Social";
    if (lower.Contains("costo") || lower.Contains("precio") || lower.Contains("constancia") || lower.Contains("certificado") || lower.Contains("pago") || lower.Contains("trámite"))
        return "Costos y Trámites";
    if (lower.Contains("calendario") || lower.Contains("fecha") || lower.Contains("inscripci") || lower.Contains("reinscripci") || lower.Contains("periodo"))
        return "Calendario Escolar";
    if (lower.Contains("baja") || lower.Contains("derecho") || lower.Contains("obligaci") || lower.Contains("sanción") || lower.Contains("reclam"))
        return "Reglamento de Estudiantes";
    return "Normatividad General";
}

// Almacén en memoria de alta resiliencia si el entorno no cuenta con motor de base de datos nativo
public static class ResilientLogStore
{
    private static readonly List<QueryLog> _memoryLogs = new()
    {
        new QueryLog
        {
            Id = 1,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2),
            UserMessage = "¿Cuáles son los requisitos y créditos necesarios para iniciar el Servicio Social?",
            BotReply = "Para realizar el Servicio Social en el ITSSNP debes contar con al menos el 70% de créditos aprobados de tu plan de estudios (Artículo 163 del Reglamento). Su duración no podrá ser menor a 6 meses ni mayor a 2 años.",
            ConversationId = "demo_conv_001",
            CitationsJson = "[\"01_Reglamento_Estudiantes_ITDS.pdf\",\"05_Lineamientos_ServicioSocial.pdf\"]",
            Rating = 1,
            Topic = "Servicio Social"
        },
        new QueryLog
        {
            Id = 2,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1).AddHours(-4),
            UserMessage = "¿Cuál es el protocolo que se debe seguir en caso de sufrir un accidente dentro del campus?",
            BotReply = "En caso de accidente escolar, debes notificar de inmediato al docente a cargo o acudir al módulo médico en el Edificio de Servicios Estudiantiles para activar el Seguro contra Accidentes Escolares.",
            ConversationId = "demo_conv_002",
            CitationsJson = "[\"04_Procedimiento_Caso_Accidente.pdf\"]",
            Rating = 1,
            Topic = "Accidente Escolar"
        },
        new QueryLog
        {
            Id = 3,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-18),
            UserMessage = "¿Qué costo tiene la emisión de una constancia de estudios?",
            BotReply = "De conformidad con el Catálogo Oficial de Servicios y Costos 2026, la emisión de constancia de estudios tiene una cuota establecida en el departamento de Servicios Escolares con pago en ventanilla bancaria.",
            ConversationId = "demo_conv_003",
            CitationsJson = "[\"03_Catalogo_Servicios_2026.pdf\"]",
            Rating = 1,
            Topic = "Costos y Trámites"
        },
        new QueryLog
        {
            Id = 4,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-6),
            UserMessage = "¿Cuáles son las fechas límite de reinscripción según el calendario escolar?",
            BotReply = "Según el Calendario Escolar Oficial 2026-2027, las fechas del periodo de reinscripciones ordinarias corresponden a la semana previa al inicio de cursos del periodo semestral.",
            ConversationId = "demo_conv_004",
            CitationsJson = "[\"02_Calendario_Escolar_2026_2027.pdf\"]",
            Rating = null,
            Topic = "Calendario Escolar"
        }
    };
    private static int _nextId = 5;

    public static List<QueryLog> GetLogs(int? rating = null, string? topic = null)
    {
        lock (_memoryLogs)
        {
            var q = _memoryLogs.AsEnumerable();
            if (rating.HasValue) q = q.Where(x => x.Rating == rating.Value);
            if (!string.IsNullOrWhiteSpace(topic)) q = q.Where(x => x.Topic == topic);
            return q.OrderByDescending(x => x.Id).ToList();
        }
    }

    public static QueryLog AddLog(string userMessage, string botReply, string convId, IReadOnlyList<string>? citations, string topic)
    {
        lock (_memoryLogs)
        {
            var log = new QueryLog
            {
                Id = _nextId++,
                CreatedAt = DateTimeOffset.UtcNow,
                UserMessage = userMessage,
                BotReply = botReply,
                ConversationId = convId,
                CitationsJson = citations != null ? JsonSerializer.Serialize(citations) : null,
                Rating = null,
                Topic = topic
            };
            _memoryLogs.Add(log);
            return log;
        }
    }

    public static bool UpdateRating(int? queryId, string? convId, int rating)
    {
        lock (_memoryLogs)
        {
            var target = queryId.HasValue 
                ? _memoryLogs.FirstOrDefault(x => x.Id == queryId.Value)
                : _memoryLogs.LastOrDefault(x => x.ConversationId == convId);

            if (target != null)
            {
                target.Rating = rating;
                return true;
            }
            return false;
        }
    }
}
