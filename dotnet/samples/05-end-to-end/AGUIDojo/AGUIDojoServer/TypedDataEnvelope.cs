// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUIDojoServer;

internal static class TypedDataEnvelopeTypes
{
    public const string PlanSnapshot = "plan_snapshot";
    public const string RecipeSnapshot = "recipe_snapshot";
    public const string DocumentPreview = "document_preview";
}

internal sealed class TypedDataEnvelope<T>
{
    [JsonPropertyName("$type")]
    public required string Type { get; init; }

    [JsonPropertyName("data")]
    public required T Data { get; init; }
}
