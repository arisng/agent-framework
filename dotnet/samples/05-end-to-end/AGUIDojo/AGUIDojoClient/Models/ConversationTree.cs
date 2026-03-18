using System.Collections.Immutable;
using Microsoft.Extensions.AI;

namespace AGUIDojoClient.Models;

/// <summary>
/// A single node in the conversation tree. Each node holds one ChatMessage
/// and tracks its parent and children (branches).
/// </summary>
public sealed record ConversationNode
{
    public required string Id { get; init; }
    public string? ParentId { get; init; }
    public required ChatMessage Message { get; init; }
    public ImmutableList<string> ChildIds { get; init; } = ImmutableList<string>.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// A conversation tree supporting branching. Messages are stored as nodes in a DAG.
/// The active branch is determined by walking from root to the active leaf.
/// </summary>
public sealed record ConversationTree
{
    public ImmutableDictionary<string, ConversationNode> Nodes { get; init; }
        = ImmutableDictionary<string, ConversationNode>.Empty;

    /// <summary>Root node ID (first message in the conversation).</summary>
    public string? RootId { get; init; }

    /// <summary>Currently active leaf node ID (last message on the active branch).</summary>
    public string? ActiveLeafId { get; init; }

    /// <summary>
    /// Get the active branch as a flat list of messages by walking from active leaf up to root,
    /// then reversing to get root → leaf order.
    /// </summary>
    public IReadOnlyList<ChatMessage> GetActiveBranchMessages()
    {
        if (RootId is null || ActiveLeafId is null || Nodes.IsEmpty)
        {
            return Array.Empty<ChatMessage>();
        }

        var path = new List<ChatMessage>();
        string? currentId = ActiveLeafId;
        while (currentId is not null && Nodes.TryGetValue(currentId, out var node))
        {
            path.Add(node.Message);
            currentId = node.ParentId;
        }

        path.Reverse();
        return path;
    }

    /// <summary>
    /// Add a message as a child of the current active leaf (or as root if tree is empty).
    /// Returns the updated tree with the new node as the active leaf.
    /// </summary>
    public ConversationTree AddMessage(ChatMessage message)
    {
        string nodeId = Guid.NewGuid().ToString("N")[..12];
        var node = new ConversationNode
        {
            Id = nodeId,
            ParentId = ActiveLeafId,
            Message = message,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var nodes = Nodes.Add(nodeId, node);

        if (ActiveLeafId is not null && nodes.TryGetValue(ActiveLeafId, out var parent))
        {
            nodes = nodes.SetItem(ActiveLeafId, parent with { ChildIds = parent.ChildIds.Add(nodeId) });
        }

        return this with
        {
            Nodes = nodes,
            RootId = RootId ?? nodeId,
            ActiveLeafId = nodeId,
        };
    }

    /// <summary>
    /// Create a branch at a specific node by adding a new child message.
    /// Sets the new child as the active leaf.
    /// </summary>
    public ConversationTree BranchAt(string parentNodeId, ChatMessage message)
    {
        if (!Nodes.TryGetValue(parentNodeId, out var parentNode))
        {
            return this;
        }

        string nodeId = Guid.NewGuid().ToString("N")[..12];
        var newNode = new ConversationNode
        {
            Id = nodeId,
            ParentId = parentNodeId,
            Message = message,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var nodes = Nodes
            .Add(nodeId, newNode)
            .SetItem(parentNodeId, parentNode with { ChildIds = parentNode.ChildIds.Add(nodeId) });

        return this with
        {
            Nodes = nodes,
            ActiveLeafId = nodeId,
        };
    }

    /// <summary>
    /// Switch to a different branch by setting a new active leaf.
    /// </summary>
    public ConversationTree SwitchToLeaf(string leafId)
    {
        if (!Nodes.ContainsKey(leafId))
        {
            return this;
        }

        return this with { ActiveLeafId = leafId };
    }

    /// <summary>
    /// Get sibling information for a node — how many siblings it has and its index.
    /// Returns (currentIndex, totalSiblings, siblingIds) or null if no siblings.
    /// </summary>
    public (int CurrentIndex, int Total, ImmutableList<string> SiblingIds)? GetBranchInfo(string nodeId)
    {
        if (!Nodes.TryGetValue(nodeId, out var node) || node.ParentId is null)
        {
            return null;
        }

        if (!Nodes.TryGetValue(node.ParentId, out var parent))
        {
            return null;
        }

        if (parent.ChildIds.Count <= 1)
        {
            return null;
        }

        int index = parent.ChildIds.IndexOf(nodeId);
        return (index, parent.ChildIds.Count, parent.ChildIds);
    }

    /// <summary>
    /// Find the leaf node of a branch starting from a given node.
    /// Follows the first child at each level until reaching a leaf.
    /// </summary>
    public string FindLeafFromNode(string nodeId)
    {
        string current = nodeId;
        while (Nodes.TryGetValue(current, out var node) && !node.ChildIds.IsEmpty)
        {
            current = node.ChildIds[0];
        }

        return current;
    }

    /// <summary>
    /// Get the node ID for a message on the active branch by its index (0-based).
    /// </summary>
    public string? GetNodeIdAtIndex(int index)
    {
        if (RootId is null || ActiveLeafId is null)
        {
            return null;
        }

        var path = new List<string>();
        string? walkId = ActiveLeafId;
        while (walkId is not null && Nodes.TryGetValue(walkId, out var node))
        {
            path.Add(walkId);
            walkId = node.ParentId;
        }

        path.Reverse();

        return index >= 0 && index < path.Count ? path[index] : null;
    }

    /// <summary>
    /// Get all node IDs on the active branch in root → leaf order.
    /// </summary>
    public IReadOnlyList<string> GetActiveBranchNodeIds()
    {
        if (RootId is null || ActiveLeafId is null || Nodes.IsEmpty)
        {
            return Array.Empty<string>();
        }

        var path = new List<string>();
        string? walkId = ActiveLeafId;
        while (walkId is not null && Nodes.TryGetValue(walkId, out var node))
        {
            path.Add(walkId);
            walkId = node.ParentId;
        }

        path.Reverse();
        return path;
    }

    /// <summary>
    /// Truncate the active branch to keep only the first <paramref name="keepCount"/> messages.
    /// Sets the active leaf to the node at <c>keepCount - 1</c>.
    /// </summary>
    public ConversationTree TruncateActiveBranch(int keepCount)
    {
        if (RootId is null || ActiveLeafId is null)
        {
            return this;
        }

        var path = GetActiveBranchNodeIds();
        if (keepCount >= path.Count)
        {
            return this;
        }

        if (keepCount <= 0)
        {
            return new ConversationTree();
        }

        return this with { ActiveLeafId = path[keepCount - 1] };
    }
}
