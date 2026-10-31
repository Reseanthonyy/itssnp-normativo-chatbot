using ItssnpNormativo.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ItssnpNormativo.Api.Services;

public class ItssnpDbContext : DbContext
{
    public ItssnpDbContext(DbContextOptions<ItssnpDbContext> options) : base(options)
    {
    }

    public DbSet<QueryLog> QueryLogs => Set<QueryLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<QueryLog>(entity =>
        {
            entity.HasIndex(e => e.ConversationId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Topic);
            entity.HasIndex(e => e.Rating);
        });
    }

    /// <summary>
    /// Inicializa la base de datos con datos de muestra realistas si está vacía
    /// </summary>
    public void SeedInitialData()
    {
        if (!QueryLogs.Any())
        {
            QueryLogs.AddRange(
                new QueryLog
                {
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
                    CreatedAt = DateTimeOffset.UtcNow.AddHours(-6),
                    UserMessage = "¿Cuáles son las fechas límite de reinscripción según el calendario escolar?",
                    BotReply = "Según el Calendario Escolar Oficial 2026-2027, las fechas del periodo de reinscripciones ordinarias corresponden a la semana previa al inicio de cursos del periodo semestral.",
                    ConversationId = "demo_conv_004",
                    CitationsJson = "[\"02_Calendario_Escolar_2026_2027.pdf\"]",
                    Rating = null,
                    Topic = "Calendario Escolar"
                }
            );

            SaveChanges();
        }
    }
}
