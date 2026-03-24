using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;

namespace AGUIDojoClient.Services;

public interface ISessionApiService
{
    Task<List<ServerSessionSummary>?> ListSessionsAsync(CancellationToken ct = default);
    Task<ServerSessionDetail?> GetSessionAsync(string id, CancellationToken ct = default);
    Task<ServerConversationGraph?> GetConversationAsync(string id, CancellationToken ct = default);
    Task<ServerModelCatalog?> GetModelCatalogAsync(CancellationToken ct = default);
    Task<bool> SetActiveLeafAsync(string id, string activeLeafId, CancellationToken ct = default);
    Task<bool> ClearConversationAsync(string id, CancellationToken ct = default);
    Task<bool> ArchiveSessionAsync(string id, CancellationToken ct = default);
}

public sealed record ServerSessionSummary
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("lastActivityAt")]
    public DateTimeOffset LastActivityAt { get; init; }

    [JsonPropertyName("subjectModule")]
    public string? SubjectModule { get; init; }

    [JsonPropertyName("subjectEntityType")]
    public string? SubjectEntityType { get; init; }

    [JsonPropertyName("subjectEntityId")]
    public string? SubjectEntityId { get; init; }

    [JsonPropertyName("preferredModelId")]
    public string? PreferredModelId { get; init; }

    [JsonPropertyName("aguiThreadId")]
    public string? AguiThreadId { get; init; }

    [JsonPropertyName("serverProtocolVersion")]
    public string? ServerProtocolVersion { get; init; }
}

public sealed record ServerSessionDetail
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("lastActivityAt")]
    public DateTimeOffset LastActivityAt { get; init; }

    [JsonPropertyName("archivedAt")]
    public DateTimeOffset? ArchivedAt { get; init; }

    [JsonPropertyName("subjectModule")]
    public string? SubjectModule { get; init; }

    [JsonPropertyName("subjectEntityType")]
    public string? SubjectEntityType { get; init; }

    [JsonPropertyName("subjectEntityId")]
    public string? SubjectEntityId { get; init; }

    [JsonPropertyName("aguiThreadId")]
    public string? AguiThreadId { get; init; }

    [JsonPropertyName("preferredModelId")]
    public string? PreferredModelId { get; init; }

    [JsonPropertyName("serverProtocolVersion")]
    public string? ServerProtocolVersion { get; init; }
}

public sealed record ServerConversationNode
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("parentId")]
    public string? ParentId { get; init; }

    [JsonPropertyName("childIds")]
    public List<string> ChildIds { get; init; } = [];

    [JsonPropertyName("messageId")]
    public string? MessageId { get; init; }

    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("authorName")]
    public string? AuthorName { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("content")]
    public JsonElement? Content { get; init; }

    [JsonPropertyName("additionalProperties")]
    public JsonElement? AdditionalProperties { get; init; }

    [JsonPropertyName("createdAt")]
    public JsonElement? CreatedAt { get; init; }

    public DateTimeOffset GetCreatedAtOrDefault()
    {
        if (CreatedAt is null || CreatedAt.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return DateTimeOffset.UtcNow;
        }

        JsonElement createdAt = CreatedAt.Value;
        if (createdAt.ValueKind == JsonValueKind.Number && createdAt.TryGetInt64(out long unixTimeMilliseconds))
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMilliseconds);
        }

        if (createdAt.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(
                createdAt.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTimeOffset parsed))
        {
            return parsed;
        }

        return DateTimeOffset.UtcNow;
    }
}

public sealed record ActiveLeafUpdateRequest
{
    [JsonPropertyName("activeLeafId")]
    public required string ActiveLeafId { get; init; }
}

public sealed record ServerModelInfo
{
    [JsonPropertyName("modelId")]
    public required string ModelId { get; init; }

    [JsonPropertyName("displayName")]
    public required string DisplayName { get; init; }

    [JsonPropertyName("contextWindowTokens")]
    public required int ContextWindowTokens { get; init; }

    [JsonPropertyName("supportsVision")]
    public bool SupportsVision { get; init; }
}

public sealed record ServerModelCatalog
{
    [JsonPropertyName("models")]
    public List<ServerModelInfo> Models { get; init; } = [];

    [JsonPropertyName("activeModelId")]
    public string? ActiveModelId { get; init; }
}

public sealed record ServerConversationGraph
{
    [JsonPropertyName("rootId")]
    public string? RootId { get; init; }

    [JsonPropertyName("activeLeafId")]
    public string? ActiveLeafId { get; init; }

    [JsonPropertyName("nodes")]
    public List<ServerConversationNode> Nodes { get; init; } = [];
}

public sealed class SessionApiService : ISessionApiService
{
    private readonly HttpClient _httpClient;
    private readonly NavigationManager _navigationManager;
    private readonly ILogger<SessionApiService> _logger;

    public SessionApiService(
        IHttpClientFactory httpClientFactory,
        NavigationManager navigationManager,
        ILogger<SessionApiService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("business-api");
        _navigationManager = navigationManager;
        _logger = logger;
    }

    public async Task<List<ServerSessionSummary>?> ListSessionsAsync(CancellationToken ct = default)
    {
        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(
                _navigationManager.ToAbsoluteUri("/api/chat-sessions"),
                ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Loading server sessions failed with status code {StatusCode}.", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<List<ServerSessionSummary>>(cancellationToken: ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Loading server sessions timed out.");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Loading server sessions failed.");
            return null;
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex, "Loading server sessions returned an unsupported content type.");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Loading server sessions returned invalid JSON.");
            return null;
        }
    }

    public async Task<ServerSessionDetail?> GetSessionAsync(string id, CancellationToken ct = default)
    {
        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(
                BuildUri($"/api/chat-sessions/{Uri.EscapeDataString(id)}"),
                ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Loading server session {SessionId} failed with status code {StatusCode}.", id, response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ServerSessionDetail>(cancellationToken: ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Loading server session {SessionId} timed out.", id);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Loading server session {SessionId} failed.", id);
            return null;
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex, "Loading server session {SessionId} returned an unsupported content type.", id);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Loading server session {SessionId} returned invalid JSON.", id);
            return null;
        }
    }

    public async Task<ServerConversationGraph?> GetConversationAsync(string id, CancellationToken ct = default)
    {
        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(
                BuildUri($"/api/chat-sessions/{Uri.EscapeDataString(id)}/conversation"),
                ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Loading server conversation {SessionId} failed with status code {StatusCode}.", id, response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ServerConversationGraph>(cancellationToken: ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Loading server conversation {SessionId} timed out.", id);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Loading server conversation {SessionId} failed.", id);
            return null;
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex, "Loading server conversation {SessionId} returned an unsupported content type.", id);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Loading server conversation {SessionId} returned invalid JSON.", id);
            return null;
        }
    }

    public async Task<ServerModelCatalog?> GetModelCatalogAsync(CancellationToken ct = default)
    {
        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(
                _navigationManager.ToAbsoluteUri("/api/models"),
                ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Loading model catalog failed with status code {StatusCode}.", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ServerModelCatalog>(cancellationToken: ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Loading model catalog timed out.");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Loading model catalog failed.");
            return null;
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex, "Loading model catalog returned an unsupported content type.");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Loading model catalog returned invalid JSON.");
            return null;
        }
    }

    public async Task<bool> SetActiveLeafAsync(string id, string activeLeafId, CancellationToken ct = default)
    {
        try
        {
            using HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                BuildUri($"/api/chat-sessions/{Uri.EscapeDataString(id)}/active-leaf"),
                new ActiveLeafUpdateRequest { ActiveLeafId = activeLeafId },
                cancellationToken: ct);

            if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.BadRequest)
            {
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Updating server active leaf {SessionId} failed with status code {StatusCode}.", id, response.StatusCode);
                return false;
            }

            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Updating server active leaf {SessionId} timed out.", id);
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Updating server active leaf {SessionId} failed.", id);
            return false;
        }
    }

    public async Task<bool> ClearConversationAsync(string id, CancellationToken ct = default)
    {
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Delete, BuildUri($"/api/chat-sessions/{Uri.EscapeDataString(id)}/conversation"));
            using HttpResponseMessage response = await _httpClient.SendAsync(request, ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Clearing server conversation {SessionId} failed with status code {StatusCode}.", id, response.StatusCode);
                return false;
            }

            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Clearing server conversation {SessionId} timed out.", id);
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Clearing server conversation {SessionId} failed.", id);
            return false;
        }
    }

    public async Task<bool> ArchiveSessionAsync(string id, CancellationToken ct = default)
    {
        try
        {
            using HttpResponseMessage response = await _httpClient.PostAsync(
                BuildUri($"/api/chat-sessions/{Uri.EscapeDataString(id)}/archive"),
                content: null,
                ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Archiving server session {SessionId} failed with status code {StatusCode}.", id, response.StatusCode);
                return false;
            }

            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Archiving server session {SessionId} timed out.", id);
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Archiving server session {SessionId} failed.", id);
            return false;
        }
    }

    private Uri BuildUri(string relativePath) => _navigationManager.ToAbsoluteUri(relativePath);
}
