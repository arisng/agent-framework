// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace AGUIDojoClient.Services;

/// <summary>
/// Service for handling function approval requests and responses in the Human-in-the-Loop workflow.
/// Transforms between FunctionCallContent with name="request_approval" and a displayable approval format.
/// </summary>
public sealed class ApprovalHandler : IApprovalHandler
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Dictionary<string, PendingApproval> _pendingApprovals = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalHandler"/> class.
    /// </summary>
    public ApprovalHandler()
    {
        this._jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    /// <inheritdoc />
    public bool HasPendingApprovals => this._pendingApprovals.Count > 0;

    /// <inheritdoc />
    public IReadOnlyCollection<PendingApproval> PendingApprovals => this._pendingApprovals.Values.ToList().AsReadOnly();

    /// <inheritdoc />
    public bool TryExtractApprovalRequest(FunctionCallContent functionCall, out PendingApproval? approval)
    {
        approval = null;

        if (!string.Equals(functionCall.Name, "request_approval", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            if (functionCall.Arguments?.TryGetValue("request", out var reqObj) == true)
            {
                ApprovalRequest? request = null;

                if (reqObj is JsonElement jsonElement)
                {
                    request = jsonElement.Deserialize<ApprovalRequest>(this._jsonOptions);
                }
                else if (reqObj is ApprovalRequest req)
                {
                    request = req;
                }

                if (request is not null)
                {
                    approval = new PendingApproval
                    {
                        ApprovalId = request.ApprovalId,
                        FunctionName = request.FunctionName,
                        FunctionArguments = request.FunctionArguments,
                        Message = request.Message ?? $"Approve execution of '{request.FunctionName}'?",
                        OriginalCallId = functionCall.CallId
                    };

                    this._pendingApprovals[approval.ApprovalId] = approval;
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            // Failed to parse approval request
        }

        return false;
    }

    /// <inheritdoc />
    public FunctionResultContent CreateApprovalResponse(string approvalId, bool approved)
    {
        if (!this._pendingApprovals.TryGetValue(approvalId, out var pendingApproval))
        {
            throw new InvalidOperationException($"No pending approval found with ID: {approvalId}");
        }

        var response = new ApprovalResponse
        {
            ApprovalId = approvalId,
            Approved = approved
        };

        // Remove from pending after creating response
        this._pendingApprovals.Remove(approvalId);

        return new FunctionResultContent(
            callId: pendingApproval.OriginalCallId,
            result: JsonSerializer.SerializeToElement(response, this._jsonOptions));
    }

    /// <inheritdoc />
    public void ClearPendingApprovals()
    {
        this._pendingApprovals.Clear();
    }
}

/// <summary>
/// Interface for the approval handler service.
/// </summary>
public interface IApprovalHandler
{
    /// <summary>
    /// Gets a value indicating whether there are pending approvals.
    /// </summary>
    bool HasPendingApprovals { get; }

    /// <summary>
    /// Gets the collection of pending approvals.
    /// </summary>
    IReadOnlyCollection<PendingApproval> PendingApprovals { get; }

    /// <summary>
    /// Attempts to extract an approval request from a FunctionCallContent.
    /// </summary>
    /// <param name="functionCall">The function call content to check.</param>
    /// <param name="approval">The extracted approval request, if found.</param>
    /// <returns>True if an approval request was found and extracted; otherwise, false.</returns>
    bool TryExtractApprovalRequest(FunctionCallContent functionCall, out PendingApproval? approval);

    /// <summary>
    /// Creates an approval response to send back to the server.
    /// </summary>
    /// <param name="approvalId">The ID of the approval request.</param>
    /// <param name="approved">True if approved; false if rejected.</param>
    /// <returns>A FunctionResultContent containing the approval response.</returns>
    FunctionResultContent CreateApprovalResponse(string approvalId, bool approved);

    /// <summary>
    /// Clears all pending approvals.
    /// </summary>
    void ClearPendingApprovals();
}

/// <summary>
/// Represents a pending approval request that awaits user decision.
/// </summary>
public sealed class PendingApproval
{
    /// <summary>
    /// Gets or sets the unique identifier for this approval request.
    /// </summary>
    public required string ApprovalId { get; set; }

    /// <summary>
    /// Gets or sets the name of the function requiring approval.
    /// </summary>
    public required string FunctionName { get; set; }

    /// <summary>
    /// Gets or sets the arguments passed to the function.
    /// </summary>
    public JsonElement? FunctionArguments { get; set; }

    /// <summary>
    /// Gets or sets the message to display to the user.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Gets or sets the original call ID for creating the response.
    /// </summary>
    public required string OriginalCallId { get; set; }
}

/// <summary>
/// Represents an approval request from the server.
/// This class is used for JSON deserialization only.
/// </summary>
#pragma warning disable CA1812 // Internal class is instantiated via JSON deserialization
internal sealed class ApprovalRequest
{
    [JsonPropertyName("approval_id")]
    public required string ApprovalId { get; init; }

    [JsonPropertyName("function_name")]
    public required string FunctionName { get; init; }

    [JsonPropertyName("function_arguments")]
    public JsonElement? FunctionArguments { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}
#pragma warning restore CA1812

/// <summary>
/// Represents an approval response to send back to the server.
/// </summary>
internal sealed class ApprovalResponse
{
    [JsonPropertyName("approval_id")]
    public required string ApprovalId { get; init; }

    [JsonPropertyName("approved")]
    public required bool Approved { get; init; }
}
