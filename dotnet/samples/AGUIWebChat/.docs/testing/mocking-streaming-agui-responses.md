# Mocking Streaming AG-UI Responses with Function Calls

**Date:** 2026-01-29  
**Purpose:** Guide for creating mock streaming responses from AI agents with concrete function calls to registered tools, enabling automated testing of chat client frontends and AG-UI protocol handling.

---

## Overview

This document provides patterns and examples for mocking `IChatClient` streaming responses that include:
- Text content streaming
- Function call invocations (`FunctionCallContent`)
- Function results (`FunctionResultContent`)
- AG-UI state updates (`DataContent` with snapshots and deltas)
- Mixed content types in a single stream

These mocks enable **fully automated UI testing** without requiring actual LLM calls, focusing on visualization and rendering in Blazor chat clients.

---

## Key Concepts

### AG-UI Protocol Elements

1. **Text Streaming**: Progressive text chunks in `TextContent`
2. **Function Calls**: `FunctionCallContent` with tool name, callId, and arguments
3. **Function Results**: `FunctionResultContent` with callId and result data
4. **State Snapshots**: `DataContent` with `application/json` MediaType (full state)
5. **State Deltas**: `DataContent` with `application/json-patch+json` MediaType (JSON Patch operations)

### Streaming Update Structure

```csharp
ChatResponseUpdate {
    Contents: List<AIContent>,  // TextContent, FunctionCallContent, FunctionResultContent, DataContent
    Role: ChatRole.Assistant,
    MessageId: string,
    ResponseId: string,
    // ... other metadata
}
```

---

## Mocking Approaches

### 1. Simple Text Streaming Mock

**Use Case**: Basic text rendering without tools.

```csharp
internal sealed class SimpleMockChatClient : IChatClient
{
    private readonly string _responseText;

    public SimpleMockChatClient(string responseText = "Test response")
    {
        _responseText = responseText;
    }

    public ChatClientMetadata Metadata { get; } = 
        new("Test", new Uri("https://test.example.com"), "test-model");

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);

        // Split response into words to simulate streaming
        string[] words = _responseText.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            string content = i < words.Length - 1 ? words[i] + " " : words[i];
            yield return new ChatResponseUpdate
            {
                Contents = [new TextContent(content)],
                Role = ChatRole.Assistant,
                MessageId = "msg_" + Guid.NewGuid().ToString("N")
            };
        }
    }

    public Task<ChatResponse> GetResponseAsync(...)
    {
        // Implementation omitted for brevity
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose() { }
}
```

**Usage in Tests**:
```csharp
var mockClient = new SimpleMockChatClient("Hello from the assistant");
var agent = mockClient.AsIChatClient().AsAIAgent();
await foreach (var update in agent.RunStreamingAsync([new ChatMessage(ChatRole.User, "Hi")]))
{
    // Verify UI renders text progressively
}
```

---

### 2. Function Call Mock (Single Tool)

**Use Case**: Test UI rendering of tool calls and results.

```csharp
internal sealed class FunctionCallMockChatClient : IChatClient
{
    private readonly string _functionName;
    private readonly Dictionary<string, object?> _arguments;
    private readonly string _functionResult;

    public FunctionCallMockChatClient(
        string functionName = "get_weather",
        string argumentsJson = """{"location":"Seattle"}""",
        string functionResult = """{"temperature":"72F","condition":"Sunny"}""")
    {
        _functionName = functionName;
        _arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(argumentsJson) ?? [];
        _functionResult = functionResult;
    }

    public ChatClientMetadata Metadata { get; } = 
        new("Test", new Uri("https://test.example.com"), "test-model");

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);
        string callId = "call_" + Guid.NewGuid().ToString("N");

        // 1. Emit function call
        yield return new ChatResponseUpdate
        {
            Contents = [new FunctionCallContent(callId, _functionName, _arguments)],
            Role = ChatRole.Assistant,
            MessageId = "msg_toolcall"
        };

        // Simulate function execution delay
        await Task.Delay(50, cancellationToken);

        // 2. Emit function result
        yield return new ChatResponseUpdate
        {
            Contents = [new FunctionResultContent(callId, _functionResult)],
            Role = ChatRole.Tool,
            MessageId = "msg_toolresult"
        };

        // 3. Emit final text response
        yield return new ChatResponseUpdate
        {
            Contents = [new TextContent("The weather in Seattle is 72F and sunny.")],
            Role = ChatRole.Assistant,
            MessageId = "msg_final"
        };
    }

    public Task<ChatResponse> GetResponseAsync(...) { /* Omitted */ }
    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}
```

**Usage in Tests**:
```csharp
var mockClient = new FunctionCallMockChatClient(
    functionName: "get_weather",
    argumentsJson: """{"location":"Seattle"}""",
    functionResult: """{"temperature":"72F"}"""
);

var agent = mockClient.AsIChatClient().AsAIAgent(
    chatOptions: new ChatOptions
    {
        Tools = [
            AIFunctionFactory.Create(
                (string location) => $"{{\"temperature\":\"72F\",\"location\":\"{location}\"}}",
                name: "get_weather",
                description: "Get weather for a location")
        ]
    }
);

List<AgentResponseUpdate> updates = [];
await foreach (var update in agent.RunStreamingAsync([new ChatMessage(ChatRole.User, "Weather?")]))
{
    updates.Add(update);
}

// Assert: UI should show function call, then result, then final response
var functionCalls = updates.SelectMany(u => u.Contents.OfType<FunctionCallContent>()).ToList();
Assert.Single(functionCalls);
Assert.Equal("get_weather", functionCalls[0].Name);

var functionResults = updates.SelectMany(u => u.Contents.OfType<FunctionResultContent>()).ToList();
Assert.Single(functionResults);
```

---

### 3. AG-UI State Streaming Mock (Plan Updates)

**Use Case**: Test AG-UI protocol with state snapshots and JSON Patch deltas.

```csharp
internal sealed class AGUIPlanMockChatClient : IChatClient
{
    public ChatClientMetadata Metadata { get; } = 
        new("Test", new Uri("https://test.example.com"), "test-model");

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);
        string createPlanCallId = "call_create_plan";
        string updateStep0CallId = "call_update_0";

        // 1. Emit create_plan function call
        yield return new ChatResponseUpdate
        {
            Contents = [new FunctionCallContent(
                createPlanCallId,
                "create_plan",
                new Dictionary<string, object?>
                {
                    ["steps"] = new[] { "Research topic", "Write draft", "Review" }
                })],
            Role = ChatRole.Assistant,
            MessageId = "msg_create"
        };

        await Task.Delay(10, cancellationToken);

        // 2. Emit create_plan result (snapshot)
        string planSnapshot = """
        {
            "steps": [
                {"description": "Research topic", "status": "pending"},
                {"description": "Write draft", "status": "pending"},
                {"description": "Review", "status": "pending"}
            ]
        }
        """;

        yield return new ChatResponseUpdate
        {
            Contents = [new FunctionResultContent(createPlanCallId, planSnapshot)],
            Role = ChatRole.Tool,
            MessageId = "msg_create_result"
        };

        // 3. Emit state snapshot (AG-UI protocol)
        byte[] snapshotBytes = Encoding.UTF8.GetBytes(planSnapshot);
        yield return new ChatResponseUpdate
        {
            Contents = [new DataContent(snapshotBytes, "application/json")],
            Role = ChatRole.System,
            MessageId = "state_snapshot"
        };

        await Task.Delay(100, cancellationToken);

        // 4. Emit update_plan_step function call
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
            MessageId = "msg_update"
        };

        await Task.Delay(10, cancellationToken);

        // 5. Emit update_plan_step result (JSON Patch delta)
        string patchDelta = """
        [
            {"op": "replace", "path": "/steps/0/status", "value": "completed"}
        ]
        """;

        yield return new ChatResponseUpdate
        {
            Contents = [new FunctionResultContent(updateStep0CallId, patchDelta)],
            Role = ChatRole.Tool,
            MessageId = "msg_update_result"
        };

        // 6. Emit state delta (AG-UI protocol)
        byte[] deltaBytes = Encoding.UTF8.GetBytes(patchDelta);
        yield return new ChatResponseUpdate
        {
            Contents = [new DataContent(deltaBytes, "application/json-patch+json")],
            Role = ChatRole.System,
            MessageId = "state_delta"
        };

        // 7. Final text response
        yield return new ChatResponseUpdate
        {
            Contents = [new TextContent("Plan created and first step completed.")],
            Role = ChatRole.Assistant,
            MessageId = "msg_final"
        };
    }

    public Task<ChatResponse> GetResponseAsync(...) { /* Omitted */ }
    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}
```

**Wrapped with AgenticUIAgent for Protocol Handling**:

```csharp
// In AGUIWebChat pattern, wrap the mock with AgenticUIAgent
var mockChatClient = new AGUIPlanMockChatClient();
var baseAgent = mockChatClient.AsIChatClient().AsAIAgent(
    chatOptions: new ChatOptions
    {
        Tools = [
            AIFunctionFactory.Create(CreatePlan, "create_plan", "Create a plan"),
            AIFunctionFactory.Create(UpdatePlanStepAsync, "update_plan_step", "Update step")
        ]
    }
);

var jsonOptions = new JsonSerializerOptions();
var agenticUIAgent = new AgenticUIAgent(baseAgent, jsonOptions);

// Test streaming with AG-UI protocol
await foreach (var update in agenticUIAgent.RunStreamingAsync([new ChatMessage(ChatRole.User, "Create plan")]))
{
    // Verify:
    // 1. FunctionCallContent for create_plan
    // 2. FunctionResultContent with snapshot
    // 3. DataContent with application/json (snapshot)
    // 4. FunctionCallContent for update_plan_step
    // 5. FunctionResultContent with delta
    // 6. DataContent with application/json-patch+json (delta)
}
```

---

### 4. Multi-Tool Scenario Mock

**Use Case**: Test UI with multiple concurrent tool calls.

```csharp
internal sealed class MultiToolMockChatClient : IChatClient
{
    public ChatClientMetadata Metadata { get; } = 
        new("Test", new Uri("https://test.example.com"), "test-model");

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);

        // 1. Emit multiple function calls in one update (parallel tools)
        yield return new ChatResponseUpdate
        {
            Contents = [
                new FunctionCallContent("call_weather", "get_weather", 
                    new Dictionary<string, object?> { ["location"] = "Seattle" }),
                new FunctionCallContent("call_time", "get_time", 
                    new Dictionary<string, object?> { ["timezone"] = "PST" })
            ],
            Role = ChatRole.Assistant,
            MessageId = "msg_calls"
        };

        await Task.Delay(50, cancellationToken);

        // 2. Emit results separately (realistic async execution)
        yield return new ChatResponseUpdate
        {
            Contents = [new FunctionResultContent("call_time", """{"time":"3:45 PM"}""")],
            Role = ChatRole.Tool,
            MessageId = "msg_time_result"
        };

        await Task.Delay(20, cancellationToken);

        yield return new ChatResponseUpdate
        {
            Contents = [new FunctionResultContent("call_weather", """{"temp":"72F"}""")],
            Role = ChatRole.Tool,
            MessageId = "msg_weather_result"
        };

        // 3. Final response
        yield return new ChatResponseUpdate
        {
            Contents = [new TextContent("It's 72F in Seattle, and the time is 3:45 PM.")],
            Role = ChatRole.Assistant,
            MessageId = "msg_final"
        };
    }

    public Task<ChatResponse> GetResponseAsync(...) { /* Omitted */ }
    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}
```

---

## Testing Patterns

### E2E UI Testing with Playwright

Combine mocks with Playwright to test **actual Blazor rendering**:

```csharp
[Test]
public async Task Chat_RendersToolCallsAndResults()
{
    // 1. Configure server to use mock client
    // (Set environment variable or DI override)
    Environment.SetEnvironmentVariable("USE_MOCK_CHAT_CLIENT", "true");

    // 2. Start server with mock
    await StartServerWithMockAsync(new FunctionCallMockChatClient());

    // 3. Navigate to chat UI
    await Page.GotoAsync("http://localhost:5000");

    // 4. Send message
    var input = Page.Locator("textarea[placeholder='Type your message...']");
    await input.FillAsync("What's the weather?");
    await Page.Locator("button.send-button").ClickAsync();

    // 5. Assert UI updates
    // - User message appears
    await Expect(Page.Locator(".user-message").Last).ToContainTextAsync("weather");

    // - Tool call indicator appears (e.g., spinner, "Calling get_weather...")
    await Expect(Page.Locator(".tool-call-indicator")).ToBeVisibleAsync();

    // - Tool result appears (e.g., "Weather retrieved: 72F")
    await Expect(Page.Locator(".tool-result")).ToContainTextAsync("72F");

    // - Final assistant message appears
    await Expect(Page.Locator(".assistant-message").Last).ToContainTextAsync("sunny");
}
```

### Unit Testing AGUIProtocolService

Test protocol parsing separately:

```csharp
[Fact]
public async Task StreamResponseAsync_WithFunctionCall_YieldsToolCallUpdate()
{
    // Arrange
    var mockClient = new FunctionCallMockChatClient();
    var logger = new Mock<ILogger<AGUIProtocolService>>();
    var service = new AGUIProtocolService(mockClient, logger.Object);

    var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };

    // Act
    List<AGUIUpdate> updates = [];
    await foreach (var update in service.StreamResponseAsync(messages))
    {
        updates.Add(update);
    }

    // Assert
    Assert.Contains(updates, u => u is AGUIToolCall);
    Assert.Contains(updates, u => u is AGUIToolResult);
    
    var toolCall = updates.OfType<AGUIToolCall>().First();
    Assert.Equal("get_weather", toolCall.Content.Name);
}
```

---

## Best Practices

### 1. **Preserve Realistic Timing**
Add small delays (`Task.Delay`) between updates to simulate network latency and async tool execution.

### 2. **Include Message IDs**
Set `MessageId` on each update for correlation in the UI.

### 3. **Use Proper Content Types**
- `FunctionCallContent` for tool invocations
- `FunctionResultContent` for tool outputs
- `DataContent` with correct `MediaType` for AG-UI state

### 4. **Test Both Snapshot and Delta**
Ensure UI handles:
- Initial state snapshots (`application/json`)
- Incremental updates (`application/json-patch+json`)

### 5. **Mock Tool Registration**
Match mock tool names with actual `AITool` registrations to test `FunctionInvokingChatClient` behavior.

```csharp
AITool[] tools = [
    AIFunctionFactory.Create(GetWeather, "get_weather", "Get weather"),
    AIFunctionFactory.Create(CreatePlan, "create_plan", "Create plan")
];

// Mock must emit function calls matching these tool names
```

### 6. **Test Error Cases**
Mock error scenarios:
- Tool call with invalid arguments
- Tool result with error status
- State patch that fails to apply

```csharp
yield return new ChatResponseUpdate
{
    Contents = [new FunctionResultContent(
        callId,
        null,
        exception: new InvalidOperationException("Tool execution failed"))],
    Role = ChatRole.Tool
};
```

---

## Integration with AGUIWebChat

### Server-Side Setup

```csharp
// Program.cs - Conditional mock for testing
IChatClient chatClient;
if (builder.Configuration["USE_MOCK_CLIENT"] == "true")
{
    chatClient = new FunctionCallMockChatClient();
}
else
{
    // Real Azure OpenAI or OpenAI client
    chatClient = azureOpenAIClient.GetChatClient(deploymentName);
}

AIAgent agent = new AgenticUIAgent(
    chatClient.AsIChatClient().AsAIAgent(options: new ChatClientAgentOptions
    {
        ChatOptions = new ChatOptions { Tools = tools }
    }),
    jsonOptions.SerializerOptions
);

app.MapAGUI("/ag-ui", agent);
```

### Client-Side Consumption

No changes needed! `AGUIProtocolService` handles all content types:

```csharp
await foreach (var update in _protocolService.StreamResponseAsync(messages))
{
    switch (update)
    {
        case AGUITextPart text:
            AppendText(text.Text);
            break;
        case AGUIToolCall call:
            ShowToolCallIndicator(call.Content.Name);
            break;
        case AGUIToolResult result:
            ShowToolResult(result.Content);
            break;
        case AGUIDataSnapshot snapshot:
            RenderComponent(snapshot.JsonData);
            break;
        case AGUIDataDelta delta:
            PatchComponent(delta.JsonData);
            break;
    }
}
```

---

## Testing Workflow Summary

1. **Create Mock Client**: Implement `IChatClient` with predefined streaming responses
2. **Register Tools**: Use `AIFunctionFactory` to register tools matching mock function names
3. **Wrap with AgenticUIAgent**: Apply AG-UI protocol transformations (state events)
4. **Configure DI or Env**: Inject mock into server via configuration
5. **Run E2E Tests**: Use Playwright to verify UI rendering
6. **Run Unit Tests**: Test protocol service and state management separately

---

## Example: Full Test Case

```csharp
[Fact]
public async Task AGUIWebChat_RendersCreatePlanWithUpdates()
{
    // 1. Setup mock
    var mockClient = new AGUIPlanMockChatClient();
    
    // 2. Configure server
    await using var server = await CreateTestServerAsync(mockClient);
    
    // 3. Create client
    var httpClient = server.CreateClient();
    var chatClient = new AGUIChatClient(httpClient, "/ag-ui", null);
    var agent = chatClient.AsAIAgent();
    
    // 4. Send message
    List<AgentResponseUpdate> updates = [];
    await foreach (var update in agent.RunStreamingAsync([new("What's your plan?")]))
    {
        updates.Add(update);
    }
    
    // 5. Verify AG-UI protocol
    var dataSnapshots = updates.SelectMany(u => u.Contents.OfType<DataContent>())
        .Where(d => d.MediaType == "application/json")
        .ToList();
    Assert.Single(dataSnapshots); // Initial plan snapshot
    
    var dataDeltas = updates.SelectMany(u => u.Contents.OfType<DataContent>())
        .Where(d => d.MediaType == "application/json-patch+json")
        .ToList();
    Assert.Single(dataDeltas); // Step status update
    
    // 6. Verify function calls
    var functionCalls = updates.SelectMany(u => u.Contents.OfType<FunctionCallContent>()).ToList();
    Assert.Equal(2, functionCalls.Count); // create_plan and update_plan_step
    Assert.Contains(functionCalls, fc => fc.Name == "create_plan");
    Assert.Contains(functionCalls, fc => fc.Name == "update_plan_step");
}
```

---

## Conclusion

Mocking streaming responses with function calls enables:
- ✅ **Automated UI Testing**: Playwright tests without LLM dependency
- ✅ **Fast Iteration**: Test AG-UI protocol handling without API delays
- ✅ **Reproducible Scenarios**: Deterministic test cases for edge conditions
- ✅ **Cost-Free Testing**: No Azure/OpenAI API costs during development

Follow the patterns in `dotnet/tests/Microsoft.Agents.AI.Hosting.OpenAI.UnitTests/TestHelpers.cs` and `dotnet/tests/Microsoft.Agents.AI.Hosting.AGUI.AspNetCore.IntegrationTests/ToolCallingTests.cs` for production-quality test infrastructure.

---

**Next Steps**:
1. Create `AGUIWebChat.Tests.Mocks` project with reusable mock clients
2. Implement E2E Playwright tests using mocks (via `playwright-cli` skill)
3. Add unit tests for `AGUIProtocolService` with mock scenarios
4. Document mock usage in AGUIWebChat README.md

