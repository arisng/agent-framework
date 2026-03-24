using AGUIDojoClient.Store.SessionManager;

namespace AGUIDojoClient.Tests.Store.SessionManager;

public sealed class SessionPreferredModelReducerTests
{
    [Fact]
    public void OnCreateSession_StoresPreferredModelOnMetadata()
    {
        SessionManagerState state = SessionManagerState.CreateInitial();

        SessionManagerState updated = SessionReducers.OnCreateSession(
            state,
            new SessionActions.CreateSessionAction(
                "session-model",
                PreferredModelId: "gpt-4.1"));

        SessionEntry entry = updated.Sessions["session-model"];

        Assert.Equal("gpt-4.1", entry.Metadata.PreferredModelId);
    }

    [Fact]
    public void OnSetPreferredModel_UpdatesSessionMetadata()
    {
        SessionManagerState state = SessionManagerState.CreateInitial();
        state = SessionReducers.OnCreateSession(state, new SessionActions.CreateSessionAction("session-model"));

        SessionManagerState updated = SessionReducers.OnSetPreferredModel(
            state,
            new SessionActions.SetPreferredModelAction("session-model", "gpt-4o-mini"));

        Assert.Equal("gpt-4o-mini", updated.Sessions["session-model"].Metadata.PreferredModelId);
    }
}
