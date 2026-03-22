// Copyright (c) Microsoft. All rights reserved.

namespace AGUIDojoServer.Models;

/// <summary>
/// Application-owned registry mapping model identifiers to context window sizes and capabilities.
/// </summary>
public interface IModelRegistry
{
    /// <summary>Gets information about a specific model.</summary>
    ModelInfo? GetModel(string modelId);

    /// <summary>Gets all available models.</summary>
    IReadOnlyList<ModelInfo> GetAvailableModels();

    /// <summary>Gets the currently active model identifier.</summary>
    string ActiveModelId { get; }
}

/// <summary>
/// Configuration-backed model registry with sensible defaults for common OpenAI and Azure models.
/// </summary>
public sealed class ModelRegistry : IModelRegistry
{
    private readonly Dictionary<string, ModelInfo> _models;
    private readonly IReadOnlyList<ModelInfo> _modelList;

    public string ActiveModelId { get; }

    public ModelRegistry(IConfiguration configuration)
    {
        ActiveModelId = configuration["OPENAI_MODEL"]
            ?? configuration["AZURE_OPENAI_DEPLOYMENT_NAME"]
            ?? "gpt-5.4-mini";

        // Sensible defaults for common models — extend as needed
        var defaultModels = new ModelInfo[]
        {
            new("gpt-5.4-mini", "GPT-5.4 Mini", ContextWindowTokens: 1_047_576),
            new("gpt-5.4", "GPT-5.4", ContextWindowTokens: 1_047_576),
            new("gpt-5.1", "GPT-5.1", ContextWindowTokens: 1_047_576),
            new("gpt-5.1-mini", "GPT-5.1 Mini", ContextWindowTokens: 1_047_576),
            new("gpt-4.1", "GPT-4.1", ContextWindowTokens: 1_047_576),
            new("gpt-4.1-mini", "GPT-4.1 Mini", ContextWindowTokens: 1_047_576),
            new("gpt-4.1-nano", "GPT-4.1 Nano", ContextWindowTokens: 1_047_576),
            new("gpt-4o", "GPT-4o", ContextWindowTokens: 128_000, SupportsVision: true),
            new("gpt-4o-mini", "GPT-4o Mini", ContextWindowTokens: 128_000, SupportsVision: true),
        };

        _models = defaultModels.ToDictionary(m => m.ModelId, StringComparer.OrdinalIgnoreCase);
        _modelList = defaultModels;
    }

    public ModelInfo? GetModel(string modelId) =>
        _models.TryGetValue(modelId, out var info) ? info : null;

    public IReadOnlyList<ModelInfo> GetAvailableModels() => _modelList;
}
