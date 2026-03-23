// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI.AGUI.Shared;

namespace Microsoft.Agents.AI.AGUI;

internal sealed class AGUIHttpService(HttpClient client, string endpoint)
{
    private const int MaxReconnectAttempts = 3;

    // MY CUSTOMIZATION POINT: expose server-owned chat-session headers so sample clients can persist canonical session ids.
    public string? ServerSessionId { get; private set; }

    public async IAsyncEnumerable<BaseEvent> PostRunAsync(
        RunAgentInput input,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string? lastEventId = null;
        int reconnectAttempts = 0;
        this.ServerSessionId = null;

        while (true)
        {
            using HttpRequestMessage request = new(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(input, AGUIJsonSerializerContext.Default.RunAgentInput)
            };

            if (!string.IsNullOrWhiteSpace(lastEventId))
            {
                // MY CUSTOMIZATION POINT: resume native SSE streams from the last successfully parsed event when the server honors Last-Event-ID.
                request.Headers.TryAddWithoutValidation("Last-Event-ID", lastEventId);
            }

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ShouldReconnect(ex, lastEventId, reconnectAttempts, cancellationToken))
            {
                reconnectAttempts++;
                continue;
            }

            using (response)
            {
                Stream responseStream;
                try
                {
                    response.EnsureSuccessStatusCode();
                    this.ServerSessionId = response.Headers.TryGetValues("X-Session-Id", out IEnumerable<string>? sessionIds)
                        ? sessionIds.FirstOrDefault()
                        : null;

#if NET
                    responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
                    responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
#endif
                }
                catch (Exception ex) when (ShouldReconnect(ex, lastEventId, reconnectAttempts, cancellationToken))
                {
                    reconnectAttempts++;
                    continue;
                }

                IAsyncEnumerator<SseItem<BaseEvent>> enumerator =
                    SseParser.Create(responseStream, ItemParser).EnumerateAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);

                bool shouldReconnect = false;
                try
                {
                    while (true)
                    {
                        bool hasNext;
                        try
                        {
                            hasNext = await enumerator.MoveNextAsync().ConfigureAwait(false);
                        }
                        catch (Exception ex) when (ShouldReconnect(ex, lastEventId, reconnectAttempts, cancellationToken))
                        {
                            reconnectAttempts++;
                            shouldReconnect = true;
                            break;
                        }

                        if (!hasNext)
                        {
                            yield break;
                        }

                        SseItem<BaseEvent> sseItem = enumerator.Current;
                        BaseEvent parsedEvent = sseItem.Data;
                        parsedEvent.EventId = sseItem.EventId;
                        lastEventId = sseItem.EventId ?? lastEventId;
                        yield return parsedEvent;
                    }
                }
                finally
                {
                    await enumerator.DisposeAsync().ConfigureAwait(false);
                }

                if (!shouldReconnect)
                {
                    yield break;
                }
            }
        }
    }

    private static BaseEvent ItemParser(string type, ReadOnlySpan<byte> data)
    {
        return JsonSerializer.Deserialize(data, AGUIJsonSerializerContext.Default.BaseEvent) ??
            throw new InvalidOperationException("Failed to deserialize SSE item.");
    }

    private static bool ShouldReconnect(Exception exception, string? lastEventId, int reconnectAttempts, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested
            || string.IsNullOrWhiteSpace(lastEventId)
            || reconnectAttempts >= MaxReconnectAttempts)
        {
            return false;
        }

        return exception is HttpRequestException or IOException;
    }
}
