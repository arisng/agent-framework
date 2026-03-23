using System.Text;
using System.Text.Json;
using AGUIDojoClient.Models;
using AGUIDojoClient.Shared;
using Microsoft.Extensions.AI;

namespace AGUIDojoClient.Helpers;

/// <summary>
/// Provides static helper methods for Chat.razor, including DataContent parsing,
/// plan/document/recipe state detection, content consolidation, and approval
/// response handling. Extracted from Chat.razor to reduce component size and
/// improve testability.
/// </summary>
public static class ChatHelpers
{
    public const string PlanSnapshotType = "plan_snapshot";
    public const string RecipeSnapshotType = "recipe_snapshot";
    public const string DocumentPreviewType = "document_preview";

    /// <summary>
    /// Determines if <see cref="DataContent"/> represents a Plan state snapshot (Agentic Generative UI).
    /// Plan snapshots have media type <c>application/json</c> and contain a <c>"steps"</c> array.
    /// </summary>
    /// <param name="dc">The <see cref="DataContent"/> to inspect.</param>
    /// <returns><see langword="true"/> if the content is a plan state snapshot; otherwise, <see langword="false"/>.</returns>
    public static bool IsPlanSnapshot(DataContent dc)
    {
        if (TryGetTypedEnvelopePayload(dc, out string? contentType, out JsonElement data))
        {
            return string.Equals(contentType, PlanSnapshotType, StringComparison.Ordinal) &&
                   data.ValueKind == JsonValueKind.Object &&
                   data.TryGetProperty("steps", out _);
        }

        return TryGetJsonRoot(dc, out JsonElement root) &&
               root.ValueKind == JsonValueKind.Object &&
               root.TryGetProperty("steps", out _);
    }

    /// <summary>
    /// Determines if <see cref="DataContent"/> represents a Plan state delta (JSON Patch).
    /// </summary>
    /// <param name="dc">The <see cref="DataContent"/> to inspect.</param>
    /// <returns><see langword="true"/> if the content is a plan state delta; otherwise, <see langword="false"/>.</returns>
    public static bool IsPlanStateDelta(DataContent dc)
    {
        return dc.MediaType == "application/json-patch+json" && dc.Data.Length > 0;
    }

    /// <summary>
    /// Attempts to parse a <see cref="DataContent"/> as a <see cref="Plan"/> snapshot.
    /// </summary>
    /// <param name="dc">The <see cref="DataContent"/> containing plan JSON data.</param>
    /// <returns>The parsed <see cref="Plan"/>, or <see langword="null"/> if parsing fails.</returns>
    public static Plan? TryParsePlanSnapshot(DataContent dc)
    {
        try
        {
            if (TryGetTypedEnvelopePayload(dc, out string? contentType, out JsonElement data))
            {
                if (!string.Equals(contentType, PlanSnapshotType, StringComparison.Ordinal))
                {
                    return null;
                }

                return JsonSerializer.Deserialize<Plan>(data.GetRawText(), JsonDefaults.Options);
            }

            string text = Encoding.UTF8.GetString(dc.Data.ToArray());
            return JsonSerializer.Deserialize<Plan>(text, JsonDefaults.Options);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Attempts to parse a <see cref="DataContent"/> as a list of <see cref="JsonPatchOperation"/>.
    /// </summary>
    /// <param name="dc">The <see cref="DataContent"/> containing JSON Patch data.</param>
    /// <returns>The parsed list of operations, or <see langword="null"/> if parsing fails.</returns>
    public static List<JsonPatchOperation>? TryParsePatchOperations(DataContent dc)
    {
        try
        {
            string text = Encoding.UTF8.GetString(dc.Data.ToArray());
            return JsonSerializer.Deserialize<List<JsonPatchOperation>>(text, JsonDefaults.Options);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Attempts to extract a <see cref="DocumentState"/> snapshot from <see cref="DataContent"/>
    /// (Predictive State Updates). Returns <see langword="true"/> if the content is a valid
    /// DocumentState snapshot (contains <c>"document"</c> property but not <c>"steps"</c> or <c>"ingredients"</c>).
    /// </summary>
    /// <param name="dc">The <see cref="DataContent"/> to inspect.</param>
    /// <param name="documentState">When this method returns, contains the parsed document state, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the content is a valid document state snapshot; otherwise, <see langword="false"/>.</returns>
    public static bool TryExtractDocumentSnapshot(DataContent dc, out DocumentState? documentState)
    {
        documentState = null;

        if (TryGetTypedEnvelopePayload(dc, out string? contentType, out JsonElement data))
        {
            if (!string.Equals(contentType, DocumentPreviewType, StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                documentState = JsonSerializer.Deserialize<DocumentState>(data.GetRawText(), JsonDefaults.Options);
                return documentState is not null;
            }
            catch
            {
                return false;
            }
        }

        if (!TryGetJsonRoot(dc, out JsonElement root))
        {
            return false;
        }

        // Verify this is a DocumentState (has "document" property but not "steps" or "ingredients")
        if (!root.TryGetProperty("document", out _))
        {
            return false;
        }

        // Exclude Plan snapshots (have "steps")
        if (root.TryGetProperty("steps", out _))
        {
            return false;
        }

        // Exclude Recipe snapshots (have "ingredients")
        if (root.TryGetProperty("ingredients", out _))
        {
            return false;
        }

        try
        {
            documentState = JsonSerializer.Deserialize<DocumentState>(root.GetRawText(), JsonDefaults.Options);
            return documentState is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Consolidates <see cref="DataContent"/> in a message by replacing previous instance of same category.
    /// Categories are determined by media type and content structure (is_state_snapshot, json-patch, etc.)
    /// This prevents accumulating many redundant state updates in the DOM.
    /// </summary>
    /// <param name="message">The <see cref="ChatMessage"/> to consolidate content in.</param>
    /// <param name="newContent">The new <see cref="DataContent"/> to add or replace.</param>
    public static void ConsolidateDataContent(ChatMessage message, DataContent newContent)
    {
        string category = GetDataContentCategory(newContent);

        // Find and replace existing DataContent of same category
        for (int i = 0; i < message.Contents.Count; i++)
        {
            if (message.Contents[i] is DataContent existing && GetDataContentCategory(existing) == category)
            {
                // Replace with newer content
                message.Contents[i] = newContent;
                return;
            }
        }

        // No existing content of this category, add new
        message.Contents.Add(newContent);
    }

    /// <summary>
    /// Determines the category of <see cref="DataContent"/> for consolidation purposes.
    /// Same category content will replace previous instances rather than accumulating.
    /// </summary>
    /// <param name="dc">The <see cref="DataContent"/> to categorize.</param>
    /// <returns>A string category identifier used for content consolidation.</returns>
    public static string GetDataContentCategory(DataContent dc)
    {
        // State deltas (JSON Patch) - consolidate per message
        if (dc.MediaType == "application/json-patch+json")
        {
            return "state-delta";
        }

        // State snapshots - consolidate per message
        if (dc.MediaType == "application/json" && dc.AdditionalProperties?.ContainsKey("is_state_snapshot") == true)
        {
            return "state-snapshot";
        }

        if (TryGetTypedEnvelopePayload(dc, out string? contentType, out _))
        {
            return contentType switch
            {
                PlanSnapshotType => "plan-snapshot",
                RecipeSnapshotType => "recipe-snapshot",
                DocumentPreviewType => "document-snapshot",
                _ => $"typed:{contentType}"
            };
        }

        // Try to detect content type from JSON structure
        if (TryGetJsonRoot(dc, out JsonElement root))
        {
            // Plan snapshots
            if (root.TryGetProperty("steps", out _))
            {
                return "plan-snapshot";
            }

            // Recipe snapshots
            if (root.TryGetProperty("ingredients", out _))
            {
                return "recipe-snapshot";
            }

            // Document state snapshots
            if (root.TryGetProperty("document", out _))
            {
                return "document-snapshot";
            }
        }

        // Generic DataContent - use media type as category.
        // MediaType is non-nullable on DataContent (always set in constructors).
        return dc.MediaType;
    }

    /// <summary>
    /// Attempts to parse a typed DataContent envelope and extract its discriminator and payload.
    /// </summary>
    public static bool TryGetTypedEnvelopePayload(DataContent dc, out string? contentType, out JsonElement data)
    {
        contentType = null;
        data = default;

        if (!TryGetJsonRoot(dc, out JsonElement root))
        {
            return false;
        }

        if (!root.TryGetProperty("$type", out JsonElement typeElement) ||
            typeElement.ValueKind != JsonValueKind.String ||
            !root.TryGetProperty("data", out JsonElement dataElement))
        {
            return false;
        }

        contentType = typeElement.GetString();
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        data = dataElement;
        return true;
    }

    private static bool TryGetJsonRoot(DataContent dc, out JsonElement root)
    {
        root = default;

        if (dc.MediaType != "application/json" || dc.Data.Length == 0)
        {
            return false;
        }

        try
        {
            string text = Encoding.UTF8.GetString(dc.Data.ToArray());
            using JsonDocument doc = JsonDocument.Parse(text);
            root = doc.RootElement.Clone();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Determines if a <see cref="FunctionResultContent"/> represents a rejection response from HITL approval.
    /// Rejection responses have <c>{"approved": false}</c> in their Result.
    /// </summary>
    /// <param name="functionResult">The <see cref="FunctionResultContent"/> to inspect.</param>
    /// <returns><see langword="true"/> if the result represents a rejection; otherwise, <see langword="false"/>.</returns>
    public static bool IsRejectionResponse(FunctionResultContent functionResult)
    {
        try
        {
            if (functionResult.Result is JsonElement jsonElement
                && jsonElement.TryGetProperty("approved", out JsonElement approvedProp))
            {
                return !approvedProp.GetBoolean();
            }
        }
        catch
        {
            // Failed to parse result - treat as not a rejection
        }

        return false;
    }

    /// <summary>
    /// Converts a nullable <see cref="JsonElement"/> (from PendingApproval) to an
    /// <see cref="IDictionary{TKey,TValue}"/> for the ApprovalQueue component.
    /// </summary>
    /// <param name="arguments">The nullable <see cref="JsonElement"/> containing approval arguments.</param>
    /// <returns>A dictionary of argument key-value pairs, or <see langword="null"/> if the input is not a JSON object.</returns>
    public static IDictionary<string, object?>? ConvertApprovalArguments(JsonElement? arguments)
    {
        if (arguments is null || arguments.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var dict = new Dictionary<string, object?>();
        foreach (var property in arguments.Value.EnumerateObject())
        {
            dict[property.Name] = property.Value;
        }

        return dict;
    }
}
