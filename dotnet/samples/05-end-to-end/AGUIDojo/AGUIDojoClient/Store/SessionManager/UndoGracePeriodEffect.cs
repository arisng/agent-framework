using System.Collections.Concurrent;
using Fluxor;

namespace AGUIDojoClient.Store.SessionManager;

/// <summary>
/// Manages expiration for session-scoped undo grace periods.
/// </summary>
public sealed class UndoGracePeriodEffect : IDisposable
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _pendingTimers = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Dispose()
    {
        foreach ((_, CancellationTokenSource cts) in _pendingTimers)
        {
            cts.Cancel();
            cts.Dispose();
        }

        _pendingTimers.Clear();
    }

    [EffectMethod]
    public Task OnStartUndoGracePeriod(SessionActions.StartUndoGracePeriodAction action, IDispatcher dispatcher)
    {
        CancellationTokenSource timer = new();
        ReplaceTimer(action.SessionId, timer);

        TimeSpan delay = action.ExpiresAt - DateTimeOffset.UtcNow;
        if (delay <= TimeSpan.Zero)
        {
            dispatcher.Dispatch(new SessionActions.ClearUndoGracePeriodAction(action.SessionId, action.CheckpointId));
            return Task.CompletedTask;
        }

        return WaitForExpiryAsync(action.SessionId, action.CheckpointId, delay, timer, dispatcher);
    }

    [EffectMethod]
    public Task OnClearUndoGracePeriod(SessionActions.ClearUndoGracePeriodAction action, IDispatcher _)
    {
        CancelTimer(action.SessionId);
        return Task.CompletedTask;
    }

    private async Task WaitForExpiryAsync(
        string sessionId,
        string checkpointId,
        TimeSpan delay,
        CancellationTokenSource timer,
        IDispatcher dispatcher)
    {
        try
        {
            await Task.Delay(delay, timer.Token);
            dispatcher.Dispatch(new SessionActions.ClearUndoGracePeriodAction(sessionId, checkpointId));
        }
        catch (TaskCanceledException)
        {
        }
        finally
        {
            RemoveTimer(sessionId, timer);
        }
    }

    private void ReplaceTimer(string sessionId, CancellationTokenSource replacement)
    {
        if (_pendingTimers.TryRemove(sessionId, out CancellationTokenSource? previous))
        {
            previous.Cancel();
            previous.Dispose();
        }

        _pendingTimers[sessionId] = replacement;
    }

    private void CancelTimer(string sessionId)
    {
        if (_pendingTimers.TryRemove(sessionId, out CancellationTokenSource? existing))
        {
            existing.Cancel();
            existing.Dispose();
        }
    }

    private void RemoveTimer(string sessionId, CancellationTokenSource timer)
    {
        if (_pendingTimers.TryGetValue(sessionId, out CancellationTokenSource? existing) &&
            ReferenceEquals(existing, timer) &&
            _pendingTimers.TryRemove(sessionId, out CancellationTokenSource? removed))
        {
            removed.Dispose();
        }
    }
}
