// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using System.Text.RegularExpressions;
using AGUIDojoClient.Models;

namespace AGUIDojoClient.Services;

/// <summary>
/// Service for parsing raw SSE (Server-Sent Events) data, specifically handling STATE_DELTA events
/// that are not recognized by the AGUIChatClient library.
/// </summary>
/// <remarks>
/// The AGUI library's BaseEventJsonConverter doesn't handle STATE_DELTA events in its Read method,
/// causing "Unknown BaseEvent type discriminator: 'STATE_DELTA'" errors. This service provides
/// a workaround by parsing STATE_DELTA events directly from raw SSE data.
/// </remarks>
public interface ISseEventParser
{
    /// <summary>
    /// Streams STATE_DELTA events from an HTTP response as JSON patch operations.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for the request.</param>
    /// <param name="requestUri">The request URI (endpoint path).</param>
    /// <param name="content">The request content (messages, tools, etc.).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of JSON patch operation batches.</returns>
    IAsyncEnumerable<SseStateDeltaEvent> StreamStateDeltaEventsAsync(
        HttpClient httpClient,
        string requestUri,
        HttpContent content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses a single SSE data line and extracts STATE_DELTA event if present.
    /// </summary>
    /// <param name="dataLine">The SSE data line (without "data: " prefix).</param>
    /// <returns>The parsed event, or null if not a STATE_DELTA event.</returns>
    SseStateDeltaEvent? TryParseStateDeltaEvent(string dataLine);
}

/// <summary>
/// Represents a STATE_DELTA event parsed from raw SSE data.
/// </summary>
public sealed class SseStateDeltaEvent
{
    /// <summary>
    /// Gets or sets the JSON patch operations from the delta.
    /// </summary>
    public List<JsonPatchOperation>? Operations { get; set; }

    /// <summary>
    /// Gets or sets the raw JSON element for the delta.
    /// </summary>
    public JsonElement? RawDelta { get; set; }
}

/// <summary>
/// Implementation of <see cref="ISseEventParser"/> that handles STATE_DELTA events.
/// </summary>
public sealed partial class SseEventParser : ISseEventParser
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [GeneratedRegex(@"^\s*data:\s*(.+)$", RegexOptions.Compiled)]
    private static partial Regex DataLineRegex();

    [GeneratedRegex(@"""type""\s*:\s*""STATE_DELTA""", RegexOptions.Compiled)]
    private static partial Regex StateDeltaTypeRegex();

    /// <inheritdoc />
    public async IAsyncEnumerable<SseStateDeltaEvent> StreamStateDeltaEventsAsync(
        HttpClient httpClient,
        string requestUri,
        HttpContent content,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, requestUri) { Content = content };
        request.Headers.Accept.ParseAdd("text/event-stream");

        using HttpResponseMessage response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using StreamReader reader = new(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null &&
               !cancellationToken.IsCancellationRequested)
        {
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            Match match = DataLineRegex().Match(line);
            if (!match.Success)
            {
                continue;
            }

            string dataContent = match.Groups[1].Value;
            SseStateDeltaEvent? evt = this.TryParseStateDeltaEvent(dataContent);
            if (evt is not null)
            {
                yield return evt;
            }
        }
    }

    /// <inheritdoc />
    public SseStateDeltaEvent? TryParseStateDeltaEvent(string dataLine)
    {
        if (string.IsNullOrWhiteSpace(dataLine))
        {
            return null;
        }

        // Quick check for STATE_DELTA type before parsing
        if (!StateDeltaTypeRegex().IsMatch(dataLine))
        {
            return null;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(dataLine);
            JsonElement root = doc.RootElement;

            // Verify it's a STATE_DELTA event
            if (!root.TryGetProperty("type", out JsonElement typeElement) ||
                typeElement.GetString() != "STATE_DELTA")
            {
                return null;
            }

            // Extract the delta property
            if (!root.TryGetProperty("delta", out JsonElement deltaElement))
            {
                return null;
            }

            // Parse the delta as JSON patch operations
            List<JsonPatchOperation>? operations = null;
            if (deltaElement.ValueKind == JsonValueKind.Array)
            {
                operations = JsonSerializer.Deserialize<List<JsonPatchOperation>>(
                    deltaElement.GetRawText(),
                    s_jsonOptions);
            }

            return new SseStateDeltaEvent
            {
                Operations = operations,
                RawDelta = deltaElement.Clone()
            };
        }
        catch (JsonException)
        {
            // Not valid JSON or doesn't match expected structure
            return null;
        }
    }
}
