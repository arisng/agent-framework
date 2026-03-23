using AGUIDojoClient.Components.Governance;
using AGUIDojoClient.Store.SessionManager;
using AGUIDojoClient.Tests.Infrastructure;

namespace AGUIDojoClient.Tests.Components.Governance;

public class UndoGracePeriodToastTests : AGUIDojoClientComponentTestBase
{
    [Fact]
    public void Render_WithPendingUndo_ShowsSummaryCheckpointAndActions()
    {
        // Arrange
        PendingUndoState pendingUndo = CreatePendingUndo();

        // Act
        IRenderedComponent<UndoGracePeriodToast> component = this.Render<UndoGracePeriodToast>(parameters => parameters
            .Add(toast => toast.PendingUndo, pendingUndo));

        // Assert
        Assert.Contains("Recipe workspace initialized", component.Markup);
        Assert.Contains("Before recipe bootstrap", component.Markup);
        component.Find("[data-testid='undo-grace-toast']");
        Assert.Equal(2, component.FindAll("button").Count);
    }

    [Fact]
    public void UndoButton_Click_InvokesUndoCallbackWithCheckpointId()
    {
        // Arrange
        string? invokedCheckpointId = null;
        PendingUndoState pendingUndo = CreatePendingUndo();
        IRenderedComponent<UndoGracePeriodToast> component = this.Render<UndoGracePeriodToast>(parameters => parameters
            .Add(toast => toast.PendingUndo, pendingUndo)
            .Add(toast => toast.OnUndo, EventCallback.Factory.Create<string>(this, checkpointId => invokedCheckpointId = checkpointId)));

        // Act
        component.Find(".undo-grace-toast__undo-btn").Click();

        // Assert
        Assert.Equal(pendingUndo.CheckpointId, invokedCheckpointId);
    }

    private static PendingUndoState CreatePendingUndo()
    {
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        return new PendingUndoState
        {
            CheckpointId = "checkpoint-1",
            CheckpointLabel = "Before recipe bootstrap",
            Summary = "Recipe workspace initialized",
            StartedAt = startedAt,
            ExpiresAt = startedAt.AddSeconds(5),
        };
    }
}
