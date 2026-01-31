// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUIWebChatServer.AgenticUI;

internal sealed class Plan
{
    [JsonPropertyName("steps")]
    public List<Step> Steps { get; set; } = [];
}
