// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

#if ASPNETCORE
namespace Microsoft.Agents.AI.Hosting.AGUI.AspNetCore.Shared;
#else
namespace Microsoft.Agents.AI.AGUI.Shared;
#endif

[JsonConverter(typeof(BaseEventJsonConverter))]
internal abstract class BaseEvent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    // MY CUSTOMIZATION POINT: retain native SSE event ids separately from the AG-UI JSON payload for reconnect handling.
    [JsonIgnore]
    public string? EventId { get; set; }
}
