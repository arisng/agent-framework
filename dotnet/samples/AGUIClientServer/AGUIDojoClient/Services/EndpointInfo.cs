// Copyright (c) Microsoft. All rights reserved.

namespace AGUIDojoClient.Services;

/// <summary>
/// Represents information about an AG-UI endpoint.
/// </summary>
/// <param name="Path">The endpoint path (e.g., "agentic_chat").</param>
/// <param name="DisplayName">Human-readable display name for the endpoint.</param>
/// <param name="Description">Description of the AG-UI feature this endpoint demonstrates.</param>
public sealed record EndpointInfo(string Path, string DisplayName, string Description);
