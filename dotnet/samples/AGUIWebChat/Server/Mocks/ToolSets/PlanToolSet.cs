// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics;
using AGUIWebChatServer.AgenticUI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;

namespace AGUIWebChat.Server.Mocks.ToolSets;

/// <summary>
/// Implements the plan tool set that simulates realistic LLM planning behavior by emitting
/// a <c>create_plan</c> tool call followed by multiple <c>update_plan_step</c> calls.
/// </summary>
/// <remarks>
/// <para>
/// This tool set simulates the complete planning workflow that a real LLM would perform:
/// </para>
/// <list type="number">
/// <item><description>Emit <c>create_plan</c> with a list of step descriptions</description></item>
/// <item><description>For each step, emit <c>update_plan_step</c> with status "completed"</description></item>
/// <item><description>Emit a summary text message upon completion</description></item>
/// </list>
/// <para>
/// The sequence demonstrates how an LLM iteratively processes a plan, marking each step
/// as completed in order. Configurable delays between steps simulate realistic execution time
/// and allow the UI to visually display step-by-step progress (0→1→2→...→N).
/// </para>
/// </remarks>
public sealed class PlanToolSet : IToolSet
{
    /// <summary>
    /// Default steps to use when no specific plan is requested.
    /// </summary>
    private static readonly string[] DefaultSteps =
    [
        "Research the topic thoroughly",
        "Create an outline for the content",
        "Write the first draft",
        "Review and revise the draft",
        "Finalize and publish"
    ];

    /// <summary>
    /// Summary text to emit after all plan steps are completed.
    /// </summary>
    private const string CompletionSummaryText = "All plan steps have been completed successfully! Your plan is now ready for execution.";

    /// <summary>
    /// The configuration options for this tool set.
    /// </summary>
    private readonly MockAgentOptions _options;

    /// <summary>
    /// The step descriptions to use in the plan.
    /// </summary>
    private readonly string[] _steps;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlanToolSet"/> class with default configuration.
    /// </summary>
    public PlanToolSet()
        : this(options: null, steps: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PlanToolSet"/> class with custom configuration.
    /// </summary>
    /// <param name="options">The mock agent options containing delay configuration. If <see langword="null"/>, default options are used.</param>
    /// <param name="steps">The step descriptions to include in the plan. If <see langword="null"/>, uses default steps.</param>
    public PlanToolSet(MockAgentOptions? options = null, string[]? steps = null)
    {
        _options = options ?? new MockAgentOptions();
        _steps = steps ?? DefaultSteps;
    }

    /// <summary>
    /// Gets the unique name of this tool set.
    /// </summary>
    /// <value>Returns "PlanTools".</value>
    public string Name => "PlanTools";

    /// <summary>
    /// Gets the keywords or phrases that trigger this tool set.
    /// </summary>
    /// <value>A read-only list containing "plan", "create plan", and "make a plan".</value>
    public IReadOnlyList<string> TriggerKeywords { get; } = new[] { "plan", "create plan", "make a plan" };

    /// <summary>
    /// Executes the plan tool sequence, emitting <c>create_plan</c> followed by <c>update_plan_step</c> for each step.
    /// </summary>
    /// <param name="context">The execution context containing response metadata and helper methods.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>
    /// An asynchronous enumerable of <see cref="AgentResponseUpdate"/> instances containing
    /// the <c>create_plan</c> tool call followed by <c>update_plan_step</c> tool calls for each step,
    /// and a summary text message upon completion.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The sequence simulates how a real LLM would process a planning request:
    /// </para>
    /// <list type="number">
    /// <item><description>First, emit <c>create_plan</c> FunctionCallContent with step descriptions</description></item>
    /// <item><description>Emit the FunctionResultContent with the plan snapshot JSON</description></item>
    /// <item><description>Then, for each step (index 0 to N-1), emit <c>update_plan_step</c> FunctionCallContent</description></item>
    /// <item><description>Emit the FunctionResultContent with JSON Patch delta for each step</description></item>
    /// <item><description>Finally, emit a summary text message confirming completion</description></item>
    /// </list>
    /// <para>
    /// The FunctionResultContent is essential for proper UI rendering. The client uses the result content
    /// to render appropriate UI components (plan panels with steps).
    /// </para>
    /// <para>
    /// Configurable delays (via <see cref="MockAgentOptions.PlanStepDelayMs"/>) between steps allow the UI
    /// to visually display step-by-step progress (0→1→2→...→N).
    /// </para>
    /// </remarks>
    public async IAsyncEnumerable<AgentResponseUpdate> ExecuteSequenceAsync(
        MockSequenceContext context,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        ILogger<PlanToolSet> logger = context.CreateLogger<PlanToolSet>();
        Stopwatch overallStopwatch = Stopwatch.StartNew();

        logger.LogInformation("[PlanToolSet] Starting plan sequence with {StepCount} steps, delay: {DelayMs}ms between steps", _steps.Length, _options.PlanStepDelayMs);

        // Step 1: Emit create_plan tool call with steps
        // Arguments format matches AgenticPlanningTools.CreatePlan(List<string> steps) signature
        Dictionary<string, object?> createPlanArgs = new()
        {
            ["steps"] = _steps.ToList()
        };

        Stopwatch toolStopwatch = Stopwatch.StartNew();
        logger.LogInformation("[PlanToolSet] Emitting tool call: create_plan with {StepCount} steps", _steps.Length);

        AgentResponseUpdate createPlanCallUpdate = context.CreateToolCallUpdate("create_plan", createPlanArgs);
        string createPlanCallId = ((Microsoft.Extensions.AI.FunctionCallContent)createPlanCallUpdate.Contents[0]).CallId;
        yield return createPlanCallUpdate;

        // Emit FunctionResultContent with plan snapshot JSON (all steps start as pending)
        // Format: {"steps":[{"description":"...","status":"pending"},...]}
        // Note: Status must be lowercase to match real AgenticPlanningTools behavior
        List<Dictionary<string, object>> planStepsSnapshot = _steps
            .Select(step => new Dictionary<string, object>
            {
                ["description"] = step,
                ["status"] = "pending"  // lowercase to match real tool pattern
            })
            .ToList();

        Dictionary<string, object> planSnapshot = new()
        {
            ["steps"] = planStepsSnapshot
        };

        logger.LogInformation("[PlanToolSet] Emitting plan snapshot with {StepCount} steps (all pending)", _steps.Length);
        yield return context.CreateToolResultUpdate(createPlanCallId, "create_plan", planSnapshot);

        toolStopwatch.Stop();
        logger.LogInformation("[PlanToolSet] Completed tool call: create_plan in {ElapsedMs}ms", toolStopwatch.ElapsedMilliseconds);

        // Wait for create_plan to be processed (simulated execution time)
        if (_options.PlanStepDelayMs > 0)
        {
            await Task.Delay(_options.PlanStepDelayMs, cancellationToken).ConfigureAwait(false);
        }

        // Step 2: For each step, emit update_plan_step tool call to mark as completed
        // Arguments format matches AgenticPlanningTools.UpdatePlanStepAsync(int index, string? description, StepStatus? status)
        for (int i = 0; i < _steps.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Dictionary<string, object?> updateStepArgs = new()
            {
                ["index"] = i,
                ["status"] = "completed"
            };

            toolStopwatch.Restart();
            logger.LogInformation("[PlanToolSet] Emitting tool call: update_plan_step (step {StepIndex}/{TotalSteps})", i + 1, _steps.Length);

            AgentResponseUpdate updateStepCallUpdate = context.CreateToolCallUpdate("update_plan_step", updateStepArgs);
            string updateStepCallId = ((Microsoft.Extensions.AI.FunctionCallContent)updateStepCallUpdate.Contents[0]).CallId;
            yield return updateStepCallUpdate;

            // Emit FunctionResultContent with JSON Patch delta using JsonPatchOperation class
            // This ensures proper serialization with [JsonPropertyName] attributes matching real tool
            // Format: [{"op":"replace","path":"/steps/{i}/status","value":"completed"}]
            List<JsonPatchOperation> jsonPatchDelta =
            [
                new JsonPatchOperation
                {
                    Op = "replace",
                    Path = $"/steps/{i}/status",
                    Value = "completed"
                }
            ];

            logger.LogDebug("[PlanToolSet] Emitting JSON Patch delta for step {StepIndex}: path=/steps/{StepIndex}/status, value=completed", i, i);
            yield return context.CreateToolResultUpdate(updateStepCallId, "update_plan_step", jsonPatchDelta);

            toolStopwatch.Stop();
            logger.LogInformation("[PlanToolSet] Completed tool call: update_plan_step (step {StepIndex}/{TotalSteps}) in {ElapsedMs}ms", i + 1, _steps.Length, toolStopwatch.ElapsedMilliseconds);

            // Delay between steps to allow UI to render incremental progress (0→1→2→...→N)
            if (_options.PlanStepDelayMs > 0 && i < _steps.Length - 1)
            {
                logger.LogDebug("[PlanToolSet] Waiting {DelayMs}ms before next step update", _options.PlanStepDelayMs);
                await Task.Delay(_options.PlanStepDelayMs, cancellationToken).ConfigureAwait(false);
            }
        }

        // Step 3: Emit summary text after all steps are completed
        logger.LogInformation("[PlanToolSet] All {StepCount} steps completed, emitting summary text", _steps.Length);

        await foreach (AgentResponseUpdate textUpdate in context.StreamTextAsync(CompletionSummaryText, cancellationToken).ConfigureAwait(false))
        {
            yield return textUpdate;
        }

        overallStopwatch.Stop();
        logger.LogInformation("[PlanToolSet] Plan sequence completed in {TotalElapsedMs}ms", overallStopwatch.ElapsedMilliseconds);
    }
}
