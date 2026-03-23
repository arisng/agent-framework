# AGUIDojo LLM picker architecture and MAF alignment

## Executive summary

AGUIDojo can support per-session model selection without breaking its current unified `POST /chat` architecture. The consolidated finding from the prior research is that the feature still spans **five layers**:

1. model metadata registry,
2. client-side session state and UI,
3. AG-UI transport,
4. server-side model routing,
5. model-aware compaction.

The important refinement is that Microsoft Agent Framework (MAF) .NET already covers much of layers 3-5. AGUIDojo-specific work is concentrated in:

- introducing a durable model registry and session-level `ModelId`,
- surfacing model selection in the client,
- bridging `modelId` through AG-UI transport,
- and making compaction thresholds react to model changes.

The recommended implementation pattern is:

- keep `ModelId` as AGUIDojo-owned session metadata,
- transport it as AG-UI `forwardedProps`,
- on the server, set both `ChatClientAgentRunOptions.ChatOptions.ModelId` and `ChatClientAgentRunOptions.ChatClientFactory`,
- route to a cached `IChatClient` keyed by model,
- and replace the current `ContextWindowChatClient` with `CompactionProvider` + `PipelineCompactionStrategy` driven by model-aware token thresholds.

That preserves the sample's single-agent, single-endpoint architecture while making model switching explicit, observable, and safe.

## Current AGUIDojo baseline and gaps

AGUIDojo already has the right high-level shape for an LLM picker:

- one unified `/chat` route on the server,
- one streaming loop on the client,
- one session model per chat tab,
- and one agent pipeline composed from wrappers and tools.

However, the current sample still has several gaps that matter for model selection:

| Area | Current state | Practical implication |
| --- | --- | --- |
| Session metadata | `AGUIDojoClient/Models/SessionMetadata.cs` has no `ModelId` | Model preference is not part of session identity or list state |
| Session state | `AGUIDojoClient/Store/SessionManager/SessionState.cs` has no `ModelId` or model capability data | Active session runtime cannot reason about model changes |
| Browser persistence | `AGUIDojoClient/Services/SessionPersistenceService.cs` persists no `ModelId` | Reload loses model preference unless re-derived elsewhere |
| Client transport | `AgentStreamingService` streams through `AGUIChatClientFactory` → `AGUIChatClient` | There is no AGUIDojo-specific place yet to attach `forwardedProps.modelId` |
| Server chat pipeline | `AGUIDojoServer/ChatClientAgentFactory.cs` builds one unified agent over `new ContextWindowChatClient(..., maxNonSystemMessages: 80)` | Context protection is fixed, message-count based, and not model-aware |
| Compaction policy | `AGUIDojoServer/ContextWindowChatClient.cs` only keeps the last N non-system messages | Switching to a smaller-context model can still overflow token limits |

The net effect is that AGUIDojo can currently talk to one configured model per process, but it does not yet have a session-scoped model picker architecture.

## Consolidated 5-layer design

| Layer | Purpose | MAF .NET support | AGUIDojo work |
| --- | --- | --- | --- |
| 1. Model registry | Map `modelId` to display name, context window, and capabilities | None out of the box | Add an application-owned registry/config abstraction |
| 2. Client UI and state | Let users choose a model and persist that choice with the session | None out of the box | Add `ModelId` to session state, persistence, and picker UI |
| 3. Protocol transport | Carry the selected model with each `/chat` request | `RunAgentInput` already has `forwardedProps`; server already places `input.ForwardedProperties` into `ChatOptions.AdditionalProperties["ag_ui_forwarded_properties"]` | Populate `forwardedProps.modelId` on the client |
| 4. Server-side routing | Use the requested model for the current turn | `ChatClientAgentRunOptions.ChatClientFactory` and `ChatOptions.ModelId` are already supported | Build a model-keyed client cache and wire per-request routing |
| 5. Model-aware compaction | Keep history within the active model's window, especially on downgrade | `CompactionProvider`, `PipelineCompactionStrategy`, built-in strategies, and token counting are already available | Replace the current fixed window wrapper and add dynamic thresholds |

This is the most important consolidation from the two earlier reports: **the design is still five-layer, but the server side is less custom than it first appeared.**

## What MAF already gives AGUIDojo out of the box

### 1. Transport primitives

MAF's AG-UI payload already includes `RunAgentInput.ForwardedProperties` in `dotnet/src/Microsoft.Agents.AI.AGUI/Shared/RunAgentInput.cs`, and `MapAGUI` already copies that value into `ChatOptions.AdditionalProperties["ag_ui_forwarded_properties"]` in `dotnet/src/Microsoft.Agents.AI.Hosting.AGUI.AspNetCore/AGUIEndpointRouteBuilderExtensions.cs`.

That means AGUIDojo does **not** need a new endpoint or protocol extension just to move `modelId` from client to server. A `forwardedProps` payload is enough.

The remaining transport gap is client-side: `dotnet/src/Microsoft.Agents.AI.AGUI/AGUIChatClient.cs` constructs `RunAgentInput` with `ThreadId`, `RunId`, `Messages`, and `State`, but it does not currently populate `ForwardedProperties`. Because AGUIDojo currently streams through `AGUIChatClientFactory` rather than building the request body itself, AGUIDojo needs either:

- a thin wrapper around `AGUIChatClient`,
- a small extension/fork of `AGUIChatClient`,
- or an AGUIDojo-specific request path that preserves the same AG-UI payload shape.

On the response path, `dotnet/src/Microsoft.Agents.AI.Hosting.AGUI.AspNetCore/AGUIChatResponseUpdateStreamExtensions.cs` already preserves `ChatResponseUpdate.ModelId`, which gives the client a reliable way to show the actual model that served the response.

### 2. Per-request model selection semantics

MAF already provides both pieces AGUIDojo needs for server-side model routing:

- `ChatClientAgentRunOptions.ChatOptions` for per-request model metadata,
- `ChatClientAgentRunOptions.ChatClientFactory` for per-request client replacement.

`dotnet/src/Microsoft.Agents.AI/ChatClient/ChatClientAgent.cs` already merges `ModelId` in the expected order:

```text
run-options ModelId > agent-options ModelId > inner client default
```

That merge is important for consistent tracing, fallback, and downstream providers that honor `ChatOptions.ModelId`.

However, the consolidated conclusion from both reports still stands: **`ChatOptions.ModelId` alone is not enough for real model switching when the underlying `IChatClient` is already bound to a deployment/model at construction time.** For OpenAI-style clients, AGUIDojo should treat `ModelId` as metadata plus request intent, and `ChatClientFactory` as the mechanism that actually swaps in the correct client instance.

### 3. Compaction infrastructure

MAF already ships the compaction pipeline AGUIDojo wants but does not currently use:

- `CompactionProvider`,
- `PipelineCompactionStrategy`,
- `ToolResultCompactionStrategy`,
- `SummarizationCompactionStrategy`,
- `SlidingWindowCompactionStrategy`,
- `TruncationCompactionStrategy`,
- `CompactionMessageIndex` with token counting.

This is materially better than AGUIDojo's current `ContextWindowChatClient`, which keeps the last 80 non-system messages and assumes that is "safe enough" for large-window models. The framework pipeline is token-aware, composable, and session-aware.

## Recommended AGUIDojo architecture

### 1. Introduce an application-owned model registry

AGUIDojo should own a small registry that maps stable model IDs to operational metadata. A minimal contract is enough:

```csharp
public interface IModelRegistry
{
    ModelInfo GetModel(string modelId);
    IReadOnlyList<ModelInfo> GetAvailableModels();
}

public sealed record ModelInfo(
    string ModelId,
    string DisplayName,
    int ContextWindowTokens,
    bool SupportsVision = false);
```

This is intentionally application-specific. MAF is provider-agnostic and does not know the context windows, capability tiers, or UX labels AGUIDojo wants to expose.

### 2. Keep the unified `/chat` route and transport `modelId` via `forwardedProps`

AGUIDojo should not create one `/chat` endpoint per model. The stronger consolidated recommendation is to keep one agent entry point and move model selection as per-request metadata:

```json
{
  "forwardedProps": {
    "modelId": "gpt-4.1"
  }
}
```

That keeps model choice a property of the run, not the route.

Transport guidance:

- store the user's selected model on the session,
- emit it into `forwardedProps.modelId` for each turn,
- read it from `ag_ui_forwarded_properties` on the server,
- and reflect the effective model back to the UI through `ChatResponseUpdate.ModelId`.

### 3. Use the combined routing pattern: `ModelId` merge + `ChatClientFactory`

The recommended server-side pattern is:

1. extract `modelId` from forwarded properties,
2. resolve it through the model registry,
3. set `runOptions.ChatOptions.ModelId = modelId`,
4. set `runOptions.ChatClientFactory = _ => modelClientCache.GetOrCreate(modelId)`.

That combination matters:

- `ChatOptions.ModelId` preserves model intent in the run options and participates in normal MAF merge behavior,
- `ChatClientFactory` performs the actual swap to a model-bound `IChatClient`,
- the rest of the unified agent pipeline remains unchanged.

The cache should be keyed by stable model ID and return fully configured clients for that model tier/deployment. This keeps client creation amortized and preserves a single logical agent pipeline above the transport layer.

### 4. Replace `ContextWindowChatClient` with model-aware compaction

This is the most important runtime change.

AGUIDojo's current `ContextWindowChatClient` is useful as a stopgap, but it is still a fixed sliding window. The more durable design is to move context management into MAF compaction:

1. `ToolResultCompactionStrategy`
2. `SummarizationCompactionStrategy`
3. `SlidingWindowCompactionStrategy`
4. `TruncationCompactionStrategy`

This ordering keeps the least-destructive reductions first and the emergency backstop last.

#### Why dynamic thresholds are the real gap

The critical compaction finding from the second report should be preserved unchanged in intent: MAF strategies are extensible, but `CompactionTriggers.TokensExceed(n)` captures a static threshold at creation time. If AGUIDojo creates a provider for a 400K model and then switches the session to a 32K model, a static 400K threshold will not protect the downgraded turn.

So AGUIDojo still needs one custom piece: **dynamic token thresholds**.

The preferred design is to keep one long-lived `CompactionProvider` per session/agent path and back its triggers with mutable model-aware thresholds. That keeps the provider's persisted `CompactionMessageIndex` state while letting the threshold react when the session's `ModelId` changes.

Practical guidance:

- derive trigger thresholds from `ModelInfo.ContextWindowTokens`,
- reserve a safety margin of roughly 80-85% of the advertised window,
- update the active threshold before the next LLM call when the model changes,
- let the normal compaction pipeline run immediately if the downgraded model is already over budget.

This is preferable to rebuilding the whole provider per request, which would discard accumulated compaction state.

### 5. Persist model choice as session metadata

AGUIDojo should add `ModelId` to:

- `SessionMetadata`,
- `SessionState` or equivalent active-session runtime state,
- and `SessionMetadataDto` / related browser persistence.

If AGUIDojo later moves session authority to the server, the same field should become server-owned session metadata rather than browser-only state. The architectural point remains the same: **the selected model belongs to the session, not only to an individual UI interaction.**

## Recommended implementation order

1. **Add the model registry and configuration source**  
   Establish stable `modelId` values, display names, and context windows first.

2. **Replace `ContextWindowChatClient` with MAF compaction plumbing**  
   Move AGUIDojo off fixed message-count trimming and onto `CompactionProvider` + pipeline strategies.

3. **Introduce dynamic compaction thresholds**  
   Make compaction react to model changes without discarding session compaction state.

4. **Wire server-side model routing**  
   Read `modelId`, set `ChatOptions.ModelId`, and set `ChatClientFactory` to a model-keyed cache.

5. **Extend AG-UI transport to send `forwardedProps.modelId`**  
   Add the thin client-side bridge needed because `AGUIChatClient` does not populate `ForwardedProperties` today.

6. **Persist model choice and add the UI picker**  
   Once the server path is ready, add `ModelId` to session metadata/state and expose it in the chat header or session settings.

This order keeps the hardest correctness work on the server side first, then exposes the user-facing control after routing and compaction are already safe.

## Design decisions to carry forward

- **Do not create multiple `/chat` endpoints per model.** Keep one unified agent route.
- **Do not rely on `ChatOptions.ModelId` alone for provider-bound clients.** Use `ChatClientFactory` for actual switching.
- **Do not keep client-side message-count trimming as the primary context strategy.** Replace it with MAF compaction.
- **Do not treat model choice as transient UI state.** Persist it with the session.
- **Do show the effective model used for a response.** `ChatResponseUpdate.ModelId` already gives AGUIDojo a confirmation channel.

## Repo anchors

These files are the most useful implementation anchors for the design above:

- `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Models/SessionMetadata.cs`
- `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Store/SessionManager/SessionState.cs`
- `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Services/SessionPersistenceService.cs`
- `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Services/AgentStreamingService.cs`
- `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Services/AGUIChatClientFactory.cs`
- `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoServer/ChatClientAgentFactory.cs`
- `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoServer/ContextWindowChatClient.cs`
- `dotnet/src/Microsoft.Agents.AI.AGUI/Shared/RunAgentInput.cs`
- `dotnet/src/Microsoft.Agents.AI.AGUI/AGUIChatClient.cs`
- `dotnet/src/Microsoft.Agents.AI.Hosting.AGUI.AspNetCore/AGUIEndpointRouteBuilderExtensions.cs`
- `dotnet/src/Microsoft.Agents.AI.Hosting.AGUI.AspNetCore/AGUIChatResponseUpdateStreamExtensions.cs`
- `dotnet/src/Microsoft.Agents.AI/ChatClient/ChatClientAgentRunOptions.cs`
- `dotnet/src/Microsoft.Agents.AI/ChatClient/ChatClientAgent.cs`
- `dotnet/src/Microsoft.Agents.AI/Compaction/CompactionProvider.cs`
- `dotnet/src/Microsoft.Agents.AI/Compaction/CompactionTriggers.cs`
- `dotnet/src/Microsoft.Agents.AI/Compaction/CompactionMessageIndex.cs`
- `dotnet/samples/02-agents/Agents/Agent_Step11_Middleware/Program.cs`
- `dotnet/samples/02-agents/Agents/Agent_Step18_CompactionPipeline/Program.cs`
