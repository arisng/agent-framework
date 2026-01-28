# AG-UI Feature Iteration Plan

This document outlines the refined roadmap for implementing Agentic UI (AG-UI) features in the `AGUIWebChat` sample, reflecting the findings from the deep dive architecture review.

## 1. Refactor Phase (Immediate)

**Goal**: Establish a clean, generic AG-UI foundation by removing tight coupling from `Chat.razor`.

- [ ] **Task 3.1: Extract `AGUIProtocolService`**
  - **Description**: Move all AG-UI protocol logic (SSE handling, event parsing, HTTP request construction) out of `Chat.razor` and into a dedicated `AGUIProtocolService`.
  - **Interface**: The service should expose methods like `StartStreamAsync(string input)`, `ListenForUpdates()`, etc.
  
- [ ] **Task 3.2: Extract State Container / ViewModel**
  - **Description**: Move the chat state (List of messages, current status, `IsThinking`) into a separate `ChatViewModel` or `IStateContainer` that the page subscribes to.
  - **Benefit**: Allows `Chat.razor` to be purely about rendering, enabling easier testing and State/UI separation.

- [ ] **Task 3.3: Robust JSON Patching**
  - **Description**: Replace the current manual/custom delta application logic with a standard `System.Json.Patch` approach or a robust helper that handles nested object updates correctly.
  - **Requirement**: Ensure the client correctly applies patches to the local State model received from the server.

## 2. Generative Phase (Feature 4)

**Goal**: Implement true Generative UI where the client renders specific components based on the data type, not hardcoded logic.

- [ ] **Task 4.1: Define `IComponentRegistry`**
  - **Description**: Create a service/interface that maps content MIME types (e.g., `application/vnd.microsoft.agui.plan+json`) to Blazor Component Types (e.g., `PlanComponent`).
  - **Mechanism**: A simple Dictionary-based registry that can be configured at startup in `Program.cs`.

- [ ] **Task 4.2: Implement `DynamicMessageRenderer`**
  - **Description**: create a new Blazor component that accepts a generic Data object and its content type. It uses the `IComponentRegistry` to find the correct component and renders it using Blazor's `<DynamicComponent>`.
  - **Impact**: `Chat.razor`'s render loop becomes a simple iteration of `<DynamicMessageRenderer Item="msg" />`.

- [ ] **Task 4.3: Register `PlanComponent`**
  - **Description**: Refactor the existing Plan rendering logic into a standalone component and register it to handle `application/vnd.microsoft.agui.plan+json`.

## 3. Future Phases

These features are planned but are blocked/dependent on the completion of the Refactor and Generative phases.

- [ ] **Feature 3: User Confirmation**
  - **Goal**: Allow the agent to pause execution and request explicit user approval.
  - **Status**: Pending Refactor.
  - **Implementation**: Needs `AGUIProtocolService` to handle the specific "approval needed" event and a UI modal.

- [ ] **Feature 5: Client-to-Agent Events**
  - **Goal**: Support interactive UI elements (buttons, forms) sending events back to the specific agent instance.
  - **Status**: Pending Generative Phase.
  - **Implementation**: `POST /agents/{id}/events` integrated into base component classes.

- [ ] **Feature 6: Adaptive Cards**
  - **Goal**: Support rendering Adaptive Cards as a first-class content type.
  - **Status**: Planned.
  - **Implementation**: Register `AdaptiveCardRenderer` for `application/vnd.microsoft.card.adaptive` in the `IComponentRegistry`.

- [ ] **Feature 7: Multi-turn / History**
  - **Goal**: Support re-hydrating chat history and resuming sessions.
  - **Status**: Planned.

## 4. Technical Debt

- [ ] **Magic String Path Parsing**
  - **Issue**: The current code parses JSON paths using manual string manipulation (e.g., checking if efficient path strings start with `items/`).
  - **Remediation**: Use a structured parsing approach or a proper JSON Pointer implementation to handle updates safely and robustly.
