// Copyright (c) Microsoft. All rights reserved.

using AGUIDojoClient.Components.Governance;
using AGUIDojoClient.Models;
using AGUIDojoClient.Store.SessionManager;
using AGUIDojoClient.Tests.Infrastructure;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AGUIDojoClient.Tests.Components.Governance;

public sealed class AutonomySelectorTests : AGUIDojoClientComponentTestBase
{
    [Fact]
    public void Render_ShowsCurrentAutonomyLevelAsActive()
    {
        RegisterState(AutonomyLevel.AutoReview, out _);

        IRenderedComponent<AutonomySelector> component = Render<AutonomySelector>();

        var activeButton = component.FindAll("button.autonomy-btn--active").Single();
        Assert.Contains("Auto", activeButton.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void ClickingDifferentLevel_DispatchesSetAutonomyLevelAction()
    {
        Mock<IDispatcher> dispatcher = RegisterState(AutonomyLevel.Suggest, out _);
        IRenderedComponent<AutonomySelector> component = Render<AutonomySelector>();

        component.FindAll("button").Single(button => button.TextContent.Contains("Full", StringComparison.Ordinal)).Click();

        dispatcher.Verify(d => d.Dispatch(It.Is<SessionActions.SetAutonomyLevelAction>(action => action.Level == AutonomyLevel.FullAuto)), Times.Once);
    }

    [Fact]
    public void ClickingCurrentLevel_DoesNotDispatchAction()
    {
        Mock<IDispatcher> dispatcher = RegisterState(AutonomyLevel.FullAuto, out _);
        IRenderedComponent<AutonomySelector> component = Render<AutonomySelector>();

        component.FindAll("button").Single(button => button.TextContent.Contains("Full", StringComparison.Ordinal)).Click();

        dispatcher.Verify(d => d.Dispatch(It.IsAny<object>()), Times.Never);
    }

    private Mock<IDispatcher> RegisterState(AutonomyLevel autonomyLevel, out Mock<IState<SessionManagerState>> state)
    {
        state = new Mock<IState<SessionManagerState>>();
        state.SetupGet(store => store.Value).Returns(new SessionManagerState { AutonomyLevel = autonomyLevel });
        Mock<IDispatcher> dispatcher = new();

        Services.AddSingleton(state.Object);
        Services.AddSingleton(dispatcher.Object);

        return dispatcher;
    }
}
