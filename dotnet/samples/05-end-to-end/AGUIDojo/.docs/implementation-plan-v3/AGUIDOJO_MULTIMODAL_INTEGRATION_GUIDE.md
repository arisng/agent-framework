# AGUIDojo Multimodal Attachment Support - Integration Guide

## Overview
This document provides specific implementation guidance for adding multimodal attachment support to AGUIDojo, based on the current architecture analysis.

---

## 1. Data Models to Create/Extend

### 1.1 Create AttachmentContent Model
**File**: `AGUIDojoClient/Models/AttachmentContent.cs` (NEW)

```csharp
using Microsoft.Extensions.AI;

namespace AGUIDojoClient.Models;

/// <summary>
/// Represents an attached file (image, document, etc.) in a chat message.
/// </summary>
public class AttachmentContent : AIContent
{
    /// <summary>
    /// Gets the MIME type of the attachment (e.g., "image/png", "application/pdf").
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Gets the original filename of the attachment.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets the size of the attachment in bytes.
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Gets the base64-encoded file data, or a URL reference.
    /// Strategy: For images <5MB, embed as base64. For larger files, store as URL or reference ID.
    /// </summary>
    public string DataUrlOrReference { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a unique identifier for this attachment (for server-side tracking).
    /// </summary>
    public string? AttachmentId { get; set; }

    /// <summary>
    /// Gets the preview URL for display in UI (may be same as DataUrlOrReference for images).
    /// </summary>
    public string? PreviewUrl { get; set; }
}
```

### 1.2 Extend ChatMessage Model
**File**: `AGUIDojoClient/Models/ChatMessage.cs` (MODIFY)

Current: Inherits from Microsoft.Extensions.AI.ChatMessage with Contents: IList<AIContent>

The Contents collection already supports any AIContent type, so AttachmentContent fits naturally.

**No changes needed** - AttachmentContent can be added directly to Contents.

### 1.3 Create Attachment Metadata Model
**File**: `AGUIDojoClient/Models/AttachmentMetadata.cs` (NEW)

```csharp
namespace AGUIDojoClient.Models;

/// <summary>
/// Metadata about an attachment used in title generation and notifications.
/// </summary>
public record AttachmentMetadata(
    string FileName,
    string ContentType,
    long FileSizeBytes,
    DateTimeOffset UploadedAt);
```

---

## 2. UI Components to Modify/Create

### 2.1 Enhance ChatInput.razor
**File**: `AGUIDojoClient/Components/Pages/Chat/ChatInput.razor` (MODIFY)

**Current Structure**:
```html
<EditForm Model="@this">
    <label class="input-box">
        <textarea></textarea>
        <div class="tools">
            <PanicButton />
            <button type="submit"></button>
        </div>
    </label>
</EditForm>
```

**Modified Structure**:
```html
<EditForm Model="@this" OnValidSubmit="@SendMessageAsync" FormName="chat-input-form">
    <div class="chat-input-container">
        <!-- Attachment Preview Area -->
        @if (attachedFiles.Count > 0)
        {
            <div class="attachment-previews">
                @foreach (var file in attachedFiles)
                {
                    <div class="attachment-preview-item">
                        <div class="attachment-preview-badge">
                            @if (IsImageFile(file.FileName))
                            {
                                <img src="@GetPreviewUrl(file)" alt="attachment" class="attachment-thumb" />
                            }
                            else if (IsPdfFile(file.FileName))
                            {
                                <div class="attachment-pdf-icon">📄</div>
                            }
                            else
                            {
                                <div class="attachment-file-icon">📎</div>
                            }
                        </div>
                        <div class="attachment-info">
                            <div class="attachment-name">@file.FileName</div>
                            <div class="attachment-size">@FormatFileSize(file.FileSizeBytes)</div>
                        </div>
                        <button type="button" 
                                class="attachment-remove-btn"
                                @onclick="() => RemoveAttachment(file.AttachmentId)"
                                aria-label="Remove attachment">
                            ✕
                        </button>
                    </div>
                }
            </div>
        }
        
        <!-- Input Box -->
        <label class="input-box">
            <textarea @ref="@textArea" 
                      @bind="@messageText" 
                      placeholder="Type your message..."
                      rows="1" 
                      aria-label="Type your message"></textarea>

            <div class="tools">
                <!-- File Input (hidden, triggered by button) -->
                <input type="file" 
                       @ref="@fileInput" 
                       @onchange="@HandleFileInputChange"
                       multiple 
                       accept="image/*,.pdf,.docx,.xlsx,.txt,.md"
                       hidden
                       aria-label="Choose files to attach" />
                
                <!-- Attachment Button -->
                <button type="button"
                        class="attach-button"
                        @onclick="@() => fileInput.Click()"
                        title="Attach files (images, documents)"
                        aria-label="Attach files">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" 
                         stroke-width="1.5" stroke="currentColor" class="tool-icon">
                        <path stroke-linecap="round" stroke-linejoin="round" 
                              d="M18.375 12.739l-7.693 7.693a4.5 4.5 0 01-6.364-6.364l10.94-10.94A3 3 0 1119.5 7.372L8.552 18.319m0 0l-2.261 2.261m0 0a1.5 1.5 0 002.121 2.121l10.303-10.304a6 6 0 000-8.485L11.671 2.575a3 3 0 00-4.242 4.242l8.484 8.483" />
                    </svg>
                </button>

                <!-- Panic Button -->
                <PanicButton IsAgentRunning="@IsAgentRunning" OnPanic="@OnPanic" />

                <!-- Send Button -->
                <button type="submit" 
                        title="Send" 
                        class="send-button" 
                        aria-label="Send message">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" 
                         stroke-width="1.5" stroke="currentColor" class="tool-icon">
                        <path stroke-linecap="round" stroke-linejoin="round" 
                              d="M6 12 3.269 3.125A59.769 59.769 0 0 1 21.485 12 59.768 59.768 0 0 1 3.27 20.875L5.999 12Zm0 0h7.5" />
                    </svg>
                </button>
            </div>
        </label>
    </div>
</EditForm>

@code {
    private ElementReference textArea;
    private ElementReference fileInput;
    private string? messageText;
    private List<AttachmentFile> attachedFiles = new();

    [Parameter]
    public EventCallback<(ChatMessage Message, List<AttachmentFile> Attachments)> OnSend { get; set; }

    [Parameter]
    public bool IsAgentRunning { get; set; }

    [Parameter]
    public EventCallback OnPanic { get; set; }

    public class AttachmentFile
    {
        public string AttachmentId { get; set; } = Guid.NewGuid().ToString("N");
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public byte[] FileData { get; set; } = [];
    }

    private async Task HandleFileInputChange(ChangeEventArgs e)
    {
        var files = (IBrowserFile[])e.Value;
        const long MaxFileSize = 50 * 1024 * 1024; // 50MB max

        foreach (var file in files)
        {
            if (file.Size > MaxFileSize)
            {
                // Log error or show notification
                continue;
            }

            using var stream = file.OpenReadStream(maxAllowedSize: MaxFileSize);
            var buffer = new byte[file.Size];
            await stream.ReadExactlyAsync(buffer);

            attachedFiles.Add(new AttachmentFile
            {
                FileName = file.Name,
                ContentType = file.ContentType,
                FileSizeBytes = file.Size,
                FileData = buffer
            });
        }

        StateHasChanged();
    }

    private void RemoveAttachment(string attachmentId)
    {
        attachedFiles.RemoveAll(a => a.AttachmentId == attachmentId);
    }

    private bool IsImageFile(string fileName) => 
        fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);

    private bool IsPdfFile(string fileName) => 
        fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    private string GetPreviewUrl(AttachmentFile file) =>
        $"data:{file.ContentType};base64,{Convert.ToBase64String(file.FileData)}";

    private string FormatFileSize(long bytes) =>
        bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes / (1024.0 * 1024):F1} MB"
        };

    private async Task SendMessageAsync()
    {
        if (messageText is { Length: > 0 } text)
        {
            messageText = null;
            var userMessage = new ChatMessage(ChatRole.User, text);
            
            // Create AttachmentContent for each file
            foreach (var file in attachedFiles)
            {
                var attachmentContent = new AttachmentContent
                {
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    FileSizeBytes = file.FileSizeBytes,
                    DataUrlOrReference = $"data:{file.ContentType};base64,{Convert.ToBase64String(file.FileData)}",
                    PreviewUrl = IsImageFile(file.FileName) 
                        ? $"data:{file.ContentType};base64,{Convert.ToBase64String(file.FileData)}"
                        : null
                };
                userMessage.Contents.Add(attachmentContent);
            }

            attachedFiles.Clear();
            await OnSend.InvokeAsync((userMessage, attachedFiles));
            await textArea.FocusAsync();
        }
    }
}
```

### 2.2 Enhance ChatInput.razor.css
**File**: `AGUIDojoClient/Components/Pages/Chat/ChatInput.razor.css` (MODIFY)

```css
/* Existing styles... */

/* Attachment Preview Area */
.chat-input-container {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
}

.attachment-previews {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
    padding: 0.5rem;
    background: hsl(var(--muted));
    border-radius: 0.5rem;
    border: 1px solid hsl(var(--border));
}

.attachment-preview-item {
    display: flex;
    align-items: center;
    gap: 0.75rem;
    padding: 0.5rem;
    background: hsl(var(--background));
    border-radius: 0.375rem;
    border: 1px solid hsl(var(--border));
}

.attachment-preview-badge {
    flex-shrink: 0;
    width: 2.5rem;
    height: 2.5rem;
    display: flex;
    align-items: center;
    justify-content: center;
    background: hsl(var(--muted));
    border-radius: 0.375rem;
    overflow: hidden;
}

.attachment-thumb {
    width: 100%;
    height: 100%;
    object-fit: cover;
    border-radius: 0.25rem;
}

.attachment-pdf-icon,
.attachment-file-icon {
    font-size: 1.25rem;
}

.attachment-info {
    flex: 1;
    min-width: 0;
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
}

.attachment-name {
    font-size: 0.875rem;
    font-weight: 500;
    color: hsl(var(--foreground));
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}

.attachment-size {
    font-size: 0.75rem;
    color: hsl(var(--muted-foreground));
}

.attachment-remove-btn {
    flex-shrink: 0;
    background: transparent;
    border: none;
    color: hsl(var(--muted-foreground));
    cursor: pointer;
    padding: 0.25rem;
    font-size: 1rem;
    line-height: 1;
    transition: color 0.15s ease;
}

.attachment-remove-btn:hover {
    color: hsl(var(--foreground));
}

.attach-button {
    background-color: hsl(var(--background));
    border-style: dashed;
    color: hsl(var(--muted-foreground));
    border-color: hsl(var(--border));
    padding: 3px 8px;
    cursor: pointer;
    transition: all 0.15s ease;
}

.attach-button:hover {
    background-color: hsl(var(--muted));
    color: hsl(var(--foreground));
    border-color: hsl(var(--primary));
}
```

### 2.3 Enhance ChatMessageItem.razor
**File**: `AGUIDojoClient/Components/Pages/Chat/ChatMessageItem.razor` (MODIFY)

Add support for rendering AttachmentContent in user messages:

```razor
@if (Message.Role == ChatRole.User)
{
    <div class="user-message-wrapper" role="article" aria-label="You said">
        <!-- Existing branch nav, edit mode code... -->
        
        @if (!_isEditing)
        {
            <div class="user-message-content">
                <!-- Text content -->
                <div class="user-message">
                    @Message.Text
                    <button class="edit-message-btn" @onclick="StartEdit" 
                            aria-label="Edit message" title="Edit message">
                        <!-- SVG icon -->
                    </button>
                </div>

                <!-- Attachment content -->
                @{
                    var attachments = Message.Contents.OfType<AttachmentContent>().ToList();
                }
                
                @if (attachments.Count > 0)
                {
                    <div class="user-attachments">
                        @foreach (var attachment in attachments)
                        {
                            <div class="attachment-in-message">
                                @if (IsImageAttachment(attachment))
                                {
                                    <img src="@attachment.PreviewUrl" 
                                         alt="@attachment.FileName"
                                         class="attachment-image"
                                         title="@attachment.FileName" />
                                }
                                else
                                {
                                    <div class="attachment-file-badge">
                                        @if (IsPdfAttachment(attachment))
                                        {
                                            <span class="icon">📄</span>
                                        }
                                        else
                                        {
                                            <span class="icon">📎</span>
                                        }
                                        <span class="filename">@attachment.FileName</span>
                                    </div>
                                }
                            </div>
                        }
                    </div>
                }
            </div>
        }
    </div>
}

@code {
    private bool IsImageAttachment(AttachmentContent attachment) =>
        attachment.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    private bool IsPdfAttachment(AttachmentContent attachment) =>
        attachment.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);
}
```

### 2.4 Enhance ChatMessageItem.razor.css
**File**: `AGUIDojoClient/Components/Pages/Chat/ChatMessageItem.razor.css` (MODIFY)

```css
/* User message content wrapper */
.user-message-content {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
}

/* Attachments container in message */
.user-attachments {
    display: flex;
    flex-direction: column;
    gap: 0.375rem;
    margin-top: 0.375rem;
}

.attachment-in-message {
    display: flex;
    align-items: center;
}

/* Image attachment in message */
.attachment-image {
    max-width: 15rem;
    max-height: 10rem;
    border-radius: 0.5rem;
    border: 1px solid hsl(var(--border));
    object-fit: cover;
    cursor: pointer;
    transition: border-color 0.15s ease;
}

.attachment-image:hover {
    border-color: hsl(var(--primary));
}

/* File attachment badge */
.attachment-file-badge {
    display: inline-flex;
    align-items: center;
    gap: 0.5rem;
    padding: 0.5rem 0.75rem;
    background: hsl(var(--accent-violet) / 0.1);
    border: 1px solid hsl(var(--accent-violet) / 0.3);
    border-radius: 0.375rem;
    font-size: 0.875rem;
}

.attachment-file-badge .icon {
    font-size: 1.25rem;
}

.attachment-file-badge .filename {
    color: hsl(var(--accent-violet));
    font-weight: 500;
}
```

---

## 3. Service Layer Modifications

### 3.1 Update AgentStreamingService
**File**: `AGUIDojoClient/Services/AgentStreamingService.cs` (MODIFY)

In the `RunSessionResponseAsync` method, add handling for AttachmentContent:

```csharp
// In the content processing loop (around line 405):

foreach (AIContent content in update.Contents)
{
    if (content is AttachmentContent attachmentContent)
    {
        // Store attachment reference for potential further processing
        streamingMessage.Contents.Add(attachmentContent);
        _observabilityService.LogAttachmentProcessed(attachmentContent.FileName, attachmentContent.ContentType);
    }
    // ... existing content handlers ...
}
```

### 3.2 Create Attachment Service (Optional)
**File**: `AGUIDojoClient/Services/AttachmentService.cs` (NEW)

```csharp
namespace AGUIDojoClient.Services;

/// <summary>
/// Service for managing attachment uploads and references.
/// </summary>
public interface IAttachmentService
{
    /// <summary>
    /// Upload attachment bytes and returns a reference or URL.
    /// </summary>
    Task<string> UploadAttachmentAsync(string fileName, string contentType, byte[] data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get attachment by reference/ID.
    /// </summary>
    Task<byte[]?> DownloadAttachmentAsync(string attachmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clean up attachment (delete server-side copy).
    /// </summary>
    Task DeleteAttachmentAsync(string attachmentId, CancellationToken cancellationToken = default);
}

public sealed class AttachmentService : IAttachmentService
{
    private readonly HttpClient _httpClient;

    public AttachmentService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> UploadAttachmentAsync(string fileName, string contentType, byte[] data, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(data), "file", fileName);

        var response = await _httpClient.PostAsync("/api/attachments/upload", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadAsAsync<AttachmentUploadResponse>();
        return result.AttachmentId;
    }

    public async Task<byte[]?> DownloadAttachmentAsync(string attachmentId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/attachments/{attachmentId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    public async Task DeleteAttachmentAsync(string attachmentId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"/api/attachments/{attachmentId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

public record AttachmentUploadResponse(string AttachmentId, string Url, long Size);
```

---

## 4. Backend API Modifications

### 4.1 Create Attachment Endpoints
**File**: `AGUIDojoServer/Api/AttachmentEndpoints.cs` (NEW)

```csharp
using Microsoft.AspNetCore.Mvc;

namespace AGUIDojoServer.Api;

/// <summary>
/// API endpoints for managing file attachments.
/// </summary>
internal static class AttachmentEndpoints
{
    /// <summary>
    /// Maps attachment endpoints to the application.
    /// </summary>
    public static RouteGroupBuilder MapAttachmentEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/attachments/upload", UploadAttachmentAsync)
            .WithName("UploadAttachment")
            .WithSummary("Upload an attachment file")
            .WithDescription("Upload a file (image, document, etc.) and receive a reference ID")
            .DisableAntiforgery()
            .Produces<AttachmentUploadResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status413PayloadTooLarge);

        group.MapGet("/attachments/{attachmentId}", DownloadAttachmentAsync)
            .WithName("DownloadAttachment")
            .WithSummary("Download an attachment file")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/attachments/{attachmentId}", DeleteAttachmentAsync)
            .WithName("DeleteAttachment")
            .WithSummary("Delete an attachment")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> UploadAttachmentAsync(
        IFormFile file,
        IAttachmentStorage storage,
        CancellationToken cancellationToken)
    {
        const long MaxFileSize = 50 * 1024 * 1024; // 50MB

        if (file.Length == 0 || file.Length > MaxFileSize)
        {
            return TypedResults.BadRequest("Invalid file size.");
        }

        using var stream = file.OpenReadStream();
        var buffer = new byte[file.Length];
        await stream.ReadExactlyAsync(buffer, cancellationToken);

        var attachmentId = await storage.SaveAttachmentAsync(file.FileName, file.ContentType, buffer, cancellationToken);

        return TypedResults.Ok(new AttachmentUploadResponse(
            AttachmentId: attachmentId,
            Url: $"/api/attachments/{attachmentId}",
            Size: file.Length));
    }

    private static async Task<IResult> DownloadAttachmentAsync(
        string attachmentId,
        IAttachmentStorage storage,
        CancellationToken cancellationToken)
    {
        var attachment = await storage.GetAttachmentAsync(attachmentId, cancellationToken);
        if (attachment is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.File(attachment.Data, attachment.ContentType, attachment.FileName);
    }

    private static async Task<IResult> DeleteAttachmentAsync(
        string attachmentId,
        IAttachmentStorage storage,
        CancellationToken cancellationToken)
    {
        await storage.DeleteAttachmentAsync(attachmentId, cancellationToken);
        return TypedResults.NoContent();
    }
}

public record AttachmentUploadResponse(string AttachmentId, string Url, long Size);

public record Attachment(string Id, string FileName, string ContentType, byte[] Data);

public interface IAttachmentStorage
{
    Task<string> SaveAttachmentAsync(string fileName, string contentType, byte[] data, CancellationToken cancellationToken);
    Task<Attachment?> GetAttachmentAsync(string attachmentId, CancellationToken cancellationToken);
    Task DeleteAttachmentAsync(string attachmentId, CancellationToken cancellationToken);
}
```

### 4.2 Register Attachment Service in Program.cs
**File**: `AGUIDojoServer/Program.cs` (MODIFY)

```csharp
// After existing service registrations:

// Register attachment storage (implementation depends on your storage strategy)
builder.Services.AddScoped<IAttachmentStorage, FileSystemAttachmentStorage>();
// or
// builder.Services.AddScoped<IAttachmentStorage, BlobAttachmentStorage>(); // for Azure Blob Storage

// In endpoint mapping section:
apiGroup.MapAttachmentEndpoints();
```

### 4.3 Implement Attachment Storage
**File**: `AGUIDojoServer/Services/FileSystemAttachmentStorage.cs` (NEW)

```csharp
namespace AGUIDojoServer.Services;

/// <summary>
/// File system-based implementation of attachment storage.
/// </summary>
public class FileSystemAttachmentStorage : IAttachmentStorage
{
    private readonly IWebHostEnvironment _environment;
    private readonly string _storagePath;

    public FileSystemAttachmentStorage(IWebHostEnvironment environment)
    {
        _environment = environment;
        _storagePath = Path.Combine(environment.ContentRootPath, "attachments");
        Directory.CreateDirectory(_storagePath);
    }

    public async Task<string> SaveAttachmentAsync(string fileName, string contentType, byte[] data, CancellationToken cancellationToken)
    {
        var attachmentId = Guid.NewGuid().ToString("N");
        var directory = Path.Combine(_storagePath, attachmentId);
        Directory.CreateDirectory(directory);

        var metadataPath = Path.Combine(directory, "metadata.json");
        var dataPath = Path.Combine(directory, "data");

        // Save metadata
        var metadata = new { FileName = fileName, ContentType = contentType, Size = data.Length };
        await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(metadata), cancellationToken);

        // Save file data
        await File.WriteAllBytesAsync(dataPath, data);

        return attachmentId;
    }

    public async Task<Attachment?> GetAttachmentAsync(string attachmentId, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(_storagePath, attachmentId);
        if (!Directory.Exists(directory))
            return null;

        var metadataPath = Path.Combine(directory, "metadata.json");
        var dataPath = Path.Combine(directory, "data");

        if (!File.Exists(metadataPath) || !File.Exists(dataPath))
            return null;

        var metadataJson = await File.ReadAllTextAsync(metadataPath, cancellationToken);
        var metadata = JsonDocument.Parse(metadataJson);
        var root = metadata.RootElement;

        var fileName = root.GetProperty("FileName").GetString() ?? "attachment";
        var contentType = root.GetProperty("ContentType").GetString() ?? "application/octet-stream";
        var data = await File.ReadAllBytesAsync(dataPath, cancellationToken);

        return new Attachment(attachmentId, fileName, contentType, data);
    }

    public async Task DeleteAttachmentAsync(string attachmentId, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(_storagePath, attachmentId);
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        await Task.CompletedTask;
    }
}
```

---

## 5. Chat Message Sending Flow - Updated

### Current Flow
```
ChatInput.OnSend → AddUserMessageAsync → Dispatcher → StreamingService → Agent
```

### Enhanced Flow with Attachments
```
ChatInput with Files Selected
    ↓
OnSend EventCallback (message + attachments list)
    ↓
Create ChatMessage with Text
    ↓
For each attachment:
    ├─ Create AttachmentContent
    ├─ Add to ChatMessage.Contents
    └─ (Optional) Upload to server if using reference IDs
    ↓
Dispatcher.Dispatch(AddMessageAction)
    ↓
Fluxor Store Updated
    ↓
StreamingService.ProcessAgentResponseAsync()
    ↓
IChatClient receives ChatMessage with Contents:
    ├─ TextContent (message text)
    └─ AttachmentContent[] (file data)
    ↓
Agent processes message with attachments
    ↓
If tool supports multimodal:
    ├─ Tool receives attachment data
    ├─ Processes (analyze image, extract PDF text, etc.)
    └─ Returns result
    ↓
Response streamed back with results
```

---

## 6. Model Integration - Content Type System

Update ChatMessageItem.razor content priority to include attachments:

```csharp
private static int GetContentPriority(AIContent content)
{
    return content switch
    {
        FunctionCallContent => 1,      // Tool calls first
        FunctionResultContent => 2,    // Tool results second
        AttachmentContent => 2.5,      // Attachments (visual) near results
        DataContent => 3,              // Data content third
        TextContent => 4,              // Text/assistant messages fourth
        ErrorContent => 5,             // Errors last
        _ => 99                        // Unknown types at the end
    };
}
```

---

## 7. CSS Enhancements Summary

**Files to Update**:
1. `ChatInput.razor.css` - Add attachment preview styling
2. `ChatMessageItem.razor.css` - Add attachment display styling

**Key Classes Added**:
- `.attachment-previews` - Container for selected files
- `.attachment-preview-item` - Individual file preview (upload form)
- `.attachment-in-message` - Attachment rendered in message
- `.attachment-image` - Image styling
- `.attachment-file-badge` - Document/file icon + name

---

## 8. Testing Considerations

### Unit Tests
- AttachmentContent model serialization/deserialization
- File size validation
- Content type detection
- Attachment storage CRUD operations

### Integration Tests
- Upload → Store → Download flow
- Message with mixed content (text + attachments)
- Attachment cleanup on session delete

### E2E Tests
- Select file in ChatInput
- Send message with attachment
- Verify attachment renders in ChatMessageItem
- Tool processes attachment and returns result
- Verify result displays correctly

---

## 9. Migration Path

### Phase 1: Core Infrastructure
1. Create AttachmentContent model
2. Extend ChatInput.razor with file input
3. Add attachment preview rendering
4. Wire up OnSend to include attachments

### Phase 2: Backend Support
1. Create IAttachmentStorage interface
2. Implement FileSystemAttachmentStorage
3. Add AttachmentEndpoints
4. Register services in Program.cs

### Phase 3: Tool Integration
1. Update system prompt with multimodal guidelines
2. Create tools that support attachments (analyze_image, extract_pdf, etc.)
3. Test end-to-end flows

### Phase 4: Polish
1. Error handling and notifications
2. Attachment size limits and validation
3. Preview generation (thumbnails, document pages)
4. Performance optimization for large files

