// Copyright (c) Microsoft. All rights reserved.

namespace AGUIWebChatClient.Components.Pages.Chat;

public sealed record PlanContent(
    IReadOnlyList<AgenticPlanPanel.AgenticPlanStep> Steps,
    string Title,
    string? Subtitle);
