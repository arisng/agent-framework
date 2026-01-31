# Quick Reference: Mocking Streaming Responses

**TL;DR**: How to mock `IChatClient` streaming responses with function calls for AG-UI testing.

---

## Core Pattern

```csharp
public sealed class MyMockChatClient : IChatClient
{
    public ChatClientMetadata Metadata { get; } = 
        new("Test", new Uri("https://test.example.com"), "test-model");

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken);

        // Yield updates here
        yield return new ChatResponseUpdate
        {
            Contents = [new TextContent("Hello")],
            Role = ChatRole.Assistant,
            MessageId = "msg1"
        };
    }

    public Task<ChatResponse> GetResponseAsync(...) => throw new NotImplementedException();
    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}
```

---

## Key Content Types

### 1. Text Streaming
```csharp
yield return new ChatResponseUpdate
{
    Contents = [new TextContent("word ")],
    Role = ChatRole.Assistant
};
```

### 2. Function Call
```csharp
string callId = "call_123";
yield return new ChatResponseUpdate
{
    Contents = [new FunctionCallContent(
        callId,
        "function_name",
        new Dictionary<string, object?> { ["param"] = "value" }
    )],
    Role = ChatRole.Assistant
};
```

### 3. Function Result
```csharp
yield return new ChatResponseUpdate
{
    Contents = [new FunctionResultContent(callId, """{"result":"data"}""")],
    Role = ChatRole.Tool
};
```

### 4. AG-UI State Snapshot
```csharp
byte[] jsonBytes = Encoding.UTF8.GetBytes("""{"state":"data"}""");
yield return new ChatResponseUpdate
{
    Contents = [new DataContent(jsonBytes, "application/json")],
    Role = ChatRole.System
};
```

### 5. AG-UI State Delta (JSON Patch)
```csharp
byte[] patchBytes = Encoding.UTF8.GetBytes("""[{"op":"replace","path":"/field","value":"new"}]""");
yield return new ChatResponseUpdate
{
    Contents = [new DataContent(patchBytes, "application/json-patch+json")],
    Role = ChatRole.System
};
```

---

## Complete Flow Example

```csharp
public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(...)
{
    // 1. Function call
    string callId = "call_" + Guid.NewGuid().ToString("N");
    yield return new ChatResponseUpdate
    {
        Contents = [new FunctionCallContent(callId, "create_plan", 
            new Dictionary<string, object?> { ["steps"] = new[] { "Step 1", "Step 2" } })],
        Role = ChatRole.Assistant
    };

    await Task.Delay(50);

    // 2. Function result
    yield return new ChatResponseUpdate
    {
        Contents = [new FunctionResultContent(callId, 
            """{"steps":[{"description":"Step 1","status":"pending"}]}""")],
        Role = ChatRole.Tool
    };

    // 3. (Optional) AG-UI state snapshot - added by AgenticUIAgent wrapper
    // This happens automatically when wrapping with AgenticUIAgent

    // 4. Text response
    yield return new ChatResponseUpdate
    {
        Contents = [new TextContent("Plan created!")],
        Role = ChatRole.Assistant
    };
}
```

---

## Usage in Tests

### Option 1: Direct Mock
```csharp
var mockClient = new MyMockChatClient();
var agent = mockClient.AsIChatClient().AsAIAgent();

await foreach (var update in agent.RunStreamingAsync([new ChatMessage(ChatRole.User, "Hi")]))
{
    // Test UI rendering
}
```

### Option 2: Via Dependency Injection
```csharp
// In Program.cs
if (config["USE_MOCK"] == "true")
{
    services.AddSingleton<IChatClient>(new MyMockChatClient());
}

// In test
Environment.SetEnvironmentVariable("USE_MOCK", "true");
await StartServerAsync();
```

### Option 3: Via Configuration
```csharp
// appsettings.Testing.json
{
  "USE_MOCK_CLIENT": true,
  "MOCK_SCENARIO": "planning"
}

// Program.cs
bool useMock = builder.Configuration.GetValue<bool>("USE_MOCK_CLIENT");
string? scenario = builder.Configuration["MOCK_SCENARIO"];

IChatClient client = useMock 
    ? CreateMockClient(scenario)
    : CreateRealClient();
```

---

## Wrapping with AgenticUIAgent

To emit AG-UI protocol events (state snapshots/deltas):

```csharp
var mockClient = new MyMockChatClient();
var baseAgent = mockClient.AsIChatClient().AsAIAgent(
    chatOptions: new ChatOptions
    {
        Tools = [
            AIFunctionFactory.Create(MyFunction, "my_function", "Description")
        ]
    }
);

var jsonOptions = new JsonSerializerOptions();
var agenticUIAgent = new AgenticUIAgent(baseAgent, jsonOptions);

// Now streaming will include DataContent for state updates
await foreach (var update in agenticUIAgent.RunStreamingAsync([...]))
{
    // Will receive both FunctionResultContent AND DataContent events
}
```

---

## Common Patterns

### Multiple Function Calls (Parallel)
```csharp
yield return new ChatResponseUpdate
{
    Contents = [
        new FunctionCallContent("call_1", "tool_1", args1),
        new FunctionCallContent("call_2", "tool_2", args2)
    ],
    Role = ChatRole.Assistant
};

// Results can come separately
yield return new ChatResponseUpdate
{
    Contents = [new FunctionResultContent("call_1", result1)],
    Role = ChatRole.Tool
};

yield return new ChatResponseUpdate
{
    Contents = [new FunctionResultContent("call_2", result2)],
    Role = ChatRole.Tool
};
```

### Mixed Content
```csharp
yield return new ChatResponseUpdate
{
    Contents = [
        new TextContent("I found this: "),
        new DataContent(imageBytes, "image/png"),
        new TextContent(" Hope this helps!")
    ],
    Role = ChatRole.Assistant
};
```

### Error Handling
```csharp
yield return new ChatResponseUpdate
{
    Contents = [new FunctionResultContent(
        callId,
        null,
        exception: new InvalidOperationException("Tool failed")
    )],
    Role = ChatRole.Tool
};
```

---

## File Locations

- **Full Research**: `/.docs/research/mocking-streaming-agui-responses.md`
- **Code Samples**: `/dotnet/samples/AGUIWebChat/.docs/testing/mock-clients-reference.md`
- **Framework Examples**: `/dotnet/tests/Microsoft.Agents.AI.Hosting.OpenAI.UnitTests/TestHelpers.cs`

---

## Checklist for New Mock

- [ ] Implement `IChatClient` interface
- [ ] Use `IAsyncEnumerable<ChatResponseUpdate>` for streaming
- [ ] Set `MessageId` on each update
- [ ] Add realistic delays (`Task.Delay`)
- [ ] Match function names with registered `AITool` names
- [ ] Set correct `Role` (Assistant, Tool, System)
- [ ] Use proper `MediaType` for `DataContent` (json vs json-patch+json)
- [ ] Consider wrapping with `AgenticUIAgent` for state events
- [ ] Test with both unit tests and E2E Playwright tests

---

**Next**: See full examples in `/dotnet/samples/AGUIWebChat/.docs/testing/mock-clients-reference.md`
