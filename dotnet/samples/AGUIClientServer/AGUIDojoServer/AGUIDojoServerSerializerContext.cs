// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;
using AGUIDojoServer.AgenticUI;
using AGUIDojoServer.Api;
using AGUIDojoServer.BackendToolRendering;
using AGUIDojoServer.HumanInTheLoop;
using AGUIDojoServer.PredictiveStateUpdates;
using AGUIDojoServer.SharedState;

namespace AGUIDojoServer;

// Note: Both WeatherInfo types are required - BackendToolRendering.WeatherInfo for legacy tools,
// Services.WeatherInfo for the new shared service pattern (task-1.1, task-1.2)
[JsonSerializable(typeof(WeatherInfo))]
[JsonSerializable(typeof(Services.WeatherInfo), TypeInfoPropertyName = "ServicesWeatherInfo")]
[JsonSerializable(typeof(Recipe))]
[JsonSerializable(typeof(Ingredient))]
[JsonSerializable(typeof(RecipeResponse))]
[JsonSerializable(typeof(Plan))]
[JsonSerializable(typeof(Step))]
[JsonSerializable(typeof(StepStatus))]
[JsonSerializable(typeof(StepStatus?))]
[JsonSerializable(typeof(JsonPatchOperation))]
[JsonSerializable(typeof(List<JsonPatchOperation>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(DocumentState))]
[JsonSerializable(typeof(ApprovalRequest))]
[JsonSerializable(typeof(ApprovalResponse))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSerializable(typeof(EmailRequest))]
[JsonSerializable(typeof(EmailResponse))]
[JsonSerializable(typeof(TokenRequest))]
[JsonSerializable(typeof(TokenResponse))]
internal sealed partial class AGUIDojoServerSerializerContext : JsonSerializerContext;
