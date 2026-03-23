using AGUIDojoClient.Store.SessionManager;

namespace AGUIDojoClient.Tests.Store.SessionManager;

public class UndoGracePeriodReducerTests
{
    [Fact]
    public void OnStartUndoGracePeriod_SetsPendingUndoState()
    {
        // Arrange
        SessionManagerState state = SessionManagerState.CreateInitial();
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        SessionActions.StartUndoGracePeriodAction action = new(
            SessionManagerState.DefaultSessionId,
            "checkpoint-1",
            "Before plan update",
            "Plan updated",
            startedAt,
            startedAt.AddSeconds(5));

        // Act
        SessionManagerState updated = SessionReducers.OnStartUndoGracePeriod(state, action);

        // Assert
        PendingUndoState? pendingUndo = updated.Sessions[SessionManagerState.DefaultSessionId].State.PendingUndo;
        Assert.NotNull(pendingUndo);
        Assert.Equal("checkpoint-1", pendingUndo.CheckpointId);
        Assert.Equal("Before plan update", pendingUndo.CheckpointLabel);
        Assert.Equal("Plan updated", pendingUndo.Summary);
    }

    [Fact]
    public void OnClearUndoGracePeriod_WithDifferentCheckpointId_KeepsPendingUndo()
    {
        // Arrange
        SessionManagerState state = CreateStateWithPendingUndo("checkpoint-1");
        SessionActions.ClearUndoGracePeriodAction action = new(SessionManagerState.DefaultSessionId, "checkpoint-2");

        // Act
        SessionManagerState updated = SessionReducers.OnClearUndoGracePeriod(state, action);

        // Assert
        Assert.NotNull(updated.Sessions[SessionManagerState.DefaultSessionId].State.PendingUndo);
    }

    [Fact]
    public void OnClearUndoGracePeriod_WithMatchingCheckpointId_ClearsPendingUndo()
    {
        // Arrange
        SessionManagerState state = CreateStateWithPendingUndo("checkpoint-1");
        SessionActions.ClearUndoGracePeriodAction action = new(SessionManagerState.DefaultSessionId, "checkpoint-1");

        // Act
        SessionManagerState updated = SessionReducers.OnClearUndoGracePeriod(state, action);

        // Assert
        Assert.Null(updated.Sessions[SessionManagerState.DefaultSessionId].State.PendingUndo);
    }

    private static SessionManagerState CreateStateWithPendingUndo(string checkpointId)
    {
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        SessionManagerState state = SessionManagerState.CreateInitial();
        return SessionReducers.OnStartUndoGracePeriod(
            state,
            new SessionActions.StartUndoGracePeriodAction(
                SessionManagerState.DefaultSessionId,
                checkpointId,
                "Before recipe update",
                "Recipe updated",
                startedAt,
                startedAt.AddSeconds(5)));
    }
}
