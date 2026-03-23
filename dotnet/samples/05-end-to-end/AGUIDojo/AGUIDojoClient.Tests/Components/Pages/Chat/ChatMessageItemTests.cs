using System.Collections.Immutable;
using AGUIDojoClient.Components.Pages.Chat;
using AGUIDojoClient.Models;
using AGUIDojoClient.Services;
using AGUIDojoClient.Store.SessionManager;
using AGUIDojoClient.Tests.Infrastructure;
using Fluxor;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AGUIDojoClient.Tests.Components.Pages.Chat;

public sealed class ChatMessageItemTests : AGUIDojoClientComponentTestBase
{
    public ChatMessageItemTests()
    {
        Services.AddSingleton<IMarkdownService, MarkdownService>();
        Services.AddSingleton<IToolComponentRegistry, ToolComponentRegistry>();
        Services.AddSingleton<IObservabilityService>(new NoopObservabilityService());
    }

    [Fact]
    public void UserMessage_RendersEditButtonOutsideBubble()
    {
        // Arrange
        ChatMessage message = new(ChatRole.User, "layout repro");

        // Act
        IRenderedComponent<ChatMessageItem> component = this.Render<ChatMessageItem>(parameters => parameters
            .Add(item => item.Message, message)
            .Add(item => item.MessageIndex, 0));

        // Assert
        component.Find(".user-message-row > .edit-message-btn");
        Assert.Empty(component.FindAll(".user-message .edit-message-btn"));
        Assert.Contains("layout repro", component.Find(".user-message-body").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void AssistantChart_RendersMoveToCanvasButton()
    {
        RegisterSessionState(CreateSessionManagerState(), out _);
        ChatMessage message = CreateChartMessage();

        IRenderedComponent<ChatMessageItem> component = this.Render<ChatMessageItem>(parameters => parameters
            .Add(item => item.Message, message)
            .Add(item => item.MessageIndex, 0));

        Assert.Contains("Move to canvas", component.Markup, StringComparison.Ordinal);
        component.Find(".visual-tool-result__button");
    }

    [Fact]
    public void AssistantChart_WhenPromotedToCanvas_RendersPlaceholderAndReturnAction()
    {
        SessionManagerState state = CreateSessionManagerState(
            new ToolArtifactState
            {
                ArtifactId = "chart-1",
                ToolName = "show_chart",
                Title = "Revenue Trend",
                ParsedData = new ChartResult
                {
                    Title = "Revenue Trend",
                    Labels = ["Jan"],
                    Datasets = [new ChartDataset { Name = "Revenue", Values = [42] }],
                },
                CanMoveToContext = true,
            });

        Mock<IDispatcher> dispatcher = RegisterSessionState(state, out _);
        ChatMessage message = CreateChartMessage();

        IRenderedComponent<ChatMessageItem> component = this.Render<ChatMessageItem>(parameters => parameters
            .Add(item => item.Message, message)
            .Add(item => item.MessageIndex, 0));

        Assert.Contains("Focus canvas", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Show inline", component.Markup, StringComparison.Ordinal);

        component.FindAll("button")
            .Single(button => button.TextContent.Contains("Show inline", StringComparison.Ordinal))
            .Click();

        dispatcher.Verify(d => d.Dispatch(It.Is<SessionActions.RemoveToolArtifactAction>(action => action.ArtifactId == "chart-1")), Times.Once);
    }

    private Mock<IDispatcher> RegisterSessionState(SessionManagerState state, out Mock<IState<SessionManagerState>> store)
    {
        store = new Mock<IState<SessionManagerState>>();
        store.SetupGet(sessionStore => sessionStore.Value).Returns(state);
        Mock<IDispatcher> dispatcher = new();

        Services.AddSingleton(store.Object);
        Services.AddSingleton(dispatcher.Object);

        return dispatcher;
    }

    private static SessionManagerState CreateSessionManagerState(params ToolArtifactState[] toolArtifacts)
    {
        SessionEntry session = SessionManagerState.CreateSessionEntry("session-1");
        SessionState sessionState = session.State with
        {
            ToolArtifacts = toolArtifacts.ToImmutableList(),
            ActiveToolArtifactId = toolArtifacts.LastOrDefault()?.ArtifactId,
            VisibleTabs = toolArtifacts.Length > 0
                ? session.State.VisibleTabs.Add(ArtifactType.ToolResult)
                : session.State.VisibleTabs,
            HasInteractiveArtifact = toolArtifacts.Length > 0,
            ActiveArtifactType = toolArtifacts.Length > 0 ? ArtifactType.ToolResult : ArtifactType.None,
        };

        return new SessionManagerState
        {
            ActiveSessionId = "session-1",
            Sessions = new Dictionary<string, SessionEntry>
            {
                ["session-1"] = session with { State = sessionState }
            }.ToImmutableDictionary(),
            AutonomyLevel = AutonomyLevel.Suggest,
        };
    }

    private static ChatMessage CreateChartMessage()
    {
        List<string> labels = ["Jan"];
        List<int> values = [42];
        var datasets = new List<object>
        {
            new
            {
                name = "Revenue",
                values
            }
        };
        ChatMessage message = new(ChatRole.Assistant, string.Empty)
        {
            AuthorName = "Planner",
        };

        message.Contents.Add(new FunctionCallContent(
            callId: "chart-1",
            name: "show_chart",
            arguments: new Dictionary<string, object?>()));

        message.Contents.Add(new FunctionResultContent(
            callId: "chart-1",
            result: System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                title = "Revenue Trend",
                chartType = "bar",
                labels,
                datasets
            })));

        return message;
    }

    private sealed class NoopObservabilityService : IObservabilityService
    {
        public ReasoningStep StartToolCall(string callId, string toolName, IDictionary<string, object?>? arguments)
            => new()
            {
                Id = callId,
                ToolName = toolName,
                Arguments = arguments,
                Status = ReasoningStepStatus.Running,
                StartTime = DateTime.UtcNow,
            };

        public void CompleteToolCall(string callId, object? result)
        {
        }

        public void FailToolCall(string callId, string error)
        {
        }

        public IReadOnlyList<ReasoningStep> GetSteps() => [];

        public ReasoningStep? GetActiveStep() => null;
    }
}
