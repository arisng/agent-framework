// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUIWebChatServer.AgenticUI;

[JsonSerializable(typeof(Plan))]
[JsonSerializable(typeof(Step))]
[JsonSerializable(typeof(JsonPatchOperation))]
[JsonSerializable(typeof(List<JsonPatchOperation>))]
[JsonSerializable(typeof(StepStatus))]
[JsonSerializable(typeof(WeatherInfo))]
internal sealed partial class AgenticUISerializerContext : JsonSerializerContext;
