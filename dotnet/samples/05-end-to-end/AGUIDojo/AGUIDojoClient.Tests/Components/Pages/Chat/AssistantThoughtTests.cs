using AGUIDojoClient.Components.Pages.Chat;
using AGUIDojoClient.Services;
using AGUIDojoClient.Tests.Infrastructure;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace AGUIDojoClient.Tests.Components.Pages.Chat;

public sealed class AssistantThoughtTests : AGUIDojoClientComponentTestBase
{
    public AssistantThoughtTests()
    {
        Services.AddSingleton<IToolComponentRegistry, ToolComponentRegistry>();
    }

    [Fact]
    public void Toggle_Click_UpdatesExpandedState()
    {
        // Arrange
        IRenderedComponent<AssistantThought> component = this.Render<AssistantThought>(parameters => parameters
            .Add(thought => thought.ThoughtContents, Array.Empty<AIContent>())
            .Add(thought => thought.AllContents, Array.Empty<AIContent>())
            .Add(thought => thought.InProgress, false));

        // Starts collapsed once streaming is complete.
        Assert.Contains("collapsed", component.Find(".assistant-thought").ClassName, StringComparison.Ordinal);

        // Act
        component.Find(".thought-toggle").Click();

        // Assert
        component.WaitForAssertion(() =>
            Assert.Contains("expanded", component.Find(".assistant-thought").ClassName, StringComparison.Ordinal));
    }
}
