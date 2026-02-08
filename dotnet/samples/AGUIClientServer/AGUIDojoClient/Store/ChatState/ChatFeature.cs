// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Immutable;
using Fluxor;
using Microsoft.Extensions.AI;

namespace AGUIDojoClient.Store.ChatState;

/// <summary>
/// Fluxor feature definition for <see cref="ChatState"/>.
/// Provides the initial state and feature name for Fluxor's assembly scanning registration.
/// </summary>
/// <remarks>
/// Discovered automatically by Fluxor via
/// <c>builder.Services.AddFluxor(o =&gt; o.ScanAssemblies(typeof(Program).Assembly))</c>
/// registered in <c>Program.cs</c>.
/// The initial state has an empty message list, no in-progress response,
/// no conversation ID, zero stateful count, and no pending approval.
/// </remarks>
public sealed class ChatFeature : Feature<ChatState>
{
    /// <summary>
    /// Gets the display name for this feature in Fluxor DevTools.
    /// </summary>
    public override string GetName() => "Chat";

    /// <summary>
    /// Gets the initial state with an empty conversation and no active streaming.
    /// </summary>
    /// <returns>A default <see cref="ChatState"/> with all properties set to their initial values.</returns>
    protected override ChatState GetInitialState() => new()
    {
        Messages = ImmutableList<ChatMessage>.Empty,
        CurrentResponseMessage = null,
        ConversationId = null,
        StatefulMessageCount = 0,
        PendingApproval = null
    };
}
