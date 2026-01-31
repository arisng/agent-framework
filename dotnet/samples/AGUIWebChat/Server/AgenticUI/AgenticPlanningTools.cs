// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;

namespace AGUIWebChatServer.AgenticUI;

internal static class AgenticPlanningTools
{
    [Description("Create a plan with multiple steps.")]
    public static Plan CreatePlan([Description("List of step descriptions to create the plan.")] List<string> steps)
    {
        return new Plan
        {
            Steps = [.. steps.Select(step => new Step { Description = step, Status = StepStatus.Pending })]
        };
    }

    [Description("Update a step in the plan with new description or status.")]
    public static async Task<List<JsonPatchOperation>> UpdatePlanStepAsync(
        [Description("The index of the step to update.")] int index,
        [Description("The new description for the step (optional).")] string? description = null,
        [Description("The new status for the step (optional).")] StepStatus? status = null)
    {
        List<JsonPatchOperation> changes = [];

        if (description is not null)
        {
            changes.Add(new JsonPatchOperation
            {
                Op = "replace",
                Path = $"/steps/{index}/description",
                Value = description
            });
        }

        if (status.HasValue)
        {
            string statusValue = status.Value == StepStatus.Pending ? "pending" : "completed";
            changes.Add(new JsonPatchOperation
            {
                Op = "replace",
                Path = $"/steps/{index}/status",
                Value = statusValue
            });
        }

        await Task.Delay(1000);

        return changes;
    }
}
