# The 7 AG-UI Protocol Features & Agent Framework Support

Agent Framework's AG-UI integration supports all 7 standardized protocol features:

1. **Agentic Chat** — Streaming chat with automatic tool calling (no manual parsing)
2. **Backend Tool Rendering** — Tools execute server-side via `AIFunctionFactory`; results stream to client
3. **Human-in-the-Loop** — `ApprovalRequiredAIFunction` middleware converts to approval protocol events
4. **Agentic Generative UI** — Async tools with progress updates for long-running operations
5. **Tool-Based UI Rendering** — Custom Blazor components render based on tool definitions
6. **Shared State** — Bidirectional state synchronization between agent and Blazor client
7. **Predictive State Updates** — Stream tool arguments as optimistic updates before execution