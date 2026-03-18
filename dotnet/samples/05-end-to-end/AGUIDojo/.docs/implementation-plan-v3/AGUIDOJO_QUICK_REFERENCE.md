# AGUIDojo Quick Reference Guide

## File Locations & Purposes

| File | Lines | Purpose |
|------|-------|---------|
| **Chat.razor** | 448 | Main chat page with dual-pane layout, session management |
| **ChatInput.razor** | 68 | Text input component with auto-resize, send button, panic button |
| **ChatInput.razor.js** | 44 | JS interop for textarea auto-resize and Enter submission |
| **ChatInput.razor.css** | 59 | Input box styling and tool buttons |
| **ChatMessageItem.razor** | 350 | Individual message rendering (user/assistant with branches) |
| **ChatMessageItem.razor.css** | 814 | Comprehensive styling for all message content types |
| **TitleEndpoints.cs** | 87 | POST /api/title for LLM session title generation |
| **Program.cs** | 475 | ASP.NET Core startup, DI registration, middleware setup |
| **ChatClientAgentFactory.cs** | 219 | Creates agents with tools and wrappers |
| **AgentStreamingService.cs** | 1,091 | Coordinates SSE streaming, concurrency, approvals |
| **SessionActions.cs** | 83 | Fluxor state management actions |

---

## Architecture Overview

```
FRONTEND (Blazor WebAssembly)
  ├── Chat.razor (Main page)
  ├── ChatInput.razor (User input)
  ├── ChatMessageList.razor (Message display)
  ├── ChatMessageItem.razor (Individual message)
  └── Services (AgentStreamingService, etc.)

BACKEND (ASP.NET Core)
  ├── Program.cs (Configuration, DI, Middleware)
  ├── /api/title (Session title generation)
  ├── /api/weather, /api/email (Business APIs)
  ├── /chat (AG-UI SSE endpoint)
  └── Services (Tools, State, etc.)

STATE MANAGEMENT
  └── Fluxor Store (SessionManagerState)
      ├── Sessions (per-session data)
      ├── Active Session ID
      ├── Messages
      ├── Running state
      ├── Pending approvals
      └── Artifacts (plans, documents, etc.)

STREAMING
  └── AgentStreamingService
      ├── Manages concurrent streams (max 3)
      ├── Queues requests (max 5)
      ├── Handles approvals
      └── Processes SSE updates
```

---

## Key Data Models

### ChatMessage (from Microsoft.Extensions.AI)
```csharp
ChatRole: System, User, Assistant
Text: string (optional)
Contents: IList<AIContent>
  ├── TextContent
  ├── FunctionCallContent
  ├── FunctionResultContent
  ├── DataContent
  ├── ErrorContent
  └── AttachmentContent (PROPOSED)
AuthorName: string (optional)
```

### SessionManagerState (Fluxor)
```csharp
Sessions: IReadOnlyDictionary<string, SessionEntry>
  └── SessionEntry
      ├── Metadata: SessionMetadata
      │   ├── Id: string
      │   ├── Title: string
      │   ├── Status: SessionStatus
      │   ├── HasPendingApproval: bool
      │   └── CreatedAt: DateTimeOffset
      └── State: SessionState
          ├── Messages: IList<ChatMessage>
          ├── CurrentResponseMessage: ChatMessage
          ├── IsRunning: bool
          ├── PendingApproval: PendingApproval
          ├── ConversationId: string
          ├── CurrentRecipe: Recipe
          ├── CurrentPlan: Plan
          ├── CurrentDocument: DocumentState
          └── Artifacts
```

### Content Priority (Display Order)
```
1. FunctionCallContent      (Tool calls)
2. FunctionResultContent    (Tool results)
3. DataContent              (State snapshots)
4. TextContent              (Message text)
5. ErrorContent             (Errors)
```

---

## Key Flows

### 1. Message Sending
```
User types + clicks Send
  ↓
ChatInput.OnSend (EventCallback)
  ↓
Chat.AddUserMessageAsync()
  ├─ Check if can queue response
  ├─ Cancel previous response
  ├─ Initialize recipe state if needed
  └─ Dispatch SessionActions.AddMessageAction
  ↓
Fluxor Store Updated
  ↓
StreamingService.ProcessAgentResponseAsync()
  ├─ Check if running/queued
  ├─ Start or queue request
  └─ Return task
```

### 2. Streaming Response
```
RunSessionResponseAsync()
  ├─ Create IChatClient
  ├─ Loop: await GetStreamingResponseAsync()
  │  ├─ Process each update
  │  ├─ Handle FunctionCallContent
  │  │  ├─ Check if approval needed
  │  │  ├─ If yes: notify user, wait for decision
  │  │  └─ If no: continue
  │  ├─ Handle DataContent
  │  ├─ Handle FunctionResultContent
  │  │  ├─ Check if visual component
  │  │  └─ Dispatch artifact action if needed
  │  ├─ Accumulate TextContent
  │  ├─ Update UI (throttled)
  │  └─ Notify observability service
  ├─ On completion:
  │  ├─ Add final message to session
  │  ├─ Promote queued request
  │  └─ Notify completion
  └─ On error:
     ├─ Capture error
     ├─ Create error notification
     └─ Add to message
```

### 3. Approval Handling
```
FunctionCallContent detected as approval request
  ├─ Extract PendingApproval
  ├─ Dispatch SetPendingApprovalAction
  ├─ Notify background (UI shows prompt)
  ├─ Create TaskCompletionSource
  ├─ Await user decision
  ├─ User clicks Approve/Reject
  ├─ ResolveApproval() called
  ├─ Create FunctionResultContent with decision
  ├─ Add to message
  ├─ Dispatch SetPendingApprovalAction(null)
  └─ Resume streaming
```

### 4. Message Rendering
```
ChatMessageItem.razor receives Message
  ├─ Sort Contents by priority
  ├─ If User message:
  │  ├─ Display text
  │  ├─ Show edit button on hover
  │  ├─ Show branch navigation if siblings
  │  └─ Show attachments (proposed)
  └─ If Assistant message:
     ├─ Separate visual from non-visual
     ├─ Show AssistantThought (collapsible) for non-visual
     ├─ Render visual components (MessageList location)
     ├─ Render TextContent as markdown
     └─ Show branch navigation if siblings

ChatMessageItem.razor.css applies styling
  ├─ Message bubble colors
  ├─ Tool call styling (violet)
  ├─ Tool result styling (green)
  ├─ Markdown formatting
  └─ Responsive layout
```

---

## Concurrency Management

### Stream Limits
- **Max Concurrent**: 3 streams running simultaneously
- **Max Queued**: 5 requests waiting in queue

### Queue Behavior
```
Request arrives
  ├─ If running < 3 → start immediately
  ├─ If running >= 3 AND queue < 5 → queue request
  ├─ If running >= 3 AND queue >= 5 → reject with error
  └─ If already running → return existing task

On stream completion
  ├─ Remove from _runningSessions
  ├─ Check _queuedRequests
  ├─ Promote first queued request
  └─ Start new stream
```

---

## Environment Variables

### Required (one of):
- `OPENAI_API_KEY` + `OPENAI_MODEL` (OpenAI)
- `AZURE_OPENAI_ENDPOINT` + `AZURE_OPENAI_DEPLOYMENT_NAME` (Azure OpenAI)

### Optional:
- `ASPNETCORE_ENVIRONMENT` (Development, Production)
- `ASPNETCORE_URLS` (Server URLs)
- `Jwt__SigningKey` (JWT signing key for auth)

---

## API Endpoints

| Method | Path | Purpose |
|--------|------|---------|
| POST | /api/title | Generate session title from messages |
| POST | /api/weather | Get weather for location |
| POST | /api/email | Send email (requires approval) |
| POST | /api/auth/token | Generate JWT token (dev only) |
| GET | /chat | AG-UI SSE streaming endpoint |
| GET | /health | Health check aggregate |

---

## Services & Dependencies

### Singleton
- ChatClientAgentFactory
- WeatherTool, EmailTool, DocumentTool
- IChatClient

### Scoped
- IWeatherService
- IEmailService
- IDocumentService
- IApprovalHandler
- ICheckpointService

### Transient
- SessionStreamingContext (per session)
- ChatMessage updates
- Chat updates

---

## Styling System

### CSS Variables Used
```
--background          : Main background color
--foreground          : Main text color
--primary             : Brand primary color
--muted               : Secondary background
--border              : Border color
--ring                : Focus ring color
--accent-violet       : Tool calls/highlights
--alert-success       : Success/results
--alert-danger        : Errors/failures
--alert-warning       : Warnings/info
--accent-amber        : Weather/neutral info
```

### Responsive
- Desktop: Full dual-pane layout
- Mobile: Collapsible sidebar, single-pane default
- ViewportService tracks changes

---

## Key Integration Points for Multimodal

1. **ChatInput.razor** - Add file input, preview attachments
2. **ChatMessage.Contents** - Add AttachmentContent type
3. **ChatMessageItem.razor** - Render attachment previews
4. **AgentStreamingService** - Handle AttachmentContent in streaming
5. **TitleEndpoints.cs** - Include attachment names in title generation
6. **ChatClientAgentFactory** - Update system prompt with multimodal guidelines
7. **Backend** - Add /api/attachments endpoints (optional)

---

## Common Tasks

### Add New Tool
1. Create Tool class (DI-compatible)
2. Register in Program.cs as Singleton
3. Create AITool in ChatClientAgentFactory.CreateUnifiedChatTools()
4. Register component in ToolComponentRegistry (if visual)
5. Add to AssistantThought for rendering

### Add New Artifact Type
1. Create model in Models/
2. Create action in SessionActions.cs
3. Create Blazor component for rendering
4. Register in ToolComponentRegistry
5. Add case to ChatMessageItem.IsVisualComponent()

### Add New API Endpoint
1. Create Endpoints class (e.g., MyEndpoints.cs)
2. Implement MapMyEndpoints() extension
3. Register in Program.cs: apiGroup.MapMyEndpoints()
4. Add DTO models and validation

---

## Performance Considerations

### Throttling
- State updates throttled to 100ms (prevents excessive re-renders)
- Pending state change triggers on elapsed

### Message Processing
- Content priority sort optimizes rendering order
- Visual components detected at render time (cached via registry)
- Markdown conversion on-demand

### Concurrency
- Max 3 concurrent streams prevents overload
- Queue prevents request rejection
- Session cleanup removes abandoned contexts

### Observability
- OpenTelemetry tracing enabled
- Tool call tracking via ReasoningStep
- Error notifications for user feedback

