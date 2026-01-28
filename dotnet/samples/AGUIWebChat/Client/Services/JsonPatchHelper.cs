using System.Text.Json;
using System.Text.Json.Nodes;

namespace AGUIWebChat.Client.Services;

/// <summary>
/// A lightweight, robust JSON Patch (RFC 6902) implementation based on System.Text.Json.Nodes.
/// Supports 'add', 'remove', and 'replace' operations on Objects and Arrays.
/// </summary>
public static class JsonPatchHelper
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Applies a JSON Patch document to an object.
    /// Operates by serializing to JsonNode, patching, and deserializing back.
    /// </summary>
    /// <typeparam name="T">The type of the model.</typeparam>
    /// <param name="original">The original object.</param>
    /// <param name="jsonPatch">The JSON Patch string (array of operations).</param>
    /// <returns>A new instance of T with patches applied.</returns>
    public static T? ApplyPatch<T>(T original, string jsonPatch)
    {
        if (original is null) return default;
        if (string.IsNullOrWhiteSpace(jsonPatch)) return original;

        JsonNode? rootNode;
        try
        {
            rootNode = JsonSerializer.SerializeToNode(original, _jsonOptions);
        }
        catch
        {
            return original;
        }

        if (rootNode is null) return original;

        JsonElement patchElement;
        try
        {
            using var doc = JsonDocument.Parse(jsonPatch);
            patchElement = doc.RootElement.Clone(); // Clone to keep it alive if needed, though we process immediately
        }
        catch
        {
            return original;
        }

        if (patchElement.ValueKind != JsonValueKind.Array) return original;

        foreach (var opElement in patchElement.EnumerateArray())
        {
            try
            {
                ApplyOperation(rootNode, opElement);
            }
            catch
            {
                // Ignore individual failed patch operations to maintain partial state if possible,
                // or just to avoid crashing the whole UI for one bad op.
            }
        }

        try
        {
            return rootNode.Deserialize<T>(_jsonOptions);
        }
        catch
        {
            return original;
        }
    }

    private static void ApplyOperation(JsonNode root, JsonElement opElement)
    {
        if (!opElement.TryGetProperty("op", out var opProp) ||
            !opElement.TryGetProperty("path", out var pathProp))
        {
            return;
        }

#pragma warning disable CA1308 // Normalize to lowercase for JSON Patch spec compliance
        string op = opProp.GetString()?.ToLowerInvariant() ?? "";
#pragma warning restore CA1308
        string path = pathProp.GetString() ?? "";

        // Get value if present
        JsonNode? valueNode = null;
        if (opElement.TryGetProperty("value", out var valueProp))
        {
            // We must parse explicitly to get a detached JsonNode we can attach elsewhere
            // Using GetRawText() is reliable for simple scalar or complex objects
            try
            {
                valueNode = JsonNode.Parse(valueProp.GetRawText());
            }
            catch { /* valueNode stays null */ }
        }

        var (parent, key, index) = NavigateToParent(root, path);
        if (parent is null) return;

        switch (op)
        {
            case "replace":
                ApplyReplace(parent, key, index, valueNode);
                break;
            case "add":
                ApplyAdd(parent, key, index, valueNode);
                break;
            case "remove":
                ApplyRemove(parent, key, index);
                break;
        }
    }

    private static void ApplyReplace(JsonNode parent, string key, int? index, JsonNode? value)
    {
        if (parent is JsonObject obj)
        {
            if (obj.ContainsKey(key))
            {
                obj[key] = value;
            }
        }
        else if (parent is JsonArray arr && index.HasValue)
        {
            // Replace at index
            int idx = index.Value;
            if (idx >= 0 && idx < arr.Count)
            {
                arr[idx] = value;
            }
        }
    }

    private static void ApplyAdd(JsonNode parent, string key, int? index, JsonNode? value)
    {
        if (parent is JsonObject obj)
        {
            // Add or Replace property
            obj[key] = value;
        }
        else if (parent is JsonArray arr)
        {
            int idx = index ?? -1;
            // RFC 6902: Use "-" to append
            if (key == "-")
            {
                arr.Add(value);
            }
            else if (idx >= 0 && idx <= arr.Count)
            {
                arr.Insert(idx, value);
            }
        }
    }

    private static void ApplyRemove(JsonNode parent, string key, int? index)
    {
        if (parent is JsonObject obj)
        {
            obj.Remove(key);
        }
        else if (parent is JsonArray arr && index.HasValue)
        {
            int idx = index.Value;
            if (idx >= 0 && idx < arr.Count)
            {
                arr.RemoveAt(idx);
            }
        }
    }

    /// <summary>
    /// Navigates the path to find the parent node and the segment info for the target.
    /// </summary>
    private static (JsonNode? parent, string key, int? index) NavigateToParent(JsonNode root, string path)
    {
        if (string.IsNullOrEmpty(path)) return (null, "", null);

        // Normalize path (RFC 6901 JSON Pointer)
        // Path must start with /
        if (!path.StartsWith('/')) return (null, "", null);

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return (null, "", null);

        JsonNode current = root;

        // Traverse up to the last segment
        for (int i = 0; i < segments.Length - 1; i++)
        {
            // Unescape ~0 -> ~ and ~1 -> / (RFC 6901)
            var segment = UnescapePointer(segments[i]);

            if (current is JsonObject obj)
            {
                if (obj.TryGetPropertyValue(segment, out var nextNode) && nextNode is not null)
                {
                    current = nextNode;
                }
                else
                {
                    return (null, "", null); // Path not found
                }
            }
            else if (current is JsonArray arr)
            {
                if (int.TryParse(segment, out int idx) && idx >= 0 && idx < arr.Count)
                {
                    var nextNode = arr[idx];
                    if (nextNode is not null)
                    {
                        current = nextNode;
                    }
                    else
                    {
                        return (null, "", null);
                    }
                }
                else
                {
                    return (null, "", null);
                }
            }
            else
            {
                return (null, "", null); // Scalar cannot have children
            }
        }

        string lastSegment = UnescapePointer(segments[^1]);
        int? index = null;
        if (current is JsonArray && (int.TryParse(lastSegment, out int idxResult)))
        {
            index = idxResult;
        }

        return (current, lastSegment, index);
    }

    private static string UnescapePointer(string segment)
    {
        return segment.Replace("~1", "/").Replace("~0", "~");
    }
}
