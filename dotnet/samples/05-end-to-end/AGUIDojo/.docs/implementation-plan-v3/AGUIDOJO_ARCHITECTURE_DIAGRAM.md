# AGUIDojo Architecture & Component Relationships

## Component Hierarchy

```
Chat.razor (Main Page)
│
├── ChatHeader
│   ├── Session management
│   └── New session creation
│
├── BbSidebarInset
│   └── DualPaneLayout
│       │
│       ├── ContextPaneContent
│       │   ├── ContextPane
│       │   │   ├── ChatMessageList
│       │   │   │   └── ChatMessageItem (for each message)
│       │   │   │       ├── Branch Navigation
│       │   │   │       ├── Edit Mode
│       │   │   │       ├── AssistantThought (for non-visual content)
│       │   │   │       └── Visual Components (DynamicComponent)
│       │   │   │
│       │   │   ├── ChatSuggestions
│       │   │   └── ChatInput
│       │   │       ├── Textarea (auto-resize via JS)
│       │   │       ├── PanicButton
│       │   │       └── Send Button
│       │   │
│       │   └── chat-container
│       │
│       └── CanvasPaneContent
│           └── CanvasPane (with BB Tabs)
│               ├── DiffPreview
│               ├── RecipeEditor
│               ├── DocumentPreview
│               └── DataGrid
```

## Data Flow Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        USER INTERACTION                         │
└─────────────────────────────────────────────────────────────────┘
                              ↓
                    ChatInput.razor
                  (OnSend EventCallback)
                              ↓
         ┌────────────────────┴────────────────────┐
         ↓                                         ↓
   Add Message to                        StreamingService
   Chat.razor state                  ProcessAgentResponseAsync()
         ↓                                         ↓
   Dispatch                            Can Queue Response?
   SessionActions                           │
   .AddMessageAction                       ├─ Running < 3? → Start
         ↓                                 ├─ Already Running? → Return
         ↓                                 ├─ Queue Full? → Error
   ┌─────────────────────────────────┐   └─ Queue? → Queue & Return
   │  Fluxor Store                   │           ↓
   │ (SessionManagerState)           │   RunSessionResponseAsync()
   │                                 │           ↓
   │ - Sessions                      │   Create IChatClient
   │ - Active Session ID             │           ↓
   │ - Messages                      │   Await GetStreamingResponseAsync()
   │ - Running State                 │           ↓
   │ - Current Response              │   ┌──────────────────────┐
   │ - Pending Approval              │   │  Stream Loop         │
   │ - Artifacts                     │   ├──────────────────────┤
   │ - Plan State                    │   │ For each Update:     │
   │ - Document State                │   │ - Process Contents   │
   └─────────────────────────────────┘   │ - Handle Approvals   │
         ↑                                │ - Update State       │
         │                                │ - Dispatch Actions   │
         │                                │ - Notify UI          │
         └────────────────────────────────┘
              State Changed
              StateHasChanged()
              ↓
   ChatMessageItem.razor Re-renders
   ↓
   SortContentsForDisplay()
   ↓
   Priority Sort:
   1. FunctionCallContent
   2. FunctionResultContent
   3. DataContent
   4. TextContent
   5. ErrorContent
   ↓
   Separate Visual from Thoughts
   ↓
   ├─ Visual → DynamicComponent (MessageList location)
   ├─ Thoughts → AssistantThought
   └─ Text → Markdown HTML
   ↓
   ChatMessageItem.razor.css
   ↓
   Rendered Message in Chat UI
```

## Service Layer Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                     DI Container                             │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│ SINGLETON:                                                  │
│ ├── ChatClientAgentFactory                                  │
│ │   ├── Creates ChatClient (OpenAI or Azure OpenAI)         │
│ │   ├── Creates AIAgent with:                               │
│ │   │   ├── Tools (Weather, Email, Document, etc.)          │
│ │   │   ├── Wrappers (Approval, AgenticUI, State, etc.)     │
│ │   │   └── OpenTelemetry instrumentation                   │
│ │   └── Returns IChatClient for title generation            │
│ │                                                           │
│ ├── WeatherTool (uses IHttpContextAccessor)                │
│ ├── EmailTool (uses IHttpContextAccessor)                  │
│ └── DocumentTool (uses IHttpContextAccessor)               │
│                                                              │
│ SCOPED:                                                     │
│ ├── IWeatherService                                         │
│ ├── IEmailService                                           │
│ └── IDocumentService                                        │
│                                                              │
│ TRANSIENT:                                                  │
│ ├── SessionStreamingContext (per session)                   │
│ └── ChatMessage updates                                     │
│                                                              │
│ SPECIAL:                                                    │
│ ├── IDispatcher (Fluxor)                                    │
│ ├── IState<SessionManagerState> (Fluxor)                   │
│ ├── IObservabilityService (observability)                   │
│ ├── IApprovalHandler (approval logic)                       │
│ ├── ICheckpointService (state snapshots)                    │
│ └── IStateManager (recipe/plan state)                       │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

## Message Content Type Processing

```
ChatMessage.Contents Array
│
├── FunctionCallContent (Priority: 1)
│   ├── Name: Tool name
│   ├── CallId: Unique ID
│   ├── Arguments: JSON
│   └── → Show in AssistantThought OR MessageList
│
├── FunctionResultContent (Priority: 2)
│   ├── CallId: Matches FunctionCallContent
│   ├── Result: JSON
│   ├── IsApprovalResponse: bool
│   ├── → Check registry for visual component
│   └── → Show in MessageList if visual, else AssistantThought
│
├── DataContent (Priority: 3)
│   ├── Type: "state_snapshot", "state_delta", etc.
│   ├── Data: JSON
│   └── → Update StateManager, show in thought
│
├── TextContent (Priority: 4)
│   ├── Text: Markdown string
│   └── → Render as markdown HTML in message
│
├── ErrorContent (Priority: 5)
│   ├── ErrorCode: string
│   ├── Details: string
│   └── → Show in AssistantThought with red styling
│
└── AttachmentContent (FUTURE)
    ├── FileType: string
    ├── FileData: byte[]
    ├── FileName: string
    └── → Show in message with preview
```

## Approval Flow

```
FunctionCallContent with Approval Marker
│
├── ApprovalHandler.TryExtractApprovalRequest()
│   │
│   └─ Create PendingApproval object
│
├── Dispatcher.Dispatch(SetPendingApprovalAction)
│   │
│   └─ Update Fluxor Store
│
├── NotifyBackgroundApprovalRequired()
│   │
│   └─ Add SessionNotification
│
├── UI Shows Approval Widget
│   │
│   └─ User clicks Approve/Reject
│
├── StreamingService.ResolveApproval(sessionId, approved)
│   │
│   └─ Set TaskCompletionSource result
│
├── Streaming resumes with decision
│   │
│   └─ ApprovalHandler.CreateApprovalResponse()
│
├── FunctionResultContent added to message
│   │
│   └─ Contains approval decision
│
└─ Dispatch SetPendingApprovalAction(null) to clear
```

## Session Lifecycle

```
CREATE SESSION
│
├── SessionActions.CreateSessionAction(sessionId, title, endpoint)
│   └─ Fluxor creates new SessionEntry
│
├── Add System Prompt
│   └─ SessionActions.AddMessageAction(sessionId, systemPrompt)
│
├── Display Suggestions
│   └─ ChatSuggestions.Update(messages)
│
ACTIVE SESSION
│
├── User sends messages
│   └─ SessionActions.AddMessageAction
│
├── Stream responses
│   └─ SessionActions.UpdateResponseMessageAction
│
├── Update conversation ID
│   └─ SessionActions.SetConversationIdAction
│
├── Handle approvals
│   └─ SessionActions.SetPendingApprovalAction
│
├── Update artifacts
│   ├─ SessionActions.SetPlanAction
│   ├─ SessionActions.SetDocumentAction
│   ├─ SessionActions.SetRecipeAction
│   └─ SessionActions.SetDataGridArtifactAction
│
ARCHIVE/DELETE SESSION
│
├── SessionActions.ArchiveSessionAction(sessionId)
│   OR
├── SessionActions.DeleteSessionAction(sessionId)
│
└─ Remove context from AgentStreamingService._sessionContexts
```

## Tool Invocation Pipeline

```
IChatClient.GetStreamingResponseAsync()
│
├── Agent processes message with tools
│   │
│   ├── Determines which tool to call (e.g., get_weather)
│   │
│   ├── Creates FunctionCallContent
│   │   ├── Name: "get_weather"
│   │   ├── CallId: "call-123"
│   │   ├── Arguments: { location: "Seattle" }
│   │   └─ Sent in stream update
│   │
│   └─ Tool executes (WeatherTool.GetWeatherAsync)
│
├── StreamingService captures FunctionCallContent
│   │
│   ├── Check if approval required
│   │   ├─ If yes: Notify user, wait for approval
│   │   └─ If no: Continue
│   │
│   ├── Add to streamingMessage.Contents
│   │
│   └─ Call _observabilityService.StartToolCall()
│
├── Tool returns result
│   │
│   ├── Agent creates FunctionResultContent
│   │   ├── CallId: "call-123" (matches)
│   │   ├── Result: { temperature: 72, ... }
│   │   └─ Sent in stream update
│   │
│   └─ Observer continues chain-of-thought
│
├── StreamingService captures FunctionResultContent
│   │
│   ├── Check for visual component
│   │   ├─ ToolRegistry.TryGetComponent("get_weather")
│   │   └─ Found: WeatherDisplay component
│   │
│   ├── Add to streamingMessage.Contents
│   │
│   ├── Call _observabilityService.CompleteToolCall()
│   │
│   └─ Dispatch SetDataGridArtifactAction (if applicable)
│
├── ChatMessageItem.razor renders
│   │
│   ├── Find FunctionResultContent
│   │
│   ├── Get tool name from CallId lookup
│   │
│   ├── Check IsVisualComponent()
│   │   ├─ Find tool metadata in registry
│   │   └─ Check RenderLocation == MessageList && IsVisual
│   │
│   ├── If visual: Render via DynamicComponent
│   │   └─ <DynamicComponent Type="WeatherDisplay" Parameters="weather" />
│   │
│   └─ If not visual: Render in AssistantThought component
│
└─ Agent continues with next step or final response
```

## Session Concurrency Management

```
MaxConcurrentStreams: 3
MaxQueuedStreams: 5

INCOMING REQUEST for sessionId
│
├─ Check CanQueueResponse()
│  │
│  ├─ If session already running? → return true (will wait)
│  ├─ If session already queued? → return true (wait for queue)
│  ├─ If running < 3? → return true (can start immediately)
│  ├─ If queue < 5? → return true (can queue)
│  └─ Else → return false (reject)
│
└─ ProcessAgentResponseAsync(sessionId)
   │
   ├─ If already running → return existing task
   ├─ If already queued → return completed task
   ├─ If running >= 3 → queue request, return
   └─ Else → start stream immediately
      │
      ├─ Add to _runningSessions
      ├─ Increment active count
      └─ Start RunSessionResponseAsync()
         │
         ├─ Stream responses (throttle updates)
         │
         ├─ On completion:
         │  ├─ Remove from _runningSessions
         │  ├─ Decrement active count
         │  ├─ Check _queuedRequests
         │  └─ Promote first queued request (if any)
         │
         └─ On error:
            ├─ Remove from _runningSessions
            ├─ Add error notification
            └─ Promote queued request
```

## Widget Architecture

```
ChatMessageItem.razor
│
├─ USER MESSAGE
│  ├─ Text display
│  ├─ Edit button (hover)
│  ├─ Edit mode (textarea + Save/Cancel)
│  └─ Branch navigation (if siblings)
│
└─ ASSISTANT MESSAGE
   │
   ├─ Branch navigation (if siblings)
   │
   ├─ AssistantThought (collapsible)
   │  ├─ FunctionCallContent
   │  │  ├─ Tool name
   │  │  ├─ Arguments (JSON)
   │  │  └─ Status badge
   │  ├─ FunctionResultContent (if not visual)
   │  ├─ ErrorContent
   │  └─ DataContent (thoughts)
   │
   ├─ Visual Components (MessageList)
   │  └─ DynamicComponent instances
   │     ├─ WeatherDisplay (weather data)
   │     ├─ RecipeEditor (recipe plan)
   │     ├─ DocumentPreview (document)
   │     ├─ DataGrid (tabular data)
   │     └─ ChartComponent (visualizations)
   │
   └─ TextContent
      ├─ Markdown → HTML
      ├─ Code highlighting
      ├─ Tables
      ├─ Links
      └─ Formatting (bold, italic, blockquotes)
```

---

## Key Integration Points for Multimodal Support

```
MULTIMODAL ATTACHMENT FLOW (PROPOSED)

ChatInput.razor
├─ Add <input type="file" multiple>
├─ Handle onChange event
├─ Create AttachmentContent for each file
│  ├─ FileType: "image/png", "application/pdf", etc.
│  ├─ FileData: byte[] or URL reference
│  ├─ FileName: "document.pdf"
│  └─ Size: bytes
└─ Pass as ChatMessage.Contents in OnSend
   │
   ├─ dispatch SessionActions.AddMessageAction()
   │  │
   │  └─ Include AttachmentContent in message
   │
   └─ Send to agent with message
      │
      ├─ Agent receives multimodal message
      │
      ├─ Tool can reference attachment (if API supports)
      │
      └─ Response includes reference to processed attachment

ChatMessageItem.razor
├─ Add AttachmentContent to priority sort (2.5?)
├─ Render attachment preview
│  ├─ Images: <img src="..." alt="attachment name" />
│  ├─ PDF/Docs: Embed viewer or link to download
│  └─ Video/Audio: <video> or <audio> element
└─ Styling via ChatMessageItem.razor.css
   └─ .attachment-preview class

TitleEndpoints.cs
├─ Update title generation to include attachment metadata
│  ├─ "Discussing image: document.pdf"
│  └─ Consider file names in title context
└─ Pass attachment names/types to LLM

ChatClientAgentFactory.cs
├─ Verify tools support multimodal content
├─ Update system prompt with multimodal guidelines
│  └─ "When user shares attachments, analyze them using provided tools"
└─ Add tools for attachment processing (if needed)
   ├─ analyze_image
   ├─ extract_text_from_pdf
   └─ process_document
```

