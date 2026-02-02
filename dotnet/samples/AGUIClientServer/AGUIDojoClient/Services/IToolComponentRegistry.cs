// Copyright (c) Microsoft. All rights reserved.

namespace AGUIDojoClient.Services;

/// <summary>
/// Interface for registering and resolving Blazor components for tool-specific UI rendering.
/// </summary>
public interface IToolComponentRegistry
{
    /// <summary>
    /// Gets the collection of registered tool names.
    /// </summary>
    IEnumerable<string> RegisteredTools { get; }

    /// <summary>
    /// Tries to get a Blazor component type for the specified tool name.
    /// </summary>
    /// <param name="toolName">The name of the tool.</param>
    /// <param name="componentType">When this method returns, contains the component type if found; otherwise, null.</param>
    /// <returns>True if a component was registered for the tool; otherwise, false.</returns>
    bool TryGetComponent(string toolName, out Type? componentType);

    /// <summary>
    /// Tries to get the parameter name used to pass data to the tool component.
    /// </summary>
    /// <param name="toolName">The name of the tool.</param>
    /// <param name="parameterName">When this method returns, contains the parameter name if found; otherwise, null.</param>
    /// <returns>True if a parameter name was registered for the tool; otherwise, false.</returns>
    bool TryGetParameterName(string toolName, out string? parameterName);

    /// <summary>
    /// Registers a Blazor component for a specific tool.
    /// </summary>
    /// <typeparam name="TComponent">The type of Blazor component to register.</typeparam>
    /// <param name="toolName">The name of the tool.</param>
    /// <param name="parameterName">The name of the component parameter that receives the tool result data.</param>
    void Register<TComponent>(string toolName, string parameterName) where TComponent : Microsoft.AspNetCore.Components.IComponent;

    /// <summary>
    /// Checks if a Blazor component is registered for the specified tool.
    /// </summary>
    /// <param name="toolName">The name of the tool.</param>
    /// <returns>True if a component is registered; otherwise, false.</returns>
    bool HasComponent(string toolName);
}
