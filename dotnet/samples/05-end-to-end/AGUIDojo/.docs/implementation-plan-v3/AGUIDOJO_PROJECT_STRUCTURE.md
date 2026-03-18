# AGUIDojo Project Structure - Comprehensive Overview

## Project Base Directory
`/home/arisng/src/agent-framework/dotnet/samples/05-end-to-end/AGUIDojo/`

---

## 1. DIRECTORY STRUCTURE

### AGUIDojoClient Structure
**AGUIDojoClient/Models/**
- ApprovalItem.cs
- ChartResult.cs
- Checkpoint.cs
- ConversationTree.cs
- DataGridResult.cs
- DiffPreviewData.cs
- DocumentState.cs
- DynamicFormResult.cs
- Ingredient.cs
- JsonPatchOperation.cs
- Plan.cs
- ReasoningStep.cs
- Recipe.cs
- SessionMetadata.cs
- SessionNotification.cs
- SessionStatus.cs
- Step.cs
- StepStatus.cs
- WeatherInfo.cs

**AGUIDojoClient/Services/**
- AGUIChatClientFactory.cs
- AgentStreamingService.cs (1,091 lines)
- ApprovalHandler.cs
- CheckpointService.cs
- IAGUIChatClientFactory.cs
- IAgentStreamingService.cs
- ICheckpointService.cs
- IMarkdownService.cs
- IObservabilityService.cs
- IRiskAssessmentService.cs
- IStateManager.cs
- IThemeService.cs
- IToolComponentRegistry.cs
- IWeatherApiClient.cs
- JsonPatchApplier.cs
- MarkdownService.cs
- ObservabilityService.cs
- RiskAssessmentService.cs
- SessionPersistenceService.cs
- SessionStreamingContext.cs
- StateManager.cs
- ThemeService.cs
- ToolComponentRegistry.cs
- ViewportService.cs
- WeatherApiClient.cs

**AGUIDojoClient/Components/Pages/Chat/**
- AgentAvatar.razor
- AgentAvatar.razor.css
- AgentHandoff.razor
- AgentHandoff.razor.css
- AssistantThought.razor
- AssistantThought.razor.css
- Chat.razor (448 lines)
- Chat.razor.css
- ChatCitation.razor
- ChatCitation.razor.css
- ChatHeader.razor
- ChatHeader.razor.css
- ChatInput.razor (68 lines)
- ChatInput.razor.css (59 lines)
- ChatInput.razor.js (44 lines)
- ChatMessageItem.razor (350 lines)
- ChatMessageItem.razor.css (814 lines)
- ChatMessageList.razor
- ChatMessageList.razor.css
- ChatMessageList.razor.js
- ChatSuggestions.razor
- ChatSuggestions.razor.css
- SessionListItem.razor
- SessionListItem.razor.css

### AGUIDojoServer Structure
**AGUIDojoServer/Api/**
- AuthEndpoints.cs
- EmailEndpoints.cs
- TitleEndpoints.cs (87 lines)
- WeatherEndpoints.cs

**Top-level Server Files**
- Program.cs (475 lines)
- ChatClientAgentFactory.cs (219 lines)

---

## 2. KEY FILE CONTENTS

### ChatInput.razor (68 lines)
**Purpose**: Text input component for sending chat messages

**Key Features**:
- EditForm with message text binding (@bind="@messageText")
- Textarea with placeholder "Type your message..."
- Auto-focusing via JS interop
- Send button with SVG icon
- PanicButton component for emergency stop
- OnSend EventCallback with ChatMessage parameter
- IsAgentRunning parameter controls PanicButton visibility

**Code Structure**:
```csharp
@inject IJSRuntime JS
private ElementReference textArea;
private string? messageText;
[Parameter] public EventCallback<ChatMessage> OnSend { get; set; }
[Parameter] public bool IsAgentRunning { get; set; }
[Parameter] public EventCallback OnPanic { get; set; }
private async Task SendMessageAsync()
private async Task OnAfterRenderAsync(bool firstRender)
```

---

### ChatInput.razor.js (44 lines)
**Purpose**: JavaScript interop for textarea auto-resize and Enter key submission

**Key Functions**:
- `init(elem)` - Initialize textarea with auto-resize and Enter key handling
- `resizeToFit(elem)` - Auto-resize textarea based on content (1-5 rows)
- `afterPropertyWritten(target, propName, callback)` - Property change tracking
- `getPropertyDescriptor(target, propertyName)` - Recursive property descriptor lookup

**Behavior**:
- Auto-resize on input
- Auto-submit form on Enter (without Shift)
- Prevents default on Enter to avoid newline

---

### ChatInput.razor.css (59 lines)
**Purpose**: Styling for the chat input component

**Key Classes**:
- `.input-box` - Main container, flexbox column layout
- `.input-box:focus-within` - Ring outline on focus
- `textarea` - No resize, border, outline; flex-grow
- `.tools` - Flex row for buttons
- `.send-button` - Muted color, hover state
- `.attach` - Attachment button styling (dashed border)

---

### Chat.razor (Full - 448 lines)
**Purpose**: Main chat page component with multi-pane layout

**Key Injections**:
- IAgentStreamingService StreamingService
- IRiskAssessmentService RiskAssessmentService
- IStateManager StateManager
- ViewportService ViewportService
- IDispatcher Dispatcher
- IState<SessionManagerState> SessionStore
- ISessionPersistenceService SessionPersistence

**Key Features**:
- BbSidebarInset layout with DualPaneLayout
- ChatHeader with session management
- ChatMessageList with approval handling
- ChatInput with panic button
- ChatSuggestions component
- CanvasPane for artifact rendering (recipes, documents, data grids)

**Session Management**:
- Create new sessions with unique GUIDs
- Track active session ID
- Initialize with system prompt
- Handle session selection/deletion
- Throttled state updates (100ms)

**State Handling**:
- Bootstrap recipe state on keywords (recipe, ingredients, cook, etc.)
- Edit and regenerate messages
- Manage pending approvals
- Checkpoint management

**Lifecycle**:
- OnInitializedAsync - Set up callbacks, initialize throttle timer
- OnAfterRenderAsync - Initialize viewport, hydrate sessions from storage
- Disposed - Clean up timers and subscriptions

---

### ChatMessageItem.razor (Full - 350 lines)
**Purpose**: Renders individual chat messages (user and assistant)

**Parameters**:
- `Message` - ChatMessage to display (required)
- `InProgress` - bool indicating streaming in progress
- `BranchInfo` - (CurrentIndex, Total, SiblingIds)? for conversation branches
- `NodeId` - Conversation tree node ID
- `MessageIndex` - Zero-based message index
- `OnSwitchBranch` - EventCallback for branch navigation
- `OnEditMessage` - EventCallback for edit/regenerate

**Rendering Logic**:

**User Messages**:
- Display message text
- Branch navigation buttons (if siblings exist)
- Edit mode with textarea and Save/Cancel buttons
- Edit button on hover

**Assistant Messages**:
- Sort contents by priority: FunctionCall → FunctionResult → DataContent → TextContent → ErrorContent
- Separate visual components from thoughts
- Render AssistantThought collapsible for non-visual thoughts
- Render visual components in message-list
- Render TextContent as markdown
- Display agent avatar or default assistant icon

**Content Priority System** (GetContentPriority):
```csharp
1 - FunctionCallContent
2 - FunctionResultContent
3 - DataContent
4 - TextContent
5 - ErrorContent
99 - Unknown types
```

**Visual Component Detection** (IsVisualComponent):
- Checks if content is FunctionResultContent
- Looks up tool metadata in registry
- Verifies RenderLocation == MessageList && IsVisual

---

### ChatMessageItem.razor.css (814 lines)
**Purpose**: Comprehensive styling for all message types and content

**Main Sections**:
- User message styling
- Edit mode (textarea, buttons)
- Branch navigation
- Assistant message styling
- Tool call styles (violet accent)
- Tool result styles (green success)
- Tool result rejected styles (red danger)
- Weather tool result styling
- Dynamic tool result styling
- State snapshot styles
- State delta styles
- Data content generic styles
- Error content styles
- Markdown content styles (comprehensive)
- Visual tool result styles

**Key Color Scheme**:
- Primary: main brand color
- Muted: secondary backgrounds
- Violet/Accent-violet: tool calls
- Green/Alert-success: successful results
- Red/Alert-danger: errors/rejected
- Amber/Accent-amber: warnings/weather

---

### TitleEndpoints.cs (87 lines)
**Purpose**: Minimal API endpoints for LLM-powered session title generation

**Endpoint**: `POST /api/title`

**Request/Response**:
```csharp
record TitleRequest(List<TitleMessage> Messages)
record TitleMessage(string Role, string Content)
record TitleResponse(string Title)
```

**Logic**:
- Takes conversation messages
- Passes up to first 4 messages (2 turns) to LLM
- System prompt: "Generate a concise 4-8 word title..."
- Trims result to 60 characters max
- Returns title only

---

### Program.cs (475 lines)
**Purpose**: ASP.NET Core application startup and configuration

**Key Sections**:

1. **LLM Configuration Validation**
   - Azure OpenAI (DefaultAzureCredential or API key)
   - OpenAI (API key)
   - Fails fast if neither configured

2. **OpenTelemetry Setup**
   - Service name: "AGUIDojoServer"
   - ASP.NET Core tracing
   - HttpClient tracing
   - Runtime metrics
   - Console + OTLP exporters

3. **JWT Authentication** (conditional)
   - Issuer: "AGUIDojoServer"
   - Audience: "AGUIDojoClient"
   - Token validation parameters
   - Clock skew: 2 minutes
   - Only enabled if Jwt:SigningKey configured

4. **Error Handling** (RFC 7807 ProblemDetails)
   - UseExceptionHandler - catches unhandled exceptions
   - UseStatusCodePages - formats HTTP error codes
   - Development: includes exception details
   - Production: hides exception details

5. **Service Registration**
   - Scoped: IWeatherService, IEmailService, IDocumentService
   - Singleton: WeatherTool, EmailTool, DocumentTool, ChatClientAgentFactory
   - HttpContextAccessor for DI-compatible tools
   - Health checks (self, llm_configuration)

6. **Endpoint Mapping**
   - `/health` - Health check aggregate
   - `/api/weather`, `/api/email`, `/api/title` - Business APIs
   - `/api/auth` - Dev-only authentication
   - `/chat` - AG-UI unified endpoint

---

### ChatClientAgentFactory.cs (219 lines)
**Purpose**: Factory for creating AI agents with various capabilities

**Initialization**:
- Creates ChatClient based on Azure OpenAI or OpenAI config
- Registers weather, email, document tools
- Wraps with OpenTelemetry instrumentation

**Key Methods**:
- `GetChatClient()` - Returns IChatClient for lightweight LLM calls

- `CreateUnifiedAgent()` - Creates full agent with:
  - ToolResultStreamingChatClient wrapper
  - ContextWindowChatClient (max 80 non-system messages)
  - ServerFunctionApprovalAgent for human-in-the-loop
  - AgenticUIAgent for interactive components
  - PredictiveStateUpdatesAgent for state management
  - SharedStateAgent for state sharing
  - OpenTelemetry instrumentation

**System Prompt** (UnifiedSystemPrompt):
- Versatile assistant for conversations, data queries, documents, planning, visualization
- Tool usage guidelines for each function
- Rules: planning uses tools only, after tool execution provide brief summary

**Tools Created**:
- get_weather
- send_email (requires approval)
- write_document
- create_plan
- update_plan_step
- show_chart
- show_data_grid
- show_form

---

### SessionActions.cs (83 lines)
**Purpose**: Fluxor actions for session-scoped chat state management

**Main Actions**:
- **Session Creation/Deletion**
  - CreateSessionAction
  - SetActiveSessionAction
  - ArchiveSessionAction
  - SetSessionTitleAction
  - SetSessionStatusAction

- **Message Management**
  - AddMessageAction
  - UpdateResponseMessageAction
  - EditAndRegenerateAction
  - ClearMessagesAction
  - TrimMessagesAction

- **Conversation State**
  - SetConversationIdAction
  - SetRunningAction
  - SetAuthorNameAction
  - SwitchBranchAction

- **Approval Handling**
  - SetPendingApprovalAction

- **Planning**
  - SetPlanAction
  - ApplyPlanDeltaAction
  - ClearPlanAction
  - SetPlanDiffAction

- **Document Management**
  - SetDocumentAction
  - SetDocumentPreviewAction

- **Recipe Management**
  - SetRecipeAction

- **Artifact Management**
  - ClearArtifactsAction
  - SetDiffPreviewArtifactAction
  - SetDataGridArtifactAction
  - SetActiveArtifactAction

- **Storage Hydration**
  - HydrateFromStorageAction
  - HydrateSessionsAction

---

### AgentStreamingService.cs (1,091 lines - Partial View)
**Purpose**: Coordinates AG-UI SSE streaming, governance, and conversation lifecycle

**Key Properties**:
- MaxConcurrentStreams = 3
- MaxQueuedStreams = 5

**Concurrency Management**:
- _runningSessions - HashSet of active session IDs
- _queuedRequests - Queue<QueuedStreamRequest> for backpressure
- _streamGate - Lock for thread-safe stream management
- _sessionContexts - Dictionary<string, SessionStreamingContext>

**Notification System**:
- _notifications - List<SessionNotification>
- _notificationGate - Lock for thread-safe notification management
- _notificationDismissals - Timed dismissal tracking

**Key Methods**:
- `CanQueueResponse(sessionId)` - Check if new request can be queued
- `ProcessAgentResponseAsync(sessionId)` - Queue/start streaming
- `ResolveApproval(sessionId, approved)` - Handle user approval decisions
- `CancelResponse(sessionId)` - Cancel active streaming
- `ResetConversation(sessionId, systemPrompt)` - Reset session state
- `HandlePanic(sessionId)` - Emergency stop with checkpoint revert
- `HandleCheckpointRevert(sessionId, checkpointId)` - Restore from checkpoint
- `SyncSessionState(sessionId)` - Sync state from server
- `DismissNotification(notificationId)` - Dismiss single notification

**Streaming Flow** (RunSessionResponseAsync):
1. Create chat client with current session state
2. Stream responses with approval request handling
3. Process function calls and results
4. Update data content (state snapshots, deltas)
5. Handle tool invocations via ObservabilityService
6. Dispatch updates to Fluxor store
7. Handle errors with notifications
8. Process approval responses
9. Add final message to session on completion
10. Promote queued streams when slots available

**Approval Handling**:
- Extracts approval requests from FunctionCallContent
- Creates TaskCompletionSource for user decision
- Dispatches SetPendingApprovalAction to UI
- Notifies via BackgroundApprovalRequired
- On approval, creates FunctionResultContent response
- Continues streaming after approval

**Error Handling**:
- OperationCanceledException - marked as cancelled
- HttpRequestException - captured as error notification
- Other exceptions - generic error handling
- Maintains streaming message for partial results

---

## 3. DATA FLOW ARCHITECTURE

### Message Flow: User Input → Response
```
User Types in ChatInput.razor
    ↓
OnSend EventCallback
    ↓
Chat.razor: AddUserMessageAsync()
    ↓
Dispatcher: SessionActions.AddMessageAction
    ↓
Fluxor Store Updated
    ↓
StreamingService.ProcessAgentResponseAsync()
    ↓
IChatClient.GetStreamingResponseAsync()
    ↓
Stream updates with contents
    ↓
SessionActions.UpdateResponseMessageAction (continuous)
    ↓
ChatMessageItem.razor Re-renders
    ↓
UI Updates in Real-time
    ↓
Final SessionActions.AddMessageAction
```

### Content Rendering Pipeline
```
StreamingMessage.Contents
    ↓
ChatMessageItem.razor: SortContentsForDisplay()
    ↓
Priority Sort:
  1. FunctionCallContent
  2. FunctionResultContent
  3. DataContent
  4. TextContent
  5. ErrorContent
    ↓
Separate Visual from Non-Visual
    ↓
Visual → Render in MessageList (via registry)
Non-Visual Thoughts → AssistantThought component
TextContent → Markdown conversion
    ↓
ChatMessageItem.razor.css Styling
    ↓
Final Rendered Message
```

### Approval Handling Flow
```
FunctionCallContent with approval marker
    ↓
ApprovalHandler.TryExtractApprovalRequest()
    ↓
Dispatcher: SessionActions.SetPendingApprovalAction
    ↓
Dispatcher: NotifyBackgroundApprovalRequired (notification)
    ↓
UI Shows Approval Prompt
    ↓
User Clicks Approve/Reject
    ↓
StreamingService.ResolveApproval()
    ↓
ApprovalHandler.CreateApprovalResponse()
    ↓
FunctionResultContent added to streaming message
    ↓
Resume streaming
```

---

## 4. KEY INTEGRATION POINTS FOR MULTIMODAL ATTACHMENT SUPPORT

### 1. **ChatInput.razor Component**
   - Add file input element for multimodal attachments
   - Currently: Text-only input with OnSend callback
   - **Enhancement Needed**: Accept File objects, pass as ChatMessage.Contents with AttachmentContent

### 2. **ChatMessage Model**
   - Extend Contents collection to support AttachmentContent type
   - Create AttachmentContent class for file metadata

### 3. **AgentStreamingService.cs**
   - Handle AttachmentContent in stream processing
   - Current: Processes FunctionCall, FunctionResult, DataContent, TextContent, ErrorContent
   - **Enhancement**: Add AttachmentContent handler

### 4. **ChatMessageItem.razor**
   - Render attachments in message display
   - Current: Priority sort doesn't include attachments
   - **Enhancement**: Add attachment rendering with preview (images, documents, etc.)

### 5. **TitleEndpoints.cs**
   - Include attachment metadata in title generation context
   - Current: Only processes text content
   - **Enhancement**: Consider attachment types/names in title generation

### 6. **Program.cs / ChatClientAgentFactory.cs**
   - Ensure IChatClient supports multimodal content
   - Verify tool definitions support attachment parameters

---

## 5. ENVIRONMENT CONFIGURATION

### Required Environment Variables
```
# LLM Provider (choose one):
OPENAI_API_KEY=sk-...              # OpenAI
OPENAI_MODEL=gpt-4o-mini          # Optional, defaults to gpt-5.4-mini

# OR

AZURE_OPENAI_ENDPOINT=https://...  # Azure OpenAI
AZURE_OPENAI_DEPLOYMENT_NAME=...   # Deployment name
# Authentication: DefaultAzureCredential (Managed Identity) or API key

# Optional:
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=http://localhost:5100
Jwt__SigningKey=<base64-encoded-256-bit-key>
```

---

## 6. BUILD & EXECUTION

### Project Structure
- `AGUIDojo.slnx` - Solution file
- `.aspire` - App Host project
- `AGUIDojoServer` - Backend (ASP.NET Core)
- `AGUIDojoClient` - Frontend (Blazor WebAssembly)

### Health Checks
- `GET /health` - Aggregate health status
- Checks: self (always healthy), llm_configuration (provider configured)

### API Endpoints
- `POST /api/title` - Generate session title
- `POST /api/weather` - Get weather
- `POST /api/email` - Send email (approval required)
- `GET /chat` - AG-UI SSE endpoint (streaming protocol)

