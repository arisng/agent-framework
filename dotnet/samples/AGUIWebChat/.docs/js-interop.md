# JavaScript Interop Modules (AGUIWebChat)

## Overview
The chat UI uses Blazor JavaScript interop for UI behaviors that are simpler to implement in JavaScript, such as auto-scrolling and auto-resizing text input. These modules are loaded via `IJSRuntime` on first render.

## Module Details

### Message list auto-scroll
- **Responsibility:** Keep the message list pinned to new messages while preserving user scroll intent.
- **Inputs:** DOM mutations in the message list.
- **Outputs:** Smooth scroll behavior when new content arrives.
- **Dependencies:** `MutationObserver`, custom element registration, Blazor JS interop.
- **Primary files:** [dotnet/samples/AGUIWebChat/Client/Components/Pages/Chat/ChatMessageList.razor.js](dotnet/samples/AGUIWebChat/Client/Components/Pages/Chat/ChatMessageList.razor.js), [dotnet/samples/AGUIWebChat/Client/Components/Pages/Chat/ChatMessageList.razor](dotnet/samples/AGUIWebChat/Client/Components/Pages/Chat/ChatMessageList.razor)

### Chat input autosize and submit
- **Responsibility:** Autosize the textarea and convert Enter keypresses into submit events.
- **Inputs:** Keyboard and input events on the textarea.
- **Outputs:** Resized textarea and form submission triggers.
- **Dependencies:** DOM APIs, Blazor JS interop.
- **Primary files:** [dotnet/samples/AGUIWebChat/Client/Components/Pages/Chat/ChatInput.razor.js](dotnet/samples/AGUIWebChat/Client/Components/Pages/Chat/ChatInput.razor.js), [dotnet/samples/AGUIWebChat/Client/Components/Pages/Chat/ChatInput.razor](dotnet/samples/AGUIWebChat/Client/Components/Pages/Chat/ChatInput.razor)

## References
- Blazor JavaScript interop: https://learn.microsoft.com/aspnet/core/blazor/javascript-interoperability?view=aspnetcore-8.0
- JavaScript modules in Blazor: https://learn.microsoft.com/aspnet/core/blazor/javascript-interoperability/call-javascript-from-dotnet?view=aspnetcore-8.0
