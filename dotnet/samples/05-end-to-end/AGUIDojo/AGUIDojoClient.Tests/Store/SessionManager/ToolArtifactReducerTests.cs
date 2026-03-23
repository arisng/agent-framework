using AGUIDojoClient.Models;
using AGUIDojoClient.Store.SessionManager;

namespace AGUIDojoClient.Tests.Store.SessionManager;

public sealed class ToolArtifactReducerTests
{
    [Fact]
    public void OnUpsertToolArtifact_AddsArtifactAndActivatesWorkspaceTab()
    {
        SessionManagerState state = SessionManagerState.CreateInitial();
        ToolArtifactState artifact = CreateArtifact("chart-1");

        SessionManagerState updated = SessionReducers.OnUpsertToolArtifact(
            state,
            new SessionActions.UpsertToolArtifactAction(SessionManagerState.DefaultSessionId, artifact));

        SessionState session = updated.Sessions[SessionManagerState.DefaultSessionId].State;
        Assert.Single(session.ToolArtifacts);
        Assert.Equal("chart-1", session.ActiveToolArtifactId);
        Assert.Contains(ArtifactType.ToolResult, session.VisibleTabs);
        Assert.Equal(ArtifactType.ToolResult, session.ActiveArtifactType);
    }

    [Fact]
    public void OnRemoveToolArtifact_RemovesWorkspaceTabWhenLastArtifactIsRemoved()
    {
        SessionManagerState state = SessionReducers.OnUpsertToolArtifact(
            SessionManagerState.CreateInitial(),
            new SessionActions.UpsertToolArtifactAction(SessionManagerState.DefaultSessionId, CreateArtifact("chart-1")));

        SessionManagerState updated = SessionReducers.OnRemoveToolArtifact(
            state,
            new SessionActions.RemoveToolArtifactAction(SessionManagerState.DefaultSessionId, "chart-1"));

        SessionState session = updated.Sessions[SessionManagerState.DefaultSessionId].State;
        Assert.Empty(session.ToolArtifacts);
        Assert.DoesNotContain(ArtifactType.ToolResult, session.VisibleTabs);
        Assert.Equal(ArtifactType.None, session.ActiveArtifactType);
        Assert.Null(session.ActiveToolArtifactId);
    }

    [Fact]
    public void OnSetActiveToolArtifact_SelectsRequestedArtifact()
    {
        SessionManagerState state = SessionManagerState.CreateInitial();
        state = SessionReducers.OnUpsertToolArtifact(state, new SessionActions.UpsertToolArtifactAction(SessionManagerState.DefaultSessionId, CreateArtifact("chart-1"), MakeActive: false));
        state = SessionReducers.OnUpsertToolArtifact(state, new SessionActions.UpsertToolArtifactAction(SessionManagerState.DefaultSessionId, CreateArtifact("chart-2"), MakeActive: false));

        SessionManagerState updated = SessionReducers.OnSetActiveToolArtifact(
            state,
            new SessionActions.SetActiveToolArtifactAction(SessionManagerState.DefaultSessionId, "chart-2"));

        SessionState session = updated.Sessions[SessionManagerState.DefaultSessionId].State;
        Assert.Equal("chart-2", session.ActiveToolArtifactId);
        Assert.Equal(ArtifactType.ToolResult, session.ActiveArtifactType);
    }

    [Fact]
    public void OnClearMessages_RemovesPromotedArtifacts()
    {
        SessionManagerState state = SessionReducers.OnUpsertToolArtifact(
            SessionManagerState.CreateInitial(),
            new SessionActions.UpsertToolArtifactAction(SessionManagerState.DefaultSessionId, CreateArtifact("chart-1")));

        SessionManagerState updated = SessionReducers.OnClearMessages(
            state,
            new SessionActions.ClearMessagesAction(SessionManagerState.DefaultSessionId));

        SessionState session = updated.Sessions[SessionManagerState.DefaultSessionId].State;
        Assert.Empty(session.ToolArtifacts);
        Assert.Empty(session.VisibleTabs);
        Assert.False(session.HasInteractiveArtifact);
        Assert.Null(session.ActiveToolArtifactId);
        Assert.Equal(ArtifactType.None, session.ActiveArtifactType);
    }

    private static ToolArtifactState CreateArtifact(string artifactId) => new()
    {
        ArtifactId = artifactId,
        ToolName = "show_chart",
        Title = artifactId,
        ParsedData = new ChartResult
        {
            Title = artifactId,
            Labels = ["Jan"],
            Datasets = [new ChartDataset { Name = "Revenue", Values = [42] }],
        },
        CanMoveToContext = true,
    };
}
