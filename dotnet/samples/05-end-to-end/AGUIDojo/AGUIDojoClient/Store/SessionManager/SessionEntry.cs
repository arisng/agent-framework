// Copyright (c) Microsoft. All rights reserved.

using AGUIDojoClient.Models;

namespace AGUIDojoClient.Store.SessionManager;

/// <summary>
/// Combines session metadata with the session's full state.
/// </summary>
public sealed record SessionEntry(SessionMetadata Metadata, SessionState State);
