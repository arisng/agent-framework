namespace AGUIDojoServer.Models;

/// <summary>
/// Describes an available AI model in the application-owned registry.
/// </summary>
/// <param name="ModelId">Unique identifier for routing (e.g. "gpt-4.1").</param>
/// <param name="DisplayName">Human-readable label for the UI.</param>
/// <param name="ContextWindowTokens">Maximum context window in tokens.</param>
/// <param name="SupportsVision">Whether the model can process image inputs.</param>
public sealed record ModelInfo(
    string ModelId,
    string DisplayName,
    int ContextWindowTokens,
    bool SupportsVision = false);
