using AGUIDojoClient.Models;
using Microsoft.Extensions.AI;

namespace AGUIDojoClient.Tests.Models;

public sealed class ConversationTreeTests
{
    [Fact]
    public void AddMessage_ToEmptyTree_CreatesRootAndActiveBranch()
    {
        ConversationTree tree = new();

        tree = tree.AddMessage(new ChatMessage(ChatRole.User, "Hello"));

        Assert.NotNull(tree.RootId);
        Assert.Equal(tree.RootId, tree.ActiveLeafId);
        Assert.Single(tree.Nodes);
        Assert.Equal(["Hello"], tree.GetActiveBranchMessages().Select(message => message.Text));
    }

    [Fact]
    public void BranchAt_AndSwitchToLeaf_ReturnsMessagesForSelectedBranch()
    {
        ConversationTree tree = new();
        tree = tree.AddMessage(new ChatMessage(ChatRole.User, "Root"));
        string rootId = Assert.IsType<string>(tree.RootId);

        tree = tree.AddMessage(new ChatMessage(ChatRole.Assistant, "Original reply"));
        string originalLeafId = Assert.IsType<string>(tree.ActiveLeafId);

        tree = tree.BranchAt(rootId, new ChatMessage(ChatRole.Assistant, "Branch reply"));
        string branchLeafId = Assert.IsType<string>(tree.ActiveLeafId);

        var branchInfo = tree.GetBranchInfo(branchLeafId);

        Assert.NotNull(branchInfo);
        Assert.Equal(2, branchInfo.Value.Total);
        Assert.Equal(1, branchInfo.Value.CurrentIndex);
        Assert.Equal(branchLeafId, tree.FindLeafFromNode(branchLeafId));

        ConversationTree switchedTree = tree.SwitchToLeaf(originalLeafId);
        Assert.Equal(["Root", "Original reply"], switchedTree.GetActiveBranchMessages().Select(message => message.Text));
    }

    [Fact]
    public void TruncateActiveBranch_WithNonPositiveKeepCount_ClearsTree()
    {
        ConversationTree tree = new();
        tree = tree.AddMessage(new ChatMessage(ChatRole.User, "One"));
        tree = tree.AddMessage(new ChatMessage(ChatRole.Assistant, "Two"));

        ConversationTree truncated = tree.TruncateActiveBranch(0);

        Assert.Null(truncated.RootId);
        Assert.Null(truncated.ActiveLeafId);
        Assert.Empty(truncated.Nodes);
        Assert.Empty(truncated.GetActiveBranchMessages());
    }
}
