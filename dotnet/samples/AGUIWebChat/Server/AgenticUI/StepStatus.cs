// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUIWebChatServer.AgenticUI;

[JsonConverter(typeof(JsonStringEnumConverter<StepStatus>))]
internal enum StepStatus
{
    Pending,
    Completed
}
