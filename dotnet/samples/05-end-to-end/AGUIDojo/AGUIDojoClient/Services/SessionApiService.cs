using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;

namespace AGUIDojoClient.Services;

public interface ISessionApiService
{
    Task<List<ServerSessionSummary>?> ListSessionsAsync(CancellationToken ct = default);
    Task<ServerSessionDetail?> GetSessionAsync(string id, CancellationToken ct = default);
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

    [JsonPropertyName("subjectEntityId")]
    public string? SubjectEntityId { get; init; }

    [JsonPropertyName("preferredModelId")]
    public string? PreferredModelId { get; init; }
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

    [JsonPropertyName("subjectEntityId")]
    public string? SubjectEntityId { get; init; }

    [JsonPropertyName("aguiThreadId")]
    public string? AguiThreadId { get; init; }

    [JsonPropertyName("preferredModelId")]
    public string? PreferredModelId { get; init; }
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
