// Copyright (c) Microsoft. All rights reserved.

using AGUIWebChat.Server.Mocks.ToolSets;

namespace AGUIWebChat.Server.Mocks;

/// <summary>
/// Registry for managing and discovering tool sets by keyword matching.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="ToolSetRegistry"/> provides a centralized mechanism for registering
/// tool sets and finding the appropriate tool set based on user input keywords.
/// </para>
/// <para>
/// Tool sets are matched by checking if any of their trigger keywords exist in the
/// user message (case-insensitive). The first registered tool set with a matching
/// keyword is returned.
/// </para>
/// <example>
/// <code>
/// var registry = new ToolSetRegistry();
/// registry.Register(new PlanToolSet());
/// registry.Register(new WeatherToolSet());
///
/// // Find a tool set based on user input
/// var toolSet = registry.FindByKeywords("Can you create a plan for my project?");
/// </code>
/// </example>
/// </remarks>
public sealed class ToolSetRegistry
{
    /// <summary>
    /// Internal storage for registered tool sets.
    /// </summary>
    private readonly List<IToolSet> _toolSets = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolSetRegistry"/> class.
    /// </summary>
    public ToolSetRegistry()
    {
    }

    /// <summary>
    /// Gets the number of registered tool sets.
    /// </summary>
    /// <value>The count of registered tool sets.</value>
    public int Count => this._toolSets.Count;

    /// <summary>
    /// Registers a tool set with the registry.
    /// </summary>
    /// <param name="toolSet">The tool set to register.</param>
    /// <returns>The current <see cref="ToolSetRegistry"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="toolSet"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Tool sets are matched in registration order. Register more specific tool sets
    /// before more general ones to ensure proper keyword matching priority.
    /// </remarks>
    public ToolSetRegistry Register(IToolSet toolSet)
    {
        ArgumentNullException.ThrowIfNull(toolSet);

        this._toolSets.Add(toolSet);

        return this;
    }

    /// <summary>
    /// Finds a tool set that matches keywords in the user message.
    /// </summary>
    /// <param name="userMessage">The user message to search for trigger keywords.</param>
    /// <returns>
    /// The first <see cref="IToolSet"/> with a matching keyword, or <see langword="null"/>
    /// if no matching tool set is found.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Keyword matching is case-insensitive and checks if any trigger keyword is contained
    /// within the user message. The first registered tool set with a matching keyword wins.
    /// </para>
    /// <para>
    /// If <paramref name="userMessage"/> is <see langword="null"/> or empty, no tool set
    /// will be matched.
    /// </para>
    /// </remarks>
    public IToolSet? FindByKeywords(string? userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return null;
        }

        foreach (IToolSet toolSet in this._toolSets)
        {
            foreach (string keyword in toolSet.TriggerKeywords)
            {
                if (userMessage.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return toolSet;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Gets all registered tool sets as a read-only collection.
    /// </summary>
    /// <returns>A read-only list of all registered tool sets.</returns>
    public IReadOnlyList<IToolSet> GetAll() => this._toolSets.AsReadOnly();

    /// <summary>
    /// Creates a default registry with all built-in tool sets registered.
    /// </summary>
    /// <returns>
    /// A new <see cref="ToolSetRegistry"/> instance with built-in tool sets
    /// (PlanToolSet, WeatherToolSet, QuizToolSet) registered.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This factory method creates a registry pre-configured with all the standard
    /// tool sets for AGUIWebChat mock testing. Tool sets are registered in priority
    /// order with more specific keyword matchers first.
    /// </para>
    /// <para>
    /// Currently registered tool sets:
    /// <list type="bullet">
    ///   <item><description>PlanToolSet - triggers on "plan", "create plan", "make a plan"</description></item>
    ///   <item><description>WeatherToolSet - triggers on "weather", "temperature", "forecast"</description></item>
    ///   <item><description>QuizToolSet - triggers on "quiz", "test me", "trivia"</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public static ToolSetRegistry CreateDefault()
    {
        return CreateDefault(options: null);
    }

    /// <summary>
    /// Creates a default registry with all built-in tool sets registered, configured with the specified options.
    /// </summary>
    /// <param name="options">The mock agent options containing delay configuration for tool sets. If <see langword="null"/>, default options are used.</param>
    /// <returns>
    /// A new <see cref="ToolSetRegistry"/> instance with built-in tool sets
    /// (PlanToolSet, WeatherToolSet, QuizToolSet) registered.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This factory method creates a registry pre-configured with all the standard
    /// tool sets for AGUIWebChat mock testing. Tool sets are registered in priority
    /// order with more specific keyword matchers first.
    /// </para>
    /// <para>
    /// The <paramref name="options"/> parameter allows configuring delay values:
    /// <list type="bullet">
    ///   <item><description><see cref="MockAgentOptions.PlanStepDelayMs"/> - delay between plan step updates</description></item>
    ///   <item><description><see cref="MockAgentOptions.WeatherLoadingDelayMs"/> - delay before weather result</description></item>
    ///   <item><description><see cref="MockAgentOptions.QuizLoadingDelayMs"/> - delay before quiz result</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public static ToolSetRegistry CreateDefault(MockAgentOptions? options)
    {
        // Register built-in tool sets in priority order
        // More specific matchers should be registered first
        return new ToolSetRegistry()
            .Register(new PlanToolSet(options))
            .Register(new WeatherToolSet(options))
            .Register(new QuizToolSet(options));
    }
}
