# AGUIWebChat Mock Clients Reference

Quick reference implementations for testing AGUIWebChat with mocked streaming responses.

## Basic Setup

### 1. Simple Mock with Plan Creation

```csharp
// File: Tests/AGUIWebChat.Tests.Mocks/MockChatClients.cs
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.AI;

namespace AGUIWebChat.Tests.Mocks;

/// <summary>
/// Mock chat client that simulates a planning agent with create_plan and update_plan_step tool calls.
/// </summary>
public sealed class PlanningMockChatClient : IChatClient
{
    public ChatClientMetadata Metadata { get; } = 
        new("MockPlanning", new Uri("https://test.example.com"), "mock-model");

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken);

        // Simulate LLM deciding to create a plan
        string createPlanCallId = "call_" + Guid.NewGuid().ToString("N");

        // 1. Function call to create_plan
        yield return new ChatResponseUpdate
        {
            Contents = [new FunctionCallContent(
                createPlanCallId,
                "create_plan",
                new Dictionary<string, object?>
                {
                    ["steps"] = new[] { "Gather requirements", "Design solution", "Implement" }
                })],
            Role = ChatRole.Assistant,
            MessageId = "msg_" + Guid.NewGuid().ToString("N")
        };

        await Task.Delay(50, cancellationToken);

        // 2. Function result (plan snapshot)
        string planJson = """
        {
            "steps": [
                {"description": "Gather requirements", "status": "pending"},
                {"description": "Design solution", "status": "pending"},
                {"description": "Implement", "status": "pending"}
            ]
        }
        """;

        yield return new ChatResponseUpdate
        {
            Contents = [new FunctionResultContent(createPlanCallId, planJson)],
            Role = ChatRole.Tool,
            MessageId = "msg_" + Guid.NewGuid().ToString("N")
        };

        // 3. Simulate working on step 0
        await Task.Delay(100, cancellationToken);

        string updateStep0CallId = "call_" + Guid.NewGuid().ToString("N");
        yield return new ChatResponseUpdate
        {
            Contents = [new FunctionCallContent(
                updateStep0CallId,
                "update_plan_step",
                new Dictionary<string, object?>
                {
                    ["index"] = 0,
                    ["status"] = "completed"
                })],
            Role = ChatRole.Assistant,
            MessageId = "msg_" + Guid.NewGuid().ToString("N")
        };

        await Task.Delay(50, cancellationToken);

        // 4. Update result (JSON Patch)
        string patchJson = """
        [
            {"op": "replace", "path": "/steps/0/status", "value": "completed"}
        ]
        """;

        yield return new ChatResponseUpdate
        {
            Contents = [new FunctionResultContent(updateStep0CallId, patchJson)],
            Role = ChatRole.Tool,
            MessageId = "msg_" + Guid.NewGuid().ToString("N")
        };

        // 5. Final text response
        await Task.Delay(20, cancellationToken);
        yield return new ChatResponseUpdate
        {
            Contents = [new TextContent("I've created a plan and completed the first step.")],
            Role = ChatRole.Assistant,
            MessageId = "msg_" + Guid.NewGuid().ToString("N")
        };
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Use streaming for testing");
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose() { }
}
```

### 2. Weather Tool Mock

```csharp
/// <summary>
/// Mock chat client that simulates calling the get_weather tool.
/// </summary>
public sealed class WeatherMockChatClient : IChatClient
{
    private readonly string _location;
    private readonly string _weatherResult;

    public WeatherMockChatClient(
        string location = "Seattle",
        string weatherResult = """{"temperature":"72F","condition":"Sunny"}""")
    {
        _location = location;
        _weatherResult = weatherResult;
    }

    public ChatClientMetadata Metadata { get; } = 
        new("MockWeather", new Uri("https://test.example.com"), "mock-model");

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken);

        string callId = "call_" + Guid.NewGuid().ToString("N");

        // 1. Tool call
        yield return new ChatResponseUpdate
        {
            Contents = [new FunctionCallContent(
                callId,
                "get_weather",
                new Dictionary<string, object?> { ["location"] = _location })],
            Role = ChatRole.Assistant,
            MessageId = "msg_" + Guid.NewGuid().ToString("N")
        };

        await Task.Delay(100, cancellationToken);

        // 2. Tool result
        yield return new ChatResponseUpdate
        {
            Contents = [new FunctionResultContent(callId, _weatherResult)],
            Role = ChatRole.Tool,
            MessageId = "msg_" + Guid.NewGuid().ToString("N")
        };

        await Task.Delay(20, cancellationToken);

        // 3. Text response
        yield return new ChatResponseUpdate
        {
            Contents = [new TextContent($"The weather in {_location} is 72F and sunny!")],
            Role = ChatRole.Assistant,
            MessageId = "msg_" + Guid.NewGuid().ToString("N")
        };
    }

    public Task<ChatResponse> GetResponseAsync(...) => 
        throw new NotImplementedException("Use streaming");
    
    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}
```

---

## Server Integration

### Modify Program.cs for Testing

```csharp
// File: Server/Program.cs
// Add conditional mock setup

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient().AddLogging();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Add(AgenticUISerializerContext.Default));
builder.Services.AddAGUI();

WebApplication app = builder.Build();

// === MOCK SETUP FOR TESTING ===
bool useMockClient = builder.Configuration.GetValue<bool>("USE_MOCK_CLIENT", false);
string? mockScenario = builder.Configuration["MOCK_SCENARIO"];

ChatClient chatClient;
if (useMockClient)
{
    IChatClient mockClient = mockScenario switch
    {
        "planning" => new PlanningMockChatClient(),
        "weather" => new WeatherMockChatClient(),
        _ => new PlanningMockChatClient() // Default
    };
    
    chatClient = (ChatClient)mockClient;
    Console.WriteLine($"[TEST MODE] Using mock client: {mockScenario ?? "planning"}");
}
else
{
    // Real Azure OpenAI or OpenAI setup
    string? endpoint = builder.Configuration["AZURE_OPENAI_ENDPOINT"];
    // ... (existing production setup)
}

// ... rest of Program.cs
```

---

## E2E Test Examples

### Playwright Test with Mock

```csharp
// File: Tests/AGUIWebChat.Tests.E2E/MockedChatTests.cs
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace AGUIWebChat.Tests.E2E;

[TestFixture]
public class MockedChatTests : PageTest
{
    private Process? _serverProcess;
    private Process? _clientProcess;

    [SetUp]
    public async Task SetupServerWithMock()
    {
        // Start server with mock client
        var serverStartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "run --project ../../../Server/AGUIWebChatServer.csproj",
            UseShellExecute = false,
            EnvironmentVariables =
            {
                ["USE_MOCK_CLIENT"] = "true",
                ["MOCK_SCENARIO"] = "planning"
            }
        };
        _serverProcess = Process.Start(serverStartInfo);
        await Task.Delay(3000); // Wait for server to start

        // Start client
        var clientStartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "run --project ../../../Client/AGUIWebChatClient.csproj"
        };
        _clientProcess = Process.Start(clientStartInfo);
        await Task.Delay(3000); // Wait for client to start
    }

    [TearDown]
    public void Cleanup()
    {
        _serverProcess?.Kill();
        _clientProcess?.Kill();
    }

    [Test]
    public async Task PlanningScenario_ShowsCreatePlanAndUpdate()
    {
        await Page.GotoAsync("http://localhost:5000");

        // Send message
        var input = Page.Locator("textarea[placeholder='Type your message...']");
        await input.FillAsync("Create a plan");
        await Page.Locator("button.send-button").ClickAsync();

        // Wait for user message
        await Expect(Page.Locator(".user-message").Last)
            .ToContainTextAsync("Create a plan");

        // ASSERT: Plan component should appear
        var planComponent = Page.Locator(".plan-component");
        await Expect(planComponent).ToBeVisibleAsync(new() { Timeout = 5000 });

        // ASSERT: Should show 3 steps
        var steps = planComponent.Locator(".plan-step");
        await Expect(steps).ToHaveCountAsync(3);

        // ASSERT: First step should show as pending initially
        await Expect(steps.First).ToContainTextAsync("Gather requirements");
        
        // Wait for step update (mock delays 100ms for step 0 completion)
        await Task.Delay(200);

        // ASSERT: First step should now be completed
        var firstStep = steps.First;
        await Expect(firstStep).ToHaveClassAsync(new Regex(".*completed.*"));

        // ASSERT: Final text message appears
        await Expect(Page.Locator(".assistant-message").Last)
            .ToContainTextAsync("completed the first step");
    }

    [Test]
    public async Task WeatherScenario_ShowsToolCallAndResult()
    {
        // Restart with weather mock
        _serverProcess?.Kill();
        var serverStartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "run --project ../../../Server/AGUIWebChatServer.csproj",
            EnvironmentVariables =
            {
                ["USE_MOCK_CLIENT"] = "true",
                ["MOCK_SCENARIO"] = "weather"
            }
        };
        _serverProcess = Process.Start(serverStartInfo);
        await Task.Delay(3000);

        await Page.GotoAsync("http://localhost:5000");

        var input = Page.Locator("textarea[placeholder='Type your message...']");
        await input.FillAsync("What's the weather?");
        await Page.Locator("button.send-button").ClickAsync();

        // ASSERT: Tool call indicator appears (if implemented in UI)
        // await Expect(Page.Locator(".tool-indicator")).ToContainTextAsync("get_weather");

        // ASSERT: Weather result appears in assistant message
        await Expect(Page.Locator(".assistant-message").Last)
            .ToContainTextAsync("72F and sunny", new() { Timeout = 5000 });
    }
}
```

---

## Unit Test Examples

### Testing AGUIProtocolService

```csharp
// File: Tests/AGUIWebChat.Tests.Unit/AGUIProtocolServiceTests.cs
using AGUIWebChat.Client.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AGUIWebChat.Tests.Unit;

public class AGUIProtocolServiceTests
{
    [Fact]
    public async Task StreamResponseAsync_WithPlanningMock_YieldsSnapshotAndDelta()
    {
        // Arrange
        var mockClient = new PlanningMockChatClient();
        var logger = new Mock<ILogger<AGUIProtocolService>>();
        var service = new AGUIProtocolService(mockClient, logger.Object);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Create a plan")
        };

        // Act
        List<AGUIUpdate> updates = [];
        await foreach (var update in service.StreamResponseAsync(messages))
        {
            updates.Add(update);
        }

        // Assert
        // Should have tool calls
        var toolCalls = updates.OfType<AGUIToolCall>().ToList();
        Assert.Equal(2, toolCalls.Count); // create_plan and update_plan_step
        Assert.Contains(toolCalls, tc => tc.Content.Name == "create_plan");
        Assert.Contains(toolCalls, tc => tc.Content.Name == "update_plan_step");

        // Should have tool results
        var toolResults = updates.OfType<AGUIToolResult>().ToList();
        Assert.Equal(2, toolResults.Count);

        // Should have state snapshot (from AgenticUIAgent transformation)
        // Note: This requires wrapping mockClient with AgenticUIAgent
        // var snapshots = updates.OfType<AGUIDataSnapshot>().ToList();
        // Assert.Single(snapshots);

        // Should have state delta
        // var deltas = updates.OfType<AGUIDataDelta>().ToList();
        // Assert.Single(deltas);

        // Should have final text
        var textParts = updates.OfType<AGUITextPart>().ToList();
        Assert.NotEmpty(textParts);
    }

    [Fact]
    public async Task StreamResponseAsync_WithWeatherMock_YieldsToolCallAndResult()
    {
        // Arrange
        var mockClient = new WeatherMockChatClient("Seattle");
        var logger = new Mock<ILogger<AGUIProtocolService>>();
        var service = new AGUIProtocolService(mockClient, logger.Object);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Weather in Seattle?")
        };

        // Act
        List<AGUIUpdate> updates = [];
        await foreach (var update in service.StreamResponseAsync(messages))
        {
            updates.Add(update);
        }

        // Assert
        var toolCalls = updates.OfType<AGUIToolCall>().ToList();
        Assert.Single(toolCalls);
        Assert.Equal("get_weather", toolCalls[0].Content.Name);

        var toolResults = updates.OfType<AGUIToolResult>().ToList();
        Assert.Single(toolResults);

        var textParts = updates.OfType<AGUITextPart>().ToList();
        Assert.NotEmpty(textParts);
        var fullText = string.Join("", textParts.Select(tp => tp.Text));
        Assert.Contains("72F", fullText);
    }
}
```

---

## Configuration Examples

### appsettings.Testing.json

```json
{
  "USE_MOCK_CLIENT": true,
  "MOCK_SCENARIO": "planning",
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Agents": "Debug"
    }
  }
}
```

### Launch with Mock via CLI

```bash
# Start server with planning mock
USE_MOCK_CLIENT=true MOCK_SCENARIO=planning \
  dotnet run --project Server/AGUIWebChatServer.csproj

# Start server with weather mock
USE_MOCK_CLIENT=true MOCK_SCENARIO=weather \
  dotnet run --project Server/AGUIWebChatServer.csproj
```

---

## Next Steps

1. **Create Mocks Library**:
   ```bash
   dotnet new classlib -n AGUIWebChat.Tests.Mocks -o Tests/AGUIWebChat.Tests.Mocks
   dotnet sln AGUIWebChat.slnx add Tests/AGUIWebChat.Tests.Mocks
   ```

2. **Add Mock Clients**: Implement `PlanningMockChatClient`, `WeatherMockChatClient`, etc.

3. **Update E2E Tests**: Use Playwright CLI skill to run automated UI tests with mocks.

4. **Document Scenarios**: Add more mock scenarios (multi-tool, error handling, etc.).

---

**References**:
- Main research doc: [/.docs/research/mocking-streaming-agui-responses.md](/.docs/research/mocking-streaming-agui-responses.md)
- TestHelpers: [/dotnet/tests/Microsoft.Agents.AI.Hosting.OpenAI.UnitTests/TestHelpers.cs](/dotnet/tests/Microsoft.Agents.AI.Hosting.OpenAI.UnitTests/TestHelpers.cs)
- Integration tests: [/dotnet/tests/Microsoft.Agents.AI.Hosting.AGUI.AspNetCore.IntegrationTests/ToolCallingTests.cs](/dotnet/tests/Microsoft.Agents.AI.Hosting.AGUI.AspNetCore.IntegrationTests/ToolCallingTests.cs)
