// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Concurrent;
using AGUIDojoClient.Components.ToolResults;

namespace AGUIDojoClient.Services;

/// <summary>
/// Registry that maps tool names to Blazor component types for dynamic UI rendering.
/// Uses the component registry pattern to enable DynamicComponent rendering based on tool definitions.
/// </summary>
public sealed class ToolComponentRegistry : IToolComponentRegistry
{
    private readonly ConcurrentDictionary<string, ToolRegistration> _registry = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolComponentRegistry"/> class
    /// with default tool component registrations.
    /// </summary>
    public ToolComponentRegistry()
    {
        // Register default tool components
        this.RegisterDefaults();
    }

    /// <summary>
    /// Gets the collection of registered tool names.
    /// </summary>
    public IEnumerable<string> RegisteredTools => this._registry.Keys;

    /// <summary>
    /// Tries to get a Blazor component type for the specified tool name.
    /// </summary>
    /// <param name="toolName">The name of the tool.</param>
    /// <param name="componentType">When this method returns, contains the component type if found; otherwise, null.</param>
    /// <returns>True if a component was registered for the tool; otherwise, false.</returns>
    public bool TryGetComponent(string toolName, out Type? componentType)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            componentType = null;
            return false;
        }

        if (this._registry.TryGetValue(toolName, out ToolRegistration? registration))
        {
            componentType = registration.ComponentType;
            return true;
        }

        componentType = null;
        return false;
    }

    /// <summary>
    /// Tries to get the parameter name used to pass data to the tool component.
    /// </summary>
    /// <param name="toolName">The name of the tool.</param>
    /// <param name="parameterName">When this method returns, contains the parameter name if found; otherwise, null.</param>
    /// <returns>True if a parameter name was registered for the tool; otherwise, false.</returns>
    public bool TryGetParameterName(string toolName, out string? parameterName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            parameterName = null;
            return false;
        }

        if (this._registry.TryGetValue(toolName, out ToolRegistration? registration))
        {
            parameterName = registration.ParameterName;
            return true;
        }

        parameterName = null;
        return false;
    }

    /// <summary>
    /// Registers a Blazor component for a specific tool.
    /// </summary>
    /// <typeparam name="TComponent">The type of Blazor component to register.</typeparam>
    /// <param name="toolName">The name of the tool.</param>
    /// <param name="parameterName">The name of the component parameter that receives the tool result data.</param>
    /// <exception cref="ArgumentNullException">Thrown when toolName or parameterName is null or empty.</exception>
    public void Register<TComponent>(string toolName, string parameterName) where TComponent : Microsoft.AspNetCore.Components.IComponent
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName, nameof(toolName));
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterName, nameof(parameterName));

        this._registry[toolName] = new ToolRegistration(typeof(TComponent), parameterName);
    }

    /// <summary>
    /// Checks if a Blazor component is registered for the specified tool.
    /// </summary>
    /// <param name="toolName">The name of the tool.</param>
    /// <returns>True if a component is registered; otherwise, false.</returns>
    public bool HasComponent(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return false;
        }

        return this._registry.ContainsKey(toolName);
    }

    /// <summary>
    /// Registers the default set of tool components.
    /// </summary>
    private void RegisterDefaults()
    {
        // Register WeatherDisplay for the get_weather tool
        this.Register<WeatherDisplay>("get_weather", "Weather");
    }

    /// <summary>
    /// Represents a tool component registration.
    /// </summary>
    private sealed record ToolRegistration(Type ComponentType, string ParameterName);
}
