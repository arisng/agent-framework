# AGUI WebChat Sample

This sample demonstrates a Blazor-based web chat application using the AG-UI protocol to communicate with an AI agent server, including Agentic UI state updates (plan snapshots and JSON Patch deltas).

The sample consists of two projects:

1. **Server** - An ASP.NET Core server that hosts a simple chat agent using the AG-UI protocol
2. **Client** - A Blazor Server application with a rich chat UI for interacting with the agent and rendering Agentic UI state updates

## Prerequisites

### Azure OpenAI Configuration

The server supports Azure OpenAI and OpenAI. To use Azure OpenAI, set the following environment variables:

```powershell
$env:AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/"
$env:AZURE_OPENAI_DEPLOYMENT_NAME="your-deployment-name"  # e.g., "gpt-4o"
```

The server uses `DefaultAzureCredential` for authentication. Ensure you are logged in using one of the following methods:

- Azure CLI: `az login`
- Azure PowerShell: `Connect-AzAccount`
- Visual Studio or VS Code with Azure extensions
- Environment variables with service principal credentials

### OpenAI Configuration

To use OpenAI, set the following environment variables:

```powershell
$env:OPENAI_API_KEY="your-openai-api-key"
$env:OPENAI_MODEL="gpt-5-mini"
```

```powershell
cd Server

# Set OpenAI API key (connection string value)
dotnet user-secrets set "OPENAI_API_KEY" "API-KEY"
```

If both Azure OpenAI and OpenAI environment variables are provided, the server prefers Azure OpenAI.

## Running the Sample

### Option 1: VS Code Compound Launch (Recommended for Debugging)

1. Open the AGUIWebChat folder in VS Code.
2. Press F5 or go to Run > Start Debugging.
3. Select "Debug AGUIWebChat (Server + Client)" from the dropdown.
4. Both server and client will start simultaneously, and the browser will open to `http://localhost:7000`.

### Option 2: Debug Script

Run the provided script to start both services:

```bash
./run-debug.sh
```

This will start the server on `http://localhost:6100` and client on `http://localhost:7000`. Press Ctrl+C to stop both.

### Option 3: Manual (Original Method)

#### Step 1: Start the Server

Open a terminal and navigate to the Server directory:

```bash
cd Server
dotnet run --urls=http://localhost:6100
```

The server will start on `http://localhost:6100` and expose the AG-UI endpoint at `/ag-ui`.

#### Step 2: Start the Client

Open a new terminal and navigate to the Client directory:

```bash
cd Client
dotnet run --urls=http://localhost:7000
```

The client will start on `http://localhost:7000`. Open your browser and navigate to `http://localhost:7000` to access the chat interface.

## Solution File

The sample includes a solution file at [AGUIWebChat.slnx](AGUIWebChat.slnx) that references both the server and client projects.

### Step 3: Chat with the Agent

Type your message in the text box at the bottom of the page and press Enter or click the send button. The assistant will respond with streaming text that appears in real-time.

Features:
- **Streaming responses**: Watch the assistant's response appear word by word
- **Agentic UI state updates**: View structured state snapshots and deltas emitted by the server
- **Backend tool rendering**: View tool executions with custom UI (e.g., weather tool with interactive cards)
- **Conversation suggestions**: The assistant may offer follow-up questions after responding
- **New chat**: Click the "New chat" button to start a fresh conversation
- **Auto-scrolling**: The chat automatically scrolls to show new messages

## How It **Works**

### Server (AG-UI Host)

The server (`Server/Program.cs`) creates a simple chat agent, selecting Azure OpenAI when configured, otherwise OpenAI:

```csharp
ChatClient chatClient;
if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(deploymentName))
{
  // Create Azure OpenAI client
  AzureOpenAIClient azureOpenAIClient = new AzureOpenAIClient(
    new Uri(endpoint),
    new DefaultAzureCredential());

  chatClient = azureOpenAIClient.GetChatClient(deploymentName);
}
else if (!string.IsNullOrWhiteSpace(openAiApiKey) && !string.IsNullOrWhiteSpace(openAiModel))
{
  // Create OpenAI client
  OpenAIClient openAIClient = new OpenAIClient(openAiApiKey);
  chatClient = openAIClient.GetChatClient(openAiModel);
}
else
{
  throw new InvalidOperationException(
    "Either AZURE_OPENAI_ENDPOINT and AZURE_OPENAI_DEPLOYMENT_NAME, or OPENAI_API_KEY and OPENAI_MODEL must be set.");
}

// Create AI agent
ChatClientAgent agent = chatClient.AsIChatClient().AsAIAgent(
  name: "AgenticUIAssistant",
  instructions: "You are a helpful assistant.");

// Wrap the agent to emit Agentic UI state events from tool results
AIAgent agenticUiAgent = new AgenticUIAgent(agent, jsonOptions.SerializerOptions);

// Map AG-UI endpoint
app.MapAGUI("/ag-ui", agenticUiAgent);
```

The server exposes the agent via the AG-UI protocol at `http://localhost:5100/ag-ui`, streaming both text and state events.

### Client (Blazor Web App)

The client (`Client/Program.cs`) configures an `AGUIChatClient` to connect to the server:

```csharp
string serverUrl = builder.Configuration["SERVER_URL"] ?? "http://localhost:5100";

builder.Services.AddHttpClient("aguiserver", httpClient => httpClient.BaseAddress = new Uri(serverUrl));

builder.Services.AddChatClient(sp => new AGUIChatClient(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("aguiserver"), "ag-ui"));
```

The Blazor UI (`Client/Components/Pages/Chat/Chat.razor`) uses the `IChatClient` to:
- Send user messages to the agent
- Stream responses back in real-time
- Maintain conversation history
- Display messages with appropriate styling

The message renderer (`Client/Components/Pages/Chat/ChatMessageItem.razor`) also renders Agentic UI state updates emitted as `DataContent` payloads.

The client uses a component registry to render content by MIME type instead of hardcoded checks:
- `IComponentRegistry` maps content types to component types.
- `DynamicMessageRenderer` resolves the component at runtime with `DynamicComponent`.
- `PlanComponent` is registered for `application/vnd.microsoft.agui.plan+json` so plan payloads render consistently.

### Backend Tool Rendering

The sample includes a **weather tool** (`get_weather`) that demonstrates backend tool rendering with custom UI components:

**Server-Side Implementation:**
- `WeatherTool.GetWeather(string location)` - A tool function that returns weather information for a given location
- `WeatherInfo` - A structured model containing temperature, conditions, humidity, wind speed, and "feels like" data
- Registered in the agent configuration alongside planning tools

**Client-Side Rendering:**
- `ChatWeatherTool.razor` - A custom Blazor component that renders weather data with:
  - **Loading state**: Animated spinner with "Retrieving weather..." message while the tool executes
  - **Weather card**: Custom UI with dynamic background colors based on weather conditions
  - **Weather icons**: SVG icons (sun, cloud, rain) that match the current conditions
  - **Temperature display**: Shows temperature in both Celsius and Fahrenheit
  - **Weather metrics**: Grid layout displaying humidity, wind speed, and "feels like" temperature

**Tool Call Flow:**
1. User asks about weather (e.g., "What's the weather in Seattle?")
2. LLM decides to call the `get_weather` tool with the location argument
3. Client displays loading indicator (tool call initiated)
4. Server executes the tool and returns structured weather data
5. Client renders custom weather card with all weather information

**Usage Example:**
```
User: "What's the weather in Tokyo?"
Assistant: [weather card displays with current conditions]
```

Try asking weather-related questions to see the tool in action!

### Interactive Quiz Feature

The sample includes support for **interactive quizzes** that provide an engaging way to assess knowledge, gather feedback, or guide user interactions through structured question-and-answer flows.

**Supported Quiz Capabilities:**
- **Single-select questions**: Radio button interface for selecting one answer from multiple options
- **Multi-select questions**: Checkbox interface for selecting multiple answers with configurable min/max constraints
- **Rich question content**: Questions can include descriptions, media attachments, and formatted text
- **Answer options**: Each answer can have a label, description, and optional media (images, links)
- **Real-time evaluation**: Display correct/incorrect feedback, scores, and explanatory messages
- **Conditional visibility**: Control when correct answers are shown based on display rules (always, after submission, never, or on correct answers only)
- **Disabled state**: Lock quiz cards after submission or based on business logic
- **Sequential ordering**: Cards are displayed in a specified sequence for structured learning paths

**Data Model:**
The quiz feature follows a comprehensive data model that defines:
- `Quiz`: Container with title, instructions, and a collection of question cards
- `QuestionCard`: Individual question with content, answer options, selection rules, correct answer definitions, user choices, and evaluation results
- `AnswerOption`: Answer choice with text, optional description, and media attachments
- `SelectionRule`: Defines single or multiple selection mode with min/max constraints
- `CorrectAnswerDisplayRule`: Controls visibility of correct answers based on conditions
- `CardEvaluation`: Provides scoring, correctness indicators, and feedback messages

For detailed schema information, see [.docs/ag-ui-quiz/quiz-data-model.md](.docs/ag-ui-quiz/quiz-data-model.md).

**Client-Side Components:**
- `QuizComponent.razor` - Renders the complete quiz with title, instructions, and question card list
- `QuizCardComponent.razor` - Renders individual question cards with:
  - Question text and optional description
  - Answer options (radio buttons or checkboxes based on selection mode)
  - User interaction handling and visual feedback
  - Evaluation display (score, correctness, feedback)
  - Conditional correct answer display
- `QuizComponent.razor.css` / `QuizCardComponent.razor.css` - Modern, responsive styling with visual indicators for selected, correct, and incorrect states

**Component Registry Integration:**
Quiz components are registered via the component registry system:
- `application/vnd.quiz+json` → `QuizComponent`
- `application/vnd.quiz.card+json` → `QuizCardComponent`

This enables dynamic rendering without hardcoded quiz logic in message handlers.

**Usage Example:**
```
User: "Can you quiz me on C# basics?"
Assistant: [quiz card displays with questions]
User: [selects answers via radio buttons/checkboxes]
Assistant: [displays evaluation with score and feedback]
```

**AG-UI Protocol Integration:**
The server can emit quiz data as part of the AG-UI state stream using the `application/vnd.quiz+json` or `application/vnd.quiz.card+json` media types. The client automatically routes these to the appropriate quiz renderer components, providing a seamless interactive experience.

Try asking the agent to create a quiz to see this feature in action!

### UI Components

The chat interface is built from several Blazor components:

- **Chat.razor** - Main chat page coordinating the conversation flow
- **ChatHeader.razor** - Header with "New chat" button
- **ChatMessageList.razor** - Scrollable list of messages with auto-scroll
- **ChatMessageItem.razor** - Individual message rendering (user vs assistant)
- **ChatInput.razor** - Text input with auto-resize and keyboard shortcuts
- **ChatSuggestions.razor** - AI-generated follow-up question suggestions
- **LoadingSpinner.razor** - Animated loading indicator during streaming

## Configuration

### Server Configuration

The server URL and port are configured in `Server/Properties/launchSettings.json`:

```json
{
  "profiles": {
    "http": {
      "applicationUrl": "http://localhost:6100"
    }
  }
}
```

### Client Configuration

The client connects to the server URL specified in `Client/Properties/launchSettings.json`:

```json
{
  "profiles": {
    "http": {
      "applicationUrl": "http://localhost:7000",
      "environmentVariables": {
        "SERVER_URL": "http://localhost:6100"
      }
    }
  }
}
```

To change the server URL, modify the `SERVER_URL` environment variable in the client's launch settings or provide it at runtime:

```bash
$env:SERVER_URL="http://your-server:6100"
dotnet run
```

## Customization

### Adding Custom Tools

The sample demonstrates how to add custom backend tools with specialized UI rendering. To add your own tool:

**1. Create a Tool Class (Server):**
```csharp
public static class MyTool
{
    [Description("Description of what your tool does")]
    public static MyResult ExecuteTool(
        [Description("Parameter description")] string parameter)
    {
        // Tool implementation
        return new MyResult { /* ... */ };
    }
}
```

**2. Register the Tool (Server/Program.cs):**
```csharp
AIFunctionFactory.Create(
    MyTool.ExecuteTool,
    name: "my_tool",
    description: "Tool description for LLM",
    serializerOptions: jsonOptions.SerializerOptions)
```

**3. Create a UI Component (Client):**
```razor
@* Client/Components/Pages/Chat/ChatMyTool.razor *@
<div class="my-tool-card">
    @* Custom UI for your tool result *@
</div>
```

**4. Integrate in ChatMessageItem.razor:**
```razor
else if (content is FunctionCallContent { Name: "my_tool" } myToolCall)
{
    TrackFunctionCall(myToolCall);
    var result = GetMatchingResult(myToolCall.CallId);
    <ChatMyTool Call="@myToolCall" Result="@result" InProgress="@(result == null && InProgress)" />
}
```

See `WeatherTool`, `WeatherInfo`, and `ChatWeatherTool.razor` for a complete reference implementation.

### Changing the Agent Instructions

Edit the instructions in `Server/Program.cs`:

```csharp
ChatClientAgent agent = chatClient.AsIChatClient().AsAIAgent(
  name: "AgenticUIAssistant",
  instructions: "You are a helpful coding assistant specializing in C# and .NET.");
```

### Styling the UI

The chat interface uses CSS files colocated with each Razor component. Key styles:

- `wwwroot/app.css` - Global styles, buttons, color scheme
- `Components/Pages/Chat/Chat.razor.css` - Chat container layout
- `Components/Pages/Chat/ChatMessageItem.razor.css` - Message bubbles and icons
- `Components/Pages/Chat/ChatInput.razor.css` - Input box styling

### Disabling Suggestions

To disable the AI-generated follow-up suggestions, comment out the suggestions component in `Chat.razor`:

```razor
@* <ChatSuggestions OnSelected="@AddUserMessageAsync" @ref="@chatSuggestions" /> *@
```
