# Design Section 01: Unified Agent Endpoint Architecture

> **Spec Section**: Server-Side Architecture — Unified Endpoint  
> **Addresses**: ISS-003, ISS-004, ISS-007, ISS-008, ISS-009, ISS-010, ISS-011, ISS-013, ISS-020, ISS-033, ISS-051  
> **Grounded In**: Q-UNIFY-001, Q-UNIFY-002, Q-UNIFY-003, Q-UNIFY-005, Q-UNIFY-006, Q-AGUI-004, R1, R2, R3, R11, R12  
> **Inherited By**: task-7, task-9

---

## 1. Current State: 10-Endpoint Architecture (Before)

The current AGUIDojoServer maps **10 separate `MapAGUI` endpoints**, each backed by a dedicated `AIAgent` instance created at startup by `ChatClientAgentFactory` (singleton). Every agent has its own system prompt, tool set, and optional `DelegatingAIAgent` wrapper.

### 1.1 Endpoint Inventory

| # | Endpoint Path | Factory Method | Tools | Wrapper | System Prompt Focus |
|---|---------------|---------------|-------|---------|---------------------|
| 1 | `/agentic_chat` | `CreateAgenticChat()` | — | None | General conversation |
| 2 | `/backend_tool_rendering` | `CreateBackendToolRendering()` | `get_weather` | `ToolResultStreamingChatClient` (IChatClient-level) | Tool use with backend rendering |
| 3 | `/human_in_the_loop` | `CreateHumanInTheLoop(json)` | `send_email` (approval-wrapped) | `ServerFunctionApprovalAgent` | Email with HITL approval |
| 4 | `/tool_based_generative_ui` | `CreateToolBasedGenerativeUI()` | `get_weather` | None | Weather tool → client renders component |
| 5 | `/agentic_generative_ui` | `CreateAgenticUI(json)` | `create_plan`, `update_plan_step` | `AgenticUIAgent` | Planning with agentic UI state |
| 6 | `/shared_state` | `CreateSharedState(json)` | — | `SharedStateAgent` | Recipe state management |
| 7 | `/predictive_state_updates` | `CreatePredictiveStateUpdates(json)` | `write_document` | `PredictiveStateUpdatesAgent` | Document editing with progressive preview |
| 8 | `/data_grid` | `CreateDataGrid()` | `show_data_grid` | None | Tabular data visualization |
| 9 | `/chart` | `CreateChart()` | `show_chart` | None | Chart data visualization |
| 10 | `/dynamic_form` | `CreateDynamicForm()` | `show_form` | None | Dynamic form generation |

*(Source: R1, `Program.cs` lines 462-490, `ChatClientAgentFactory.cs`)*

### 1.2 Problems with the Multi-Endpoint Architecture

1. **Client-side complexity**: `AGUIChatClientFactory` maintains a hardcoded list of 10 endpoint paths. The client must select the correct endpoint for each feature, tightly coupling client UX to server topology. (ISS-003, ISS-051, R7)
2. **Feature isolation**: Each endpoint demonstrates a single AG-UI capability in isolation. A real agentic chat app needs all capabilities simultaneously — the user shouldn't choose which "feature" they're using. (ISS-004)
3. **Duplicated base configuration**: All 10 agents share the same `ChatClient` but duplicate OpenTelemetry wrapping, instructions boilerplate, and tool registration patterns. (R1)
4. **No wrapper composition**: Each agent applies at most **one** custom wrapper. The MAF builder pattern supports arbitrary stacking (R2), but this capability is unexploited. (ISS-004)
5. **Inconsistent `ToolResultStreamingChatClient` application**: Only endpoint #2 (`/backend_tool_rendering`) applies `ToolResultStreamingChatClient`. Without it, tool results are invisible to the AG-UI streaming layer. (ISS-007, R11)

### 1.3 Four Custom Agent Wrappers

The codebase defines 4 `DelegatingAIAgent` subclasses, each intercepting specific content types:

| Wrapper | Intercepts | Emits | Invocation Pattern |
|---------|-----------|-------|-------------------|
| `ServerFunctionApprovalAgent` | `FunctionApprovalRequestContent`, `FunctionApprovalResponseContent` | Transforms to/from AG-UI `request_approval` tool call format | **Passthrough** — single `InnerAgent.RunStreamingAsync()` call |
| `AgenticUIAgent` | `FunctionResultContent` from `create_plan` / `update_plan_step` | `DataContent` snapshots (`application/json`) and deltas (`application/json-patch+json`) | **Passthrough** — single `InnerAgent.RunStreamingAsync()` call, post-processes results |
| `PredictiveStateUpdatesAgent` | `FunctionCallContent` where `Name == "write_document"` | Progressive `DataContent` snapshots (10-char chunks, 50ms delay) | **Passthrough** — single `InnerAgent.RunStreamingAsync()` call, intercepts tool args |
| `SharedStateAgent` | `ag_ui_state` from `AdditionalProperties` | `DataContent` state snapshot | **Double invocation** — runs `InnerAgent.RunStreamingAsync()` TWICE (state update + summary) |

*(Source: R1, R2, wrapper source files in AGUIDojoServer/)*

---

## 2. Target State: Single Unified Endpoint (After)

### 2.1 Endpoint Design

Replace all 10 `MapAGUI` calls with a single unified endpoint:

```
POST /chat  →  SSE response (AG-UI event stream)
```

The unified agent handles **all** AG-UI features through one pipeline:
- General conversation (agentic chat)
- Backend tool execution with result streaming
- Human-in-the-loop approval
- Agentic generative UI (plan state)
- Tool-based generative UI (weather, chart, data grid, form components)
- Shared state management (recipe)
- Predictive state updates (document editing)

### 2.2 Why Unified?

| Concern | Multi-Endpoint | Unified Endpoint |
|---------|---------------|-----------------|
| Client routing | Client must select endpoint per feature | Single endpoint; LLM selects tools contextually |
| Feature mixing | Cannot combine chat + tools + planning in one conversation | All capabilities available in every conversation |
| Wrapper composition | One wrapper per agent maximum | All wrappers composed in a single pipeline |
| Multi-session | Each session must know its endpoint path | All sessions use the same `/chat` endpoint |
| Tool result streaming | Applied to 1/10 agents | Applied globally — all tools benefit |

*(Source: ISS-003, ISS-004, ISS-051, Q-UNIFY-001)*

---

## 3. Wrapper Composition Order

### 3.1 Pipeline Topology

The MAF `AIAgentBuilder.Use()` method applies factories in **reverse order** — the first `Use()` call registers the outermost wrapper. (R2)

The unified agent pipeline, from outermost to innermost:

```
Request →
  ① ServerFunctionApprovalAgent  (outermost — intercepts approval events first)
    ② AgenticUIAgent               (intercepts plan tool results after approval)
      ③ PredictiveStateUpdatesAgent  (intercepts document tool calls)
        ④ SharedStateAgent             (reads ag_ui_state, may double-invoke)
          ⑤ OpenTelemetry               (traces the base agent invocation)
            ⑥ ChatClientAgent             (base — calls LLM via ToolResultStreamingChatClient-wrapped IChatClient)
              ⑦ ToolResultStreamingChatClient  (IChatClient-level — emits FunctionResultContent to stream)
                ⑧ FunctionInvokingChatClient     (auto tool invocation)
                  ⑨ ChatClient                     (LLM API)
```

### 3.2 Ordering Rationale

| Position | Wrapper | Rationale |
|----------|---------|-----------|
| ① Outermost | `ServerFunctionApprovalAgent` | Must intercept `FunctionApprovalRequestContent` BEFORE any inner wrapper processes the tool result. If placed inner, approval-required tool calls could be erroneously processed by `AgenticUIAgent` or `PredictiveStateUpdatesAgent`. |
| ② | `AgenticUIAgent` | Intercepts `FunctionResultContent` from `create_plan`/`update_plan_step` and emits `DataContent`. Must be outer to `SharedStateAgent` to avoid the double-invocation seeing plan state events twice. |
| ③ | `PredictiveStateUpdatesAgent` | Intercepts `FunctionCallContent` for `write_document`. Scoped by tool name — only activates on `write_document` calls (Q-AGUI-004). Safe to compose with other wrappers since it doesn't interfere with non-document tools. |
| ④ Innermost wrapper | `SharedStateAgent` | Reads `ag_ui_state` from `AdditionalProperties` and may invoke the inner agent twice. Placed innermost among wrappers so the double-invocation only affects the base agent + OTel layer, not other wrappers. |
| ⑤ | OpenTelemetry | Instruments the base agent calls. Inside SharedStateAgent so both invocations are traced. |
| ⑥ | `ChatClientAgent` (base) | Created via `IChatClient.AsAIAgent()` with unified tools and system prompt. |
| ⑦ | `ToolResultStreamingChatClient` | IChatClient-level wrapper. Applied globally so ALL tool results (weather, email, document, charts, forms, data grids) are emitted to the AG-UI event stream. (ISS-007, R11, Q-UNIFY-003) |

### 3.3 Builder Composition (Pseudo-code)

```csharp
// IChatClient pipeline: ToolResultStreamingChatClient wraps the LLM client
IChatClient wrappedClient = new ToolResultStreamingChatClient(chatClient.AsIChatClient());

// Base agent with ALL tools and unified system prompt
AIAgent baseAgent = wrappedClient.AsAIAgent(new ChatClientAgentOptions
{
    Name = "UnifiedAgent",
    Description = "Unified agentic chat agent with all AG-UI capabilities",
    ChatOptions = new ChatOptions
    {
        Instructions = unifiedSystemPrompt,
        Tools = allTools  // See §5 for full tool list
    }
});

// Wrapper composition: first Use() becomes the outermost wrapper
AIAgent unifiedAgent = baseAgent
    .AsBuilder()
    .Use(inner => new ServerFunctionApprovalAgent(inner, jsonOptions)) // ① outermost
    .Use(inner => new AgenticUIAgent(inner, jsonOptions))             // ②
    .Use(inner => new PredictiveStateUpdatesAgent(inner, jsonOptions)) // ③
    .Use(inner => new SharedStateAgent(inner, jsonOptions))           // ④ innermost wrapper
    .UseOpenTelemetry(SourceName)                                     // ⑤ traces base
    .Build();

// Single endpoint
app.MapAGUI("/chat", unifiedAgent);
```

> **Note**: `AIAgentBuilder.Build()` applies factories in reverse order, so the first `Use()` in source order becomes the **outermost** wrapper at runtime. The calls above are listed outermost-first: `ServerFunctionApprovalAgent` (①, outermost) down to `SharedStateAgent` (④, innermost wrapper). (R2)

---

## 4. SharedStateAgent Double-Invocation Challenge

### 4.1 The Problem

`SharedStateAgent` is the only wrapper that runs `InnerAgent.RunStreamingAsync()` **twice** per request:

1. **First invocation**: Appends a system message with current state + JSON schema response format → agent returns structured JSON state update
2. **Second invocation**: Appends the state update to message history + asks for a summary → agent returns natural language summary

When `SharedStateAgent` is composed with outer wrappers, those outer wrappers see **both** invocation streams. This causes:

- `AgenticUIAgent` (outer) would process `FunctionResultContent` from both invocations — potentially emitting duplicate plan state events
- `ServerFunctionApprovalAgent` (outer) would see approval requests from both invocations — potentially double-prompting the user
- `PredictiveStateUpdatesAgent` (outer) would intercept `write_document` calls from both invocations — emitting duplicate document previews

### 4.2 Why This Doesn't Break Today

In the current 10-endpoint architecture, `SharedStateAgent` has **no outer wrappers** — it wraps the base agent directly. The only layer above it is OpenTelemetry (which is a transparent pass-through). The double-invocation is invisible.

### 4.3 Proposed Solution: Invocation Context Flag

Refactor `SharedStateAgent` to tag each invocation with a context flag that outer wrappers can check:

**Strategy**: SharedStateAgent places a sentinel key in `AdditionalProperties` during the first (state-update) invocation:

```
AdditionalProperties["ag_ui_shared_state_phase"] = "state_update"  // first invocation
AdditionalProperties["ag_ui_shared_state_phase"] = "summary"       // second invocation (or absent)
```

Outer wrappers check this flag and **skip their interception logic** during the `"state_update"` phase:

- `AgenticUIAgent`: If `phase == "state_update"`, pass through without intercepting `FunctionResultContent`
- `PredictiveStateUpdatesAgent`: If `phase == "state_update"`, pass through without intercepting `FunctionCallContent`
- `ServerFunctionApprovalAgent`: If `phase == "state_update"`, pass through without transforming approval content

**Rationale**: The first invocation uses a JSON schema response format and is not expected to invoke tools. It's a state-extraction call, not a conversational turn. Outer wrappers have no meaningful work to do on it.

### 4.4 Alternative Considered: Refactor SharedStateAgent to Single Invocation

An alternative is to refactor `SharedStateAgent` to use a single invocation with a compound prompt that returns both state update AND summary. This eliminates the double-invocation entirely but:

- Requires more complex prompt engineering to reliably get both structured JSON and natural language in one response
- May degrade state update accuracy if the model conflates JSON output with prose
- Changes the tested behavior of the current SharedStateAgent

**Recommendation**: Use the invocation context flag (§4.3) for v2 since it preserves the proven two-invocation pattern while enabling safe composition. Revisit single-invocation in a future iteration if prompt engineering proves robust enough.

---

## 5. Unified Tool Registry

### 5.1 Complete Tool List

The unified agent registers **8 tools** (as `AIFunction` instances) from across the 10 current endpoints. Each tool's description is the primary mechanism for LLM routing (Q-UNIFY-001). Two additional mechanisms (`get_recipe` via SharedStateAgent and `confirm_changes` via prompt reference) are listed for completeness but are not discrete registered tools.

| # | Tool Name | Function | Category | Wrapper Interaction | Description (LLM sees this) |
|---|-----------|----------|----------|--------------------|-----------------------------|
| 1 | `get_weather` | `WeatherTool.GetWeatherAsync` | Data query | `ToolResultStreamingChatClient` emits result | Get the weather for a given location. |
| 2 | `send_email` | `EmailTool.SendEmailAsync` (approval-wrapped) | Action (HITL) | `ServerFunctionApprovalAgent` transforms approval flow | Send an email to a recipient. Requires user approval. |
| 3 | `write_document` | `DocumentTool.WriteDocumentAsync` | Content creation | `PredictiveStateUpdatesAgent` streams progressive preview | Write a document. Use markdown formatting. |
| 4 | `create_plan` | `AgenticPlanningTools.CreatePlan` | Planning | `AgenticUIAgent` emits plan snapshot | Create a plan with multiple steps. |
| 5 | `update_plan_step` | `AgenticPlanningTools.UpdatePlanStepAsync` | Planning | `AgenticUIAgent` emits plan delta | Update a step in the plan with new description or status. |
| 6 | `show_chart` | `ChartTool.ShowChartAsync` | Visualization | `ToolResultStreamingChatClient` emits result | Show data as a chart visualization. |
| 7 | `show_data_grid` | `DataGridTool.ShowDataGridAsync` | Visualization | `ToolResultStreamingChatClient` emits result | Show structured data in a rich table view. |
| 8 | `show_form` | `DynamicFormTool.ShowFormAsync` | UI generation | `ToolResultStreamingChatClient` emits result | Show a dynamic form for user input. |
| 9 | `get_recipe` | *(implicit via SharedStateAgent)* | Shared state | `SharedStateAgent` reads `ag_ui_state` | *(Not a discrete tool — state flows via AdditionalProperties)* |
| 10 | `confirm_changes` | *(referenced in PredictiveStateUpdates prompt)* | Confirmation | None | *(Not a registered tool — removed from unified system prompt. PredictiveStateUpdatesAgent handles confirmation flow internally.)* |

> **Note on tools 9-10**: `SharedStateAgent` does not register explicit tools — it operates on `ag_ui_state` from `AdditionalProperties`. `confirm_changes` was referenced in per-endpoint prompts but is not registered as an `AIFunction` in the unified agent; the confirmation flow is handled internally by `PredictiveStateUpdatesAgent`. It has been removed from the unified system prompt (§6.2) to avoid referencing a non-existent tool.

### 5.2 Tool Registration (Pseudo-code)

```csharp
AITool[] allTools =
[
    // Data query tools
    AIFunctionFactory.Create(weatherTool.GetWeatherAsync,
        name: "get_weather",
        description: "Get the weather for a given location.",
        serializerOptions),

    // Action tools (HITL)
    new ApprovalRequiredAIFunction(
        AIFunctionFactory.Create(emailTool.SendEmailAsync,
            name: "send_email",
            description: "Send an email to a recipient. Requires user approval before sending.",
            serializerOptions)),

    // Content creation tools
    AIFunctionFactory.Create(documentTool.WriteDocumentAsync,
        name: "write_document",
        description: "Write or edit a document. Use markdown formatting. Always write the full document.",
        serializerOptions),

    // Planning tools
    AIFunctionFactory.Create(AgenticPlanningTools.CreatePlan,
        name: "create_plan",
        description: "Create a plan with multiple steps for task execution.",
        serializerOptions),
    AIFunctionFactory.Create(AgenticPlanningTools.UpdatePlanStepAsync,
        name: "update_plan_step",
        description: "Update a step in an existing plan with new description or status.",
        serializerOptions),

    // Visualization tools
    AIFunctionFactory.Create(ChartTool.ShowChartAsync,
        name: "show_chart",
        description: "Show data as a chart (bar, line, pie, area). Use for trends, comparisons, distributions.",
        serializerOptions),
    AIFunctionFactory.Create(DataGridTool.ShowDataGridAsync,
        name: "show_data_grid",
        description: "Show structured data in a rich table view. Use for lists, inventories, tabular data.",
        serializerOptions),
    AIFunctionFactory.Create(DynamicFormTool.ShowFormAsync,
        name: "show_form",
        description: "Show a dynamic form for user input. Use for registrations, feedback, orders.",
        serializerOptions),
];
```

### 5.3 Tool-as-Router Strategy

With 8 registered tools, the LLM's native tool selection acts as the router (R9). No explicit routing logic or workflow engine is needed. Tool selection accuracy depends on:

1. **Clear, non-overlapping tool descriptions** — Each description specifies when to use the tool (see table above). Descriptions should be mutually exclusive in intent.
2. **Structured system prompt** — The unified prompt (§6) provides high-level behavioral guidance without repeating tool descriptions.
3. **Tool categories** — Semantic grouping (data query, action, content creation, planning, visualization) helps the LLM disambiguate. The descriptions implicitly encode these categories.

*(Source: Q-UNIFY-001, R9)*

---

## 6. Unified System Prompt

### 6.1 Design Principles

The unified system prompt must NOT be a concatenation of all 10 individual prompts. Instead, it follows a **structured prompt** pattern with:

1. **Identity & behavior** — Who the agent is, general conversation style
2. **Tool routing guidance** — When to use which category of tools (without duplicating tool descriptions)
3. **Feature-specific rules** — Conditional instructions that activate based on tool usage
4. **Output formatting** — Markdown formatting, response length guidance

### 6.2 Proposed Unified System Prompt

```text
You are a versatile AI assistant that helps users with conversations, data queries, 
document editing, planning, and data visualization. Format responses in markdown.

## Tool Usage Guidelines

Choose tools based on user intent:
- **Email**: Use `send_email` when asked to send an email. The tool will prompt the user 
  for approval automatically — do NOT ask for confirmation in chat.
- **Documents**: Use `write_document` for writing or editing content. Always write the full 
  document in markdown. Keep content concise.
  Do NOT use italic or strike-through formatting in documents.
- **Planning**: Use `create_plan` to start a new plan, then `update_plan_step` to progress 
  each step. Only one plan can be active at a time. Do NOT summarize the plan in chat — 
  the UI renders it automatically. Continue updating steps until all are completed.
- **Visualization**: Use `show_chart` for trends/comparisons/distributions, `show_data_grid` 
  for tabular data, `show_form` for user input collection.
- **Weather**: Use `get_weather` when asked about weather conditions.
- **General chat**: For questions that don't require tools, respond conversationally.

## Rules
- When planning, use tools only without additional chat messages.
- After tool execution, provide a brief summary of the result.
- Do NOT repeat tool output in your text response — the UI renders tool results directly.
```

### 6.3 Prompt Length & Token Budget

The unified prompt is approximately **250 tokens** — well within the system prompt budget for GPT-4o/GPT-5-mini. Individual per-endpoint prompts averaged 80-150 tokens each; the unified prompt is shorter than 3 individual prompts combined because it deduplicates the markdown formatting instruction and tool descriptions.

---

## 7. ToolResultStreamingChatClient — Global Application

### 7.1 Current Problem

`ToolResultStreamingChatClient` is currently applied **only** to endpoint #2 (`/backend_tool_rendering`). Without it, `FunctionInvokingChatClient` consumes `FunctionResultContent` internally and the AG-UI conversion layer never produces `TOOL_CALL_RESULT` events. (ISS-007, R11)

### 7.2 Solution: Apply Globally at IChatClient Level

In the unified agent, `ToolResultStreamingChatClient` wraps the `IChatClient` **before** creating the base `ChatClientAgent`:

```
IChatClient pipeline:
  ToolResultStreamingChatClient → FunctionInvokingChatClient → ChatClient (LLM API)
```

This ensures **every** tool result (weather, email, document, chart, data grid, form, plan steps) is emitted to the AG-UI event stream. The wrapper is tool-name-agnostic — it detects any `FunctionResultContent` in the message stream and forwards it. (Q-UNIFY-003, R11)

### 7.3 Interaction with Approval-Required Tools

`ToolResultStreamingChatClient` and `ApprovalRequiredAIFunction` do not conflict. The approval flow operates at the `AIAgent` level (`ServerFunctionApprovalAgent`), while `ToolResultStreamingChatClient` operates at the `IChatClient` level. The tool result is only emitted after approval is granted and the function executes successfully. (R11, R12)

---

## 8. Per-Request Context Flow

### 8.1 AG-UI State via AdditionalProperties

The AG-UI protocol sends client state in the `RunAgentInput.State` field. `MapAGUI` places this into `ChatOptions.AdditionalProperties["ag_ui_state"]`. (R3)

In the unified agent:
- `SharedStateAgent` reads `ag_ui_state` to get current recipe/state data
- `PredictiveStateUpdatesAgent` is tool-name-scoped — it doesn't read `ag_ui_state`
- Other wrappers are content-type-scoped — they don't read `ag_ui_state`

No changes needed to the AdditionalProperties mechanism. The AG-UI protocol's `threadId` field naturally provides session identification for multi-session scenarios.

### 8.2 Singleton Agent, Per-Request Execution

The unified agent is created **once** at startup (singleton). `RunStreamingAsync()` creates a new execution context per request with its own message history, tool invocations, and cancellation scope. (Q-UNIFY-005, R3)

State isolation between concurrent requests is guaranteed by:
1. **AG-UI protocol**: Each POST sends the full message history — no server-side conversation state
2. **`ChatOptions` per request**: Each `RunStreamingAsync` call receives its own `ChatOptions` instance with per-request `AdditionalProperties`
3. **Wrapper statelessness**: All 4 wrappers read from the request's `options` parameter, not from instance fields

---

## 9. DataContent Disambiguation Strategy

### 9.1 The Problem

Four different wrappers emit `DataContent` with `application/json` media type, but with semantically different payloads (ISS-008):

| Source | DataContent Purpose | JSON Shape |
|--------|-------------------|------------|
| `AgenticUIAgent` | Plan state snapshot | `{ "steps": [...], "title": "..." }` |
| `AgenticUIAgent` | Plan state delta | Media type: `application/json-patch+json` |
| `SharedStateAgent` | Recipe state snapshot | `{ "title": "...", "ingredients": [...], "instructions": [...] }` |
| `PredictiveStateUpdatesAgent` | Document preview snapshot | `{ "document": "..." }` |

### 9.2 Disambiguation Strategy: Typed DataContent Convention

The client disambiguates `DataContent` using a **two-signal approach**:

1. **Media type differentiation**:
   - `application/json-patch+json` → Plan delta (from `AgenticUIAgent`) → dispatch to `PlanState`
   - `application/json` → Snapshot (needs further classification)

2. **JSON structure heuristics** (for `application/json` snapshots):
   - Has `"steps"` array → Plan snapshot → dispatch to `PlanState`
   - Has `"document"` string → Document preview → dispatch to `ArtifactState`
   - Has `"ingredients"` or `"instructions"` → Recipe snapshot → dispatch to `ArtifactState` (recipe)
   - Fallback → Log warning, ignore

### 9.3 Future Enhancement: Explicit Type Headers

The current heuristic approach (§9.2) works for the known set of DataContent types but is fragile for extensibility (ISS-008). A future improvement would add a custom data content type or a discriminator field:

```
Option A: Custom media type — "application/vnd.agui.plan+json", "application/vnd.agui.recipe+json"
Option B: Envelope wrapper  — { "$type": "plan_snapshot", "data": { ... } }
Option C: AG-UI metadata    — DataContent with AdditionalProperties["ag_ui_content_type"] = "plan"
```

**Recommendation for v2**: Keep JSON structure heuristics (proven, working) and add logging for unrecognized shapes. Plan for Option B (envelope wrapper) in a future iteration when new DataContent types are added.

---

## 10. Client-Side Impact: AGUIChatClientFactory Simplification

### 10.1 Current Factory

`AGUIChatClientFactory` maintains a hardcoded list of 10 endpoint paths, creates `AGUIChatClient` instances per-endpoint, and validates endpoint selection. (R7, ISS-051)

### 10.2 Simplified Factory

With a unified endpoint, the factory reduces to:

```csharp
public class AGUIChatClientFactory
{
    private const string UnifiedEndpointPath = "/chat";
    private readonly IHttpClientFactory _httpClientFactory;

    public AGUIChatClientFactory(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public IChatClient CreateChatClient(string? sessionId = null)
    {
        HttpClient httpClient = _httpClientFactory.CreateClient("AGUIDojoServer");
        return new AGUIChatClient(httpClient, UnifiedEndpointPath);
    }
}
```

### 10.3 Why Keep the Factory Pattern

Even with a single endpoint, the factory pattern is retained because (Q-UNIFY-006, R7):

1. **IHttpClientFactory encapsulation**: Centralizes HttpClient creation with proper lifecycle management
2. **Session metadata injection**: Future sessions may need custom headers or request properties
3. **Testability**: Interface abstraction enables mocking in unit tests
4. **Aspire service discovery**: The factory resolves the server URL via Aspire's service discovery, keeping connection details out of components

### 10.4 Endpoint Selection Removal

The client no longer needs `AgentState.SelectedEndpointPath` or the endpoint selector UI component. The `AgentState` Fluxor store simplifies:

- **Remove**: `SelectedEndpointPath` field, `SelectEndpointAction`, endpoint selector component
- **Keep**: `IsRunning`, `CurrentAuthorName` (still needed for agent status display)

---

## 11. Migration Path

### 11.1 Server-Side Changes

| Step | Change | Risk |
|------|--------|------|
| 1 | Create `CreateUnifiedAgent()` in `ChatClientAgentFactory` | Low — additive |
| 2 | Apply `ToolResultStreamingChatClient` globally | Low — existing behavior preserved for all tools |
| 3 | Add invocation context flag to `SharedStateAgent` | Medium — behavioral change in wrapper |
| 4 | Add phase-awareness to outer wrappers | Medium — must not break passthrough for non-shared-state requests |
| 5 | Compose all wrappers via builder | Low — MAF builder is designed for this (R2) |
| 6 | Write unified system prompt | Low — prompt engineering |
| 7 | Map single `MapAGUI("/chat", unifiedAgent)` | Low — additive |
| 8 | Remove 10 individual `MapAGUI` calls | Medium — breaking change for existing client |
| 9 | Update client factory to use single endpoint | Low — simplification |

### 11.2 Backward Compatibility

During migration, both architectures can coexist:
- Map `/chat` → unified agent (new)
- Keep `/agentic_chat`, `/backend_tool_rendering`, etc. → individual agents (legacy)
- Client switches to `/chat` when ready
- Remove legacy endpoints after client migration completes

---

## 12. Diagrams

### 12.1 Unified Agent Pipeline (Wrapper Composition)

```
┌─────────────────────────────────────────────────────────────────┐
│                        HTTP POST /chat                          │
│                    (AG-UI RunAgentInput)                         │
└───────────────────────────┬─────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│              ① ServerFunctionApprovalAgent                       │
│    Intercepts: FunctionApprovalRequest/ResponseContent           │
│    Action: Transforms to/from AG-UI approval tool call format   │
│    Phase-aware: Skips during shared_state "state_update" phase  │
└───────────────────────────┬─────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│              ② AgenticUIAgent                                    │
│    Intercepts: FunctionResultContent (create_plan, update_step) │
│    Action: Emits DataContent snapshots/deltas for plan state    │
│    Phase-aware: Skips during shared_state "state_update" phase  │
└───────────────────────────┬─────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│              ③ PredictiveStateUpdatesAgent                       │
│    Intercepts: FunctionCallContent (write_document)             │
│    Action: Chunks document text → progressive DataContent       │
│    Phase-aware: Skips during shared_state "state_update" phase  │
└───────────────────────────┬─────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│              ④ SharedStateAgent                                  │
│    Reads: ag_ui_state from AdditionalProperties                 │
│    Action: Double invocation (state update + summary)           │
│    Sets: ag_ui_shared_state_phase context flag                  │
└───────────────────────────┬─────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│              ⑤ OpenTelemetry                                     │
│    Traces both SharedStateAgent invocations                     │
└───────────────────────────┬─────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│              ⑥ ChatClientAgent (base)                            │
│    Tools: get_weather, send_email, write_document, create_plan, │
│           update_plan_step, show_chart, show_data_grid,         │
│           show_form                                              │
│    System Prompt: Unified structured prompt (§6.2)              │
└───────────────────────────┬─────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│         IChatClient Pipeline                                     │
│  ⑦ ToolResultStreamingChatClient                                │
│    → ⑧ FunctionInvokingChatClient                               │
│      → ⑨ ChatClient (OpenAI / Azure OpenAI API)                │
└─────────────────────────────────────────────────────────────────┘
```

### 12.2 Before vs. After Endpoint Topology

```
BEFORE (10 endpoints):                    AFTER (1 endpoint):

Client                                    Client
  ├─ /agentic_chat ───── Agent 1           │
  ├─ /backend_tool ───── Agent 2           │
  ├─ /human_in_loop ──── Agent 3           └── /chat ──── Unified Agent
  ├─ /tool_gen_ui ────── Agent 4                          (all tools, all wrappers)
  ├─ /agentic_gen_ui ─── Agent 5
  ├─ /shared_state ───── Agent 6
  ├─ /predictive ─────── Agent 7
  ├─ /data_grid ──────── Agent 8
  ├─ /chart ──────────── Agent 9
  └─ /dynamic_form ───── Agent 10
```

---

## References

- **R1**: Server Architecture — 10 endpoints & agent factory (research.md)
- **R2**: MAF Builder Pattern — `AIAgentBuilder.Use()` reverse-order composition (research.md)
- **R3**: MapAGUI Extension — POST→SSE, stateless, AdditionalProperties extensible (research.md)
- **R11**: ToolResultStreamingChatClient — IChatClient-level wrapper for FunctionResultContent (research.md)
- **R12**: MAF API Stability — RC APIs, `MEAI001` experimental pragma (research.md)
- **Q-UNIFY-001**: System prompt engineering for unified agent (brainstorm.md)
- **Q-UNIFY-002**: Builder pattern supports stacking all 4 wrappers (brainstorm.md)
- **Q-UNIFY-003**: ToolResultStreamingChatClient must be applied globally (brainstorm.md)
- **Q-UNIFY-005**: Singleton agent with per-request context (brainstorm.md)
- **Q-UNIFY-006**: Keep factory pattern, simplify to single endpoint (brainstorm.md)
- **Q-AGUI-004**: PredictiveStateUpdatesAgent is tool-name-scoped (brainstorm.md)
- **ISS-003**: Spec describes single endpoint but codebase has 10 (spec-critique.md)
- **ISS-004**: No unified endpoint design in spec (spec-critique.md)
- **ISS-007**: FunctionResultContent / ToolResultStreamingChatClient not documented (spec-critique.md)
- **ISS-008**: DataContent disambiguation fragility (spec-critique.md)
- **ISS-009**: Spec code sample doesn't match actual factory pattern (spec-critique.md)
- **ISS-010**: No tool catalog in spec (spec-critique.md)
- **ISS-011**: FunctionApprovalRequestContent experimental status not noted (spec-critique.md)
- **ISS-013**: AgentStreamingService streaming complexity not specified (spec-critique.md)
- **ISS-020**: HITL background session handling unaddressed (spec-critique.md)
- **ISS-033**: DataContent routing from multiple producers unspecified (spec-critique.md)
- **ISS-051**: AGUIChatClientFactory not described in spec (spec-critique.md)
