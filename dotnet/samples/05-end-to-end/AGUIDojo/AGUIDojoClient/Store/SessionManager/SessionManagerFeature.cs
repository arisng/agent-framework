using Fluxor;

namespace AGUIDojoClient.Store.SessionManager;

/// <summary>
/// Fluxor feature that registers the session manager store.
/// </summary>
public sealed class SessionManagerFeature : Feature<SessionManagerState>
{
    /// <inheritdoc />
    public override string GetName() => "SessionManager";

    /// <inheritdoc />
    protected override SessionManagerState GetInitialState() => SessionManagerState.CreateInitial();
}
