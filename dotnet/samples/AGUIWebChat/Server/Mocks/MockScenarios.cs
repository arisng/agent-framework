// Copyright (c) Microsoft. All rights reserved.

namespace AGUIWebChat.Server.Mocks;

/// <summary>
/// Defines the type of mock scenario response.
/// </summary>
public enum MockScenarioType
{
    /// <summary>
    /// A scenario that emits only text content, streamed token-by-token.
    /// </summary>
    TextResponse,

    /// <summary>
    /// A scenario that emits a tool call (FunctionCallContent) to trigger tool execution.
    /// </summary>
    ToolCall,

    /// <summary>
    /// A scenario that combines text response followed by a tool call.
    /// </summary>
    Mixed
}

/// <summary>
/// Represents a mock scenario that defines what the MockAIAgent should respond with.
/// </summary>
/// <remarks>
/// <para>
/// Each scenario can be one of three types:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="MockScenarioType.TextResponse"/>: Returns streamed text content</description></item>
/// <item><description><see cref="MockScenarioType.ToolCall"/>: Returns a function call that triggers tool execution</description></item>
/// <item><description><see cref="MockScenarioType.Mixed"/>: Returns text followed by a tool call</description></item>
/// </list>
/// <para>
/// For <see cref="MockScenarioType.ToolCall"/> and <see cref="MockScenarioType.Mixed"/> scenarios,
/// the <see cref="ToolCallName"/> and <see cref="ToolCallArguments"/> properties must be set.
/// </para>
/// </remarks>
public sealed class MockScenario
{
    /// <summary>
    /// Gets or sets the type of this scenario.
    /// </summary>
    /// <value>The scenario type. Defaults to <see cref="MockScenarioType.TextResponse"/>.</value>
    public MockScenarioType Type { get; set; } = MockScenarioType.TextResponse;

    /// <summary>
    /// Gets or sets the text content to stream for <see cref="MockScenarioType.TextResponse"/>
    /// and <see cref="MockScenarioType.Mixed"/> scenarios.
    /// </summary>
    /// <value>The text content to stream. May be <see langword="null"/> for pure <see cref="MockScenarioType.ToolCall"/> scenarios.</value>
    public string? TextContent { get; set; }

    /// <summary>
    /// Gets or sets the name of the tool to call for <see cref="MockScenarioType.ToolCall"/>
    /// and <see cref="MockScenarioType.Mixed"/> scenarios.
    /// </summary>
    /// <value>
    /// The tool name (e.g., "create_plan", "update_plan_step", "get_weather").
    /// May be <see langword="null"/> for pure <see cref="MockScenarioType.TextResponse"/> scenarios.
    /// </value>
    public string? ToolCallName { get; set; }

    /// <summary>
    /// Gets or sets the JSON-serialized arguments for the tool call.
    /// </summary>
    /// <value>
    /// The tool call arguments as a JSON string (e.g., "{\"steps\":[\"Step 1\",\"Step 2\"]}").
    /// May be <see langword="null"/> for pure <see cref="MockScenarioType.TextResponse"/> scenarios.
    /// </value>
    public string? ToolCallArguments { get; set; }
}

/// <summary>
/// Provides predefined mock scenarios for AGUIWebChat testing.
/// </summary>
/// <remarks>
/// <para>
/// These scenarios are designed to test the AG-UI protocol features:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="TextResponse"/>: Tests basic text streaming</description></item>
/// <item><description><see cref="CreatePlan"/>: Tests tool call with state initialization via AgenticPlanningTools.CreatePlan</description></item>
/// <item><description><see cref="UpdatePlanStep"/>: Tests tool call with JSON Patch state updates via AgenticPlanningTools.UpdatePlanStepAsync</description></item>
/// </list>
/// </remarks>
public static class PredefinedScenarios
{
    /// <summary>
    /// A simple text response scenario that streams a helpful assistant message.
    /// </summary>
    /// <remarks>
    /// Use this scenario to test basic text streaming functionality without tool involvement.
    /// The text is streamed word-by-word based on the configured streaming delay.
    /// </remarks>
    public static MockScenario TextResponse { get; } = new()
    {
        Type = MockScenarioType.TextResponse,
        TextContent = "Hello! I'm a mock AI assistant. I can help you test the AGUIWebChat application. " +
                      "This is a simulated response that streams word by word to demonstrate the AG-UI protocol's " +
                      "text streaming capabilities. Let me know if you'd like to test tool calls or state management features!"
    };

    /// <summary>
    /// A tool call scenario that invokes the create_plan tool to initialize agentic state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This scenario emits a <c>FunctionCallContent</c> with the tool name "create_plan"
    /// and arguments matching the <c>AgenticPlanningTools.CreatePlan</c> signature.
    /// </para>
    /// <para>
    /// The arguments include a list of step descriptions that will be converted into
    /// a <c>Plan</c> object with pending steps, demonstrating state initialization.
    /// </para>
    /// </remarks>
    public static MockScenario CreatePlan { get; } = new()
    {
        Type = MockScenarioType.ToolCall,
        ToolCallName = "create_plan",
        ToolCallArguments = """{"steps":["Research the topic thoroughly","Create an outline for the content","Write the first draft","Review and revise the draft","Finalize and publish"]}"""
    };

    /// <summary>
    /// A tool call scenario that invokes the update_plan_step tool to demonstrate JSON Patch state updates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This scenario emits a <c>FunctionCallContent</c> with the tool name "update_plan_step"
    /// and arguments matching the <c>AgenticPlanningTools.UpdatePlanStepAsync</c> signature.
    /// </para>
    /// <para>
    /// The arguments update the first step (index 0) to completed status, demonstrating
    /// how state changes are communicated via JSON Patch operations.
    /// </para>
    /// </remarks>
    public static MockScenario UpdatePlanStep { get; } = new()
    {
        Type = MockScenarioType.ToolCall,
        ToolCallName = "update_plan_step",
        ToolCallArguments = """{"index":0,"status":"completed"}"""
    };

    /// <summary>
    /// A mixed scenario that first explains what it will do, then creates a plan.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This scenario demonstrates the <see cref="MockScenarioType.Mixed"/> type,
    /// which streams text content first before emitting a tool call.
    /// </para>
    /// <para>
    /// Use this scenario to test the full AG-UI workflow where the agent
    /// explains its reasoning before taking action.
    /// </para>
    /// </remarks>
    public static MockScenario MixedCreatePlan { get; } = new()
    {
        Type = MockScenarioType.Mixed,
        TextContent = "I'll help you create a plan for this project. Let me organize the steps for you.",
        ToolCallName = "create_plan",
        ToolCallArguments = """{"steps":["Define project requirements","Design the solution architecture","Implement core functionality","Write unit tests","Perform integration testing","Deploy to production"]}"""
    };

    /// <summary>
    /// A mixed scenario that acknowledges progress before updating a step.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This scenario demonstrates updating state while providing user feedback.
    /// The agent first acknowledges the completion, then updates the plan step.
    /// </para>
    /// </remarks>
    public static MockScenario MixedUpdateStep { get; } = new()
    {
        Type = MockScenarioType.Mixed,
        TextContent = "Great progress! I'm marking this step as completed. Let me update the plan for you.",
        ToolCallName = "update_plan_step",
        ToolCallArguments = """{"index":0,"status":"completed"}"""
    };
}
