// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Concurrent;
using AGUIDojoClient.Components.GenerativeUI;
using AGUIDojoClient.Components.ToolResults;

namespace AGUIDojoClient.Services;

/// <summary>
/// Specifies where a tool result component should be rendered.
/// </summary>
public enum RenderLocation
{
    /// <summary>
    /// Render in AssistantThought (collapsible section for debugging/raw data).
    /// </summary>
    AssistantThought,

    /// <summary>
    /// Render in message-list (visible by default for visual tool results).
    /// </summary>
    MessageList,

    /// <summary>
    /// Render in canvas-pane (for interactive shared artifacts).
    /// </summary>
    CanvasPane
}

/// <summary>
/// Metadata for tool component rendering.
/// </summary>
public sealed record ToolMetadata
{
    /// <summary>
    /// Gets or sets where the component should be rendered.
    /// </summary>
    public RenderLocation RenderLocation { get; init; } = RenderLocation.AssistantThought;

    /// <summary>
    /// Gets or sets whether the component is a visual display (should be prominently shown).
    /// </summary>
    public bool IsVisual { get; init; }

    /// <summary>
    /// Gets or sets whether the component requires user interaction (editable).
    /// </summary>
    public bool IsInteractive { get; init; }
}

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
    /// <param name="metadata">Optional metadata for component rendering. If null, defaults to AssistantThought rendering.</param>
    /// <exception cref="ArgumentNullException">Thrown when toolName or parameterName is null or empty.</exception>
    public void Register<TComponent>(string toolName, string parameterName, ToolMetadata? metadata = null) where TComponent : Microsoft.AspNetCore.Components.IComponent
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName, nameof(toolName));
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterName, nameof(parameterName));

        metadata ??= new ToolMetadata { RenderLocation = RenderLocation.AssistantThought, IsVisual = false, IsInteractive = false };
        this._registry[toolName] = new ToolRegistration(typeof(TComponent), parameterName, metadata);
    }

    /// <summary>
    /// Tries to get metadata for the specified tool component.
    /// </summary>
    /// <param name="toolName">The name of the tool.</param>
    /// <param name="metadata">When this method returns, contains the metadata if found; otherwise, null.</param>
    /// <returns>True if metadata was found for the tool; otherwise, false.</returns>
    public bool TryGetMetadata(string toolName, out ToolMetadata? metadata)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            metadata = null;
            return false;
        }

        if (this._registry.TryGetValue(toolName, out ToolRegistration? registration))
        {
            metadata = registration.Metadata;
            return true;
        }

        metadata = null;
        return false;
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
        // Classification: Visual component (should render in message-list, not AssistantThought)
        // Per task-5: Read-only weather card, non-interactive, should be visible by default
        this.Register<WeatherDisplay>(
            toolName: "get_weather",
            parameterName: "Weather",
            metadata: new ToolMetadata
            {
                RenderLocation = RenderLocation.MessageList,
                IsVisual = true,
                IsInteractive = false
            });

        // Register DataGridDisplay for the show_data_grid tool
        // Classification: Interactive shared artifact (search, sort, paginate, column visibility)
        // Per task-36/FR-001: Renders in CanvasPane "Data Table" tab via Fluxor ArtifactState
        this.Register<DataGridDisplay>(
            toolName: "show_data_grid",
            parameterName: "DataGrid",
            metadata: new ToolMetadata
            {
                RenderLocation = RenderLocation.CanvasPane,
                IsVisual = true,
                IsInteractive = true
            });

        // Register ChartDisplay for the show_chart tool
        // Classification: Visual component (read-only data visualization)
        // Per task-5: Read-only chart, should render in message-list
        this.Register<ChartDisplay>(
            toolName: "show_chart",
            parameterName: "Chart",
            metadata: new ToolMetadata
            {
                RenderLocation = RenderLocation.MessageList,
                IsVisual = true,
                IsInteractive = false
            });

        // Register DynamicFormDisplay for the show_form tool
        // Classification: Visual component (read-only form display)
        // Per task-5: One-time form submission (not iterative editing), should render in message-list
        this.Register<DynamicFormDisplay>(
            toolName: "show_form",
            parameterName: "Form",
            metadata: new ToolMetadata
            {
                RenderLocation = RenderLocation.MessageList,
                IsVisual = true,
                IsInteractive = false
            });
    }

    /// <summary>
    /// Represents a tool component registration.
    /// </summary>
    private sealed record ToolRegistration(Type ComponentType, string ParameterName, ToolMetadata Metadata);
}
