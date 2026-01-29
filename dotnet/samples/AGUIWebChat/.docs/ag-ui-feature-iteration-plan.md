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

## 2. Generative Phase (Feature 4) ✅ **COMPLETED**

**Status**: Completed (January 2026)  
**Goal**: Implement true Generative UI where the client renders specific components based on the data type, not hardcoded logic.

- [x] **Task 4.1: Define `IComponentRegistry`**
  - **Description**: Create a service/interface that maps content MIME types (e.g., `application/vnd.microsoft.agui.plan+json`) to Blazor Component Types (e.g., `PlanComponent`).
  - **Mechanism**: A simple Dictionary-based registry that can be configured at startup in `Program.cs`.
  - **Status**: Completed. `IComponentRegistry` interface and `ComponentRegistry` implementation created in `Client/Services/`.

- [x] **Task 4.2: Implement `DynamicMessageRenderer`**
  - **Description**: create a new Blazor component that accepts a generic Data object and its content type. It uses the `IComponentRegistry` to find the correct component and renders it using Blazor's `<DynamicComponent>`.
  - **Impact**: `Chat.razor`'s render loop becomes a simple iteration of `<DynamicMessageRenderer Item="msg" />`.
  - **Status**: Completed. Dynamic rendering implemented in `ChatMessageItem.razor` using `<DynamicComponent>` with registry-based component resolution.

- [x] **Task 4.3: Register `PlanComponent`**
  - **Description**: Refactor the existing Plan rendering logic into a standalone component and register it to handle `application/vnd.microsoft.agui.plan+json`.
  - **Status**: Completed. Plan and Weather components registered in `Program.cs` component registry.

## 2.1. Interactive Quiz Feature ✅ **COMPLETED**

**Status**: Completed (January 29, 2026)  
**Goal**: Implement an interactive quiz chat experience with dynamic rendering of quiz cards, answer selection, and evaluation display.

### Implemented Components

- [x] **Quiz Data Models** (`Client/Models/Quiz/QuizModels.cs`)
  - Defined C# record types: `Quiz`, `QuestionCard`, `QuestionContent`, `AnswerOption`, `SelectionRule`, `CorrectAnswerDisplayRule`, `CardEvaluation`
  - Properties match TypeScript schema with JSON serialization attributes

- [x] **QuizCardComponent** (`Client/Components/Quiz/QuizCardComponent.razor[.cs/.css]`)
  - Renders individual quiz question cards with answer options
  - Supports single-select (radio buttons) and multi-select (checkboxes) modes
  - Handles user selection events and updates `userChoiceIds`
  - Displays evaluation results when present
  - Conditionally shows correct answers based on display rules
  - Styled with modern, responsive design matching AGUIWebChat aesthetic

- [x] **QuizComponent** (`Client/Components/Quiz/QuizComponent.razor[.cs/.css]`)
  - Renders complete quiz with title, instructions, and card list
  - Iterates and renders `QuizCardComponent` for each card
  - Cards sorted by sequence number
  - Styled with proper spacing and responsive layout

- [x] **Component Registry Integration** (`Client/Program.cs`)
  - Registered `application/vnd.quiz+json` → `QuizComponent`
  - Registered `application/vnd.quiz.card+json` → `QuizCardComponent`
  - No hardcoded quiz logic in `ChatMessageItem.razor` (verified)

### Capabilities Delivered

- ✅ Single-select questions with radio button interface
- ✅ Multi-select questions with checkbox interface and min/max constraints
- ✅ Rich question content with descriptions and media
- ✅ Answer options with labels, descriptions, and media attachments
- ✅ Real-time evaluation with scoring and feedback
- ✅ Conditional correct answer visibility based on display rules
- ✅ Disabled state support
- ✅ Sequential ordering for structured learning paths
- ✅ Responsive design (mobile-friendly)
- ✅ Visual indicators for selected, correct, and incorrect answers

### Testing & Validation

- ✅ Build validation passed (no compilation errors)
- ✅ Manual test payload and procedure created
- ✅ Playwright E2E test validated quiz interaction flow
- ✅ Documentation updated in README.md

### Discovered Tasks / Future Enhancements

1. **Answer Submission to Server**: Currently, answer selection is client-side only. Future enhancement could add API integration to submit answers back to the agent for server-side evaluation.
   
2. **Real-time Updates via JSON Patch**: Quiz data currently delivered via initial snapshot. Consider supporting real-time updates (e.g., server reveals correct answers via patch operations).
   
3. **Quiz Analytics**: Track user performance metrics (time spent, attempts, scores) for educational insights.
   
4. **Media Support**: While data model supports media attachments for questions and answers, full media rendering (images, videos) could be enhanced with dedicated media components.
   
5. **Accessibility**: Add ARIA labels, keyboard navigation enhancements, and screen reader support.

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
