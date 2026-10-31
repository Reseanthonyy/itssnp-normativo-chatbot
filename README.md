# Asistente Normativo ITSSNP Enterprise (.NET 10 & Azure AI Foundry)

**Plataforma Institucional Cloud & IA - TecNM / ITSSNP**  
Implementación enterprise del Asistente Normativo Inteligente para la comunidad académica del **Instituto Tecnológico Superior de la Sierra Norte de Puebla (ITSSNP / TecNM Campus Zacatlán)**, construido sobre **.NET 10 Minimal APIs**, orquestado mediante **Microsoft AI Foundry**, con persistencia relacional híbrida (**PostgreSQL / SQLite**), servicios de voz con **Azure AI Speech** y panel de auditoría y satisfacción estudiantil.

---

## 🏛️ Arquitectura Técnica Integral

```
┌────────────────────────────────────────────────────────────────────────┐
│                        Navegador Web (Clientes)                        │
│                                                                        │
│   • Portal Institucional ITSSNP: Chatbot RAG, voz (🎙️) y feedback (👍/👎)
│   • Panel de Monitoreo (/admin.html): KPIs de satisfacción y auditoría │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │ HTTP / SSE / REST
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│             ASP.NET Core (.NET 10) Minimal APIs Enterprise             │
│                                                                        │
│   • Kestrel Server: Ultra bajo consumo de RAM (~35 MB)                 │
│   • Clean Architecture: Models, DTOs, Services e Interfaces            │
│   • Entity Framework Core: Capa de persistencia híbrida                │
│   • Azure Speech Integration: Transcripción de voz a texto             │
│   • Directiva de Concisión: Respuestas breves, directas y claras       │
│   • Documentación OpenAPI interactiva con Scalar (/scalar/v1)          │
└───────────────────┬────────────────────────────────┬───────────────────┘
                    │                                │
    Managed Identity (Entra ID)                      │ SQL / EF Core
    DefaultAzureCredential                           │
                    ▼                                ▼
┌──────────────────────────────────────┐  ┌──────────────────────────────┐
│        Microsoft AI Foundry          │  │     Capa de Base de Datos    │
│                                      │  │                              │
│ • Agente: asistente-normativo-itssnp │  │ • Azure Database for         │
│ • Motor RAG Vectorial (5 PDFs):      │  │   PostgreSQL Flexible Server │
│     1. Reglamento de Estudiantes     │  │   (o SQLite App Storage)     │
│     2. Calendario Escolar 26-27      │  │ • Tabla:                     │
│     3. Catálogo de Servicios/Costos  │  │   - QueryLogs:               │
│     4. Protocolo Accidente Escolar   │  │     • Pregunta del Alumno    │
│     5. Lineamientos Servicio Social  │  │     • Respuesta y Citas RAG  │
│                                      │  │     • Tema Detectado         │
│                                      │  │     • Feedback (👍 / 👎)     │
└──────────────────────────────────────┘  └──────────────────────────────┘
```

---

## 🚀 Estructura del Proyecto

```
proyecto-fase-2-dotnet/
├── Models/
│   ├── ChatRequest.cs          # DTO de entrada con validaciones
│   ├── ChatResponse.cs         # DTO de respuesta con citas y query_id
│   ├── ChatStreamChunk.cs      # Chunks de streaming SSE en tiempo real
│   ├── HealthResponse.cs       # DTO para monitoreo de servicios y base de datos
│   ├── QueryLog.cs             # Entidad EF Core: Auditoría y telemetría de consultas
│   └── FolioDtos.cs            # DTOs para feedback y métricas de satisfacción
├── Services/
│   ├── IFoundryAgentService.cs # Contrato de servicio desacoplado de Foundry
│   ├── FoundryAgentService.cs  # Cliente nativo Azure.AI.Projects y OpenAI (con regla de concisión)
│   ├── IAzureSpeechService.cs  # Contrato para procesamiento de voz
│   ├── AzureSpeechService.cs   # Conexión con Azure AI Speech Services
│   └── ItssnpDbContext.cs      # DbContext de EF Core con inicialización de datos
├── wwwroot/
│   ├── index.html              # Portal institucional ITSSNP (HTML5 semántico)
│   ├── app.js                  # Lógica del chatbot, streaming, voz (🎙️) y feedback (👍/👎)
│   ├── admin.html              # Panel de Monitoreo, Auditoría RAG y Métricas
│   ├── admin.js                # Lógica del panel: KPIs y tabla filtrada por satisfacción
│   ├── styles.css              # Estilos institucionales TecNM / ITSSNP
│   ├── logo_itssnp.webp        # Escudo oficial del ITSSNP
│   └── hero_itssnp.webp        # Fotografía del campus central de Zacatlán
├── appsettings.json            # Configuración tipada de Azure Foundry y servicios
├── ItssnpNormativo.Api.csproj  # Configuración .NET 10 y paquetes NuGet
└── Program.cs                  # Pipeline Minimal API y endpoints
```

---

## ⚙️ Requisitos y Ejecución Local

### Prerrequisitos
* [.NET 10 SDK](https://dotnet.microsoft.com/download)
* [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) con sesión iniciada:
  ```powershell
  az login
  ```

### Ejecutar Localmente
```powershell
cd proyecto-fase-2-dotnet
dotnet run
```
* **Portal Estudiantil:** `http://localhost:5000` (o la URL indicada en la consola).
* **Panel de Monitoreo (Admin):** `http://localhost:5000/admin.html` (PIN: `2026`).
* **Documentación OpenAPI (Scalar):** `http://localhost:5000/scalar/v1`.

---

## 📡 Endpoints de la API

### 1. Diagnóstico del Sistema (`GET /health`)
Verifica la conectividad con AI Foundry, el estado de la Base de Datos y Azure Speech.

### 2. Consulta al Asistente Normativo (`POST /api/chat`)
Envía una pregunta normativa y retorna la respuesta concisa fundamentada con citas:
```json
{
  "message": "¿Cuáles son los requisitos para el servicio social en el ITSSNP?",
  "conversation_id": null
}
```

### 3. Streaming en Tiempo Real (`POST /api/chat/stream`)
Transmite la respuesta token a token vía Server-Sent Events (SSE). Al concluir, devuelve el identificador de la consulta (`query_id`) para registrar feedback.

### 4. Retroalimentación de Calidad (`POST /api/feedback`)
Registra la calificación del estudiante (👍 = 1, 👎 = -1):
```json
{
  "query_id": 4,
  "conversation_id": "conv_075f9cce...",
  "rating": 1
}
```

### 5. Métricas Administrativas (`GET /api/admin/metrics`)
Devuelve indicadores de volumen de consultas, consultas calificadas, porcentaje de satisfacción estudiantil y temas más frecuentes.

### 6. Bitácora de Auditoría (`GET /api/admin/queries`)
Historial de preguntas, fuentes citadas y calificaciones, con filtro opcional por opinión (`rating=1` o `rating=-1`).

### 7. Transcripción de Audio (`POST /api/speech/transcribe`)
Procesa archivos de audio multipart con Azure AI Speech para transcripción en español (`es-MX`).

---

## ☁️ Despliegue en Azure App Service

```powershell
# 1. Compilar y publicar artefacto release
dotnet publish -c Release -o ./publish

# 2. Comprimir el paquete
Compress-Archive -Path ./publish/* -DestinationPath ./deploy-dotnet.zip -Force

# 3. Desplegar mediante Azure CLI
az webapp deploy --resource-group "rg-reseanthonyy-ia-2825" --name "itssnp-normativo" --src-path "./deploy-dotnet.zip" --type zip
```
