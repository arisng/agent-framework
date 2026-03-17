// Copyright (c) Microsoft. All rights reserved.

namespace AGUIDojoClient.Models;

/// <summary>
/// Holds the before/after state and title for a diff preview displayed in the Canvas Pane.
/// Used by the <see cref="Components.Governance.DiffPreview"/> component and tracked per session.
/// </summary>
/// <param name="Before">The state object before the change (serialized to JSON for display). Null = initial state.</param>
/// <param name="After">The state object after the change (serialized to JSON for display). Null = deleted.</param>
/// <param name="Title">A human-readable title describing the diff (e.g., "Plan State Change", "Recipe State Change").</param>
public sealed record DiffPreviewData(object? Before, object? After, string Title);
