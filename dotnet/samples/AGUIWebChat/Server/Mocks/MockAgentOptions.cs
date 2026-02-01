// Copyright (c) Microsoft. All rights reserved.

namespace AGUIWebChat.Server.Mocks;

/// <summary>
/// Configuration options for the mock AI agent.
/// </summary>
/// <remarks>
/// Use this class to configure how the mock agent simulates LLM behavior for testing
/// AG-UI features without requiring actual OpenAI/Azure OpenAI dependencies.
/// </remarks>
public sealed class MockAgentOptions
{
    /// <summary>
    /// Gets or sets the name of the mock agent.
    /// </summary>
    /// <value>The agent name. Defaults to "MockAgent".</value>
    public string Name { get; set; } = "MockAgent";

    /// <summary>
    /// Gets or sets the description of the mock agent.
    /// </summary>
    /// <value>The agent description. Defaults to "Mock agent for testing AG-UI features without LLM dependencies".</value>
    public string Description { get; set; } = "Mock agent for testing AG-UI features without LLM dependencies";

    /// <summary>
    /// Gets or sets the delay in milliseconds between streaming tokens.
    /// </summary>
    /// <value>The streaming delay in milliseconds. Defaults to 50ms to simulate realistic token streaming.</value>
    public int StreamingDelayMs { get; set; } = 50;

    /// <summary>
    /// Gets or sets the delay in milliseconds between plan step updates.
    /// </summary>
    /// <remarks>
    /// This delay is applied between each <c>update_plan_step</c> call in the PlanToolSet
    /// to allow the UI to visually display step-by-step progress (0→1→2→...→N).
    /// </remarks>
    /// <value>The plan step delay in milliseconds. Defaults to 750ms for visible incremental updates.</value>
    public int PlanStepDelayMs { get; set; } = 750;

    /// <summary>
    /// Gets or sets the delay in milliseconds before emitting weather result.
    /// </summary>
    /// <remarks>
    /// This delay is applied after the <c>get_weather</c> tool call but before emitting
    /// the <see cref="Microsoft.Extensions.AI.FunctionResultContent"/>, allowing the UI
    /// to display a loading state before the weather card appears.
    /// </remarks>
    /// <value>The weather loading delay in milliseconds. Defaults to 1500ms for realistic loading experience.</value>
    public int WeatherLoadingDelayMs { get; set; } = 1500;

    /// <summary>
    /// Gets or sets the delay in milliseconds before emitting quiz result.
    /// </summary>
    /// <remarks>
    /// This delay is applied after the <c>generate_quiz</c> tool call but before emitting
    /// the <see cref="Microsoft.Extensions.AI.FunctionResultContent"/>, allowing the UI
    /// to display a loading spinner before the quiz component renders.
    /// </remarks>
    /// <value>The quiz loading delay in milliseconds. Defaults to 1500ms for realistic loading experience.</value>
    public int QuizLoadingDelayMs { get; set; } = 1500;

    /// <summary>
    /// Gets or sets the default scenario to use when no keyword trigger matches.
    /// </summary>
    /// <value>The default scenario to execute when user input doesn't match any configured triggers. May be <see langword="null"/> if no default is desired.</value>
    public MockScenario? DefaultScenario { get; set; }

    /// <summary>
    /// Gets or sets a dictionary mapping keyword triggers to scenarios.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Keys are case-insensitive keywords that, when found in user input, will trigger the associated scenario.
    /// For example, a key of "plan" could trigger a CreatePlan scenario.
    /// </para>
    /// <para>
    /// When multiple keywords match the user input, the first match in enumeration order is used.
    /// </para>
    /// </remarks>
    /// <value>A dictionary mapping trigger keywords to their associated scenarios. Defaults to an empty dictionary.</value>
    public Dictionary<string, MockScenario> ScenariosByTrigger { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
