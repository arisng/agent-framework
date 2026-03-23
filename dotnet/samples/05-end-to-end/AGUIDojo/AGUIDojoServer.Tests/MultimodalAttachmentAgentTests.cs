using System.Reflection;
using System.Text.Json;
using AGUIDojoServer.Api;
using AGUIDojoServer.Multimodal;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace AGUIDojoServer.Tests;

public class MultimodalAttachmentAgentTests
{
    private static readonly JsonSerializerOptions s_caseInsensitiveJson = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task RunStreamingAsync_WithStoredAttachment_StripsMarkerAndAddsImageContent()
    {
        // Arrange
        InMemoryFileStorageService storage = new();
        FileData storedFile = storage.Store("sunrise.png", "image/png", [1, 2, 3, 4]);
        RecordingAgent innerAgent = new();
        MultimodalAttachmentAgent agent = new(innerAgent, storage);
        ChatMessage message = new(ChatRole.User, $"Describe the scene <!-- file:{storedFile.Id}:sunrise.png:image/png -->");

        // Act
        await DrainAsync(agent.RunStreamingAsync([message]));

        // Assert
        ChatMessage forwarded = Assert.Single(innerAgent.ReceivedMessages);
        Assert.Equal(ChatRole.User, forwarded.Role);
        Assert.Equal("Describe the scene", forwarded.Text);

        DataContent image = Assert.Single(forwarded.Contents.OfType<DataContent>());
        Assert.Equal("image/png", image.MediaType);
        Assert.Equal("sunrise.png", image.Name);
    }

    [Fact]
    public async Task RunStreamingAsync_WithMissingAndPresentAttachments_AppendsUnavailableNotice()
    {
        // Arrange
        InMemoryFileStorageService storage = new();
        FileData storedFile = storage.Store("available.png", "image/png", [9, 8, 7]);
        RecordingAgent innerAgent = new();
        MultimodalAttachmentAgent agent = new(innerAgent, storage);
        ChatMessage message = new(
            ChatRole.User,
            $"Review these images <!-- file:{storedFile.Id}:available.png:image/png --> <!-- file:deadbeef:missing.png:image/png -->");

        // Act
        await DrainAsync(agent.RunStreamingAsync([message]));

        // Assert
        ChatMessage forwarded = Assert.Single(innerAgent.ReceivedMessages);
        Assert.Equal(
            "Review these images" + Environment.NewLine + Environment.NewLine + "[Image attachment unavailable: missing.png]",
            forwarded.Text);

        DataContent image = Assert.Single(forwarded.Contents.OfType<DataContent>());
        Assert.Equal("available.png", image.Name);
    }

    [Fact]
    public async Task RunStreamingAsync_WithAssistantMessage_DoesNotResolveMarkers()
    {
        // Arrange
        InMemoryFileStorageService storage = new();
        FileData storedFile = storage.Store("assistant.png", "image/png", [1]);
        RecordingAgent innerAgent = new();
        MultimodalAttachmentAgent agent = new(innerAgent, storage);
        string originalText = $"This should stay untouched <!-- file:{storedFile.Id}:assistant.png:image/png -->";
        ChatMessage message = new(ChatRole.Assistant, originalText);

        // Act
        await DrainAsync(agent.RunStreamingAsync([message]));

        // Assert
        ChatMessage forwarded = Assert.Single(innerAgent.ReceivedMessages);
        Assert.Equal(originalText, forwarded.Text);
        Assert.Empty(forwarded.Contents.OfType<DataContent>());
    }

    [Theory]
    [InlineData("image/png", "image.png")]
    [InlineData("image/jpeg", "photo.jpg")]
    [InlineData("image/gif", "animation.gif")]
    [InlineData("image/webp", "render.webp")]
    public async Task UploadFileAsync_WithAllowedContentType_StoresFileAndReturnsMetadata(string contentType, string fileName)
    {
        // Arrange
        InMemoryFileStorageService storage = new();
        byte[] content = [1, 2, 3, 4, 5];
        DefaultHttpContext httpContext = CreateUploadContext(CreateFormFile(fileName, contentType, content));

        // Act
        IResult result = await InvokeUploadFileAsync(httpContext.Request, storage);
        ResultExecution resultExecution = await ExecuteResultAsync(result);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, resultExecution.StatusCode);

        FileUploadResponse? payload = JsonSerializer.Deserialize<FileUploadResponse>(
            resultExecution.BodyText,
            s_caseInsensitiveJson);
        Assert.NotNull(payload);
        Assert.Equal(fileName, payload.FileName);
        Assert.Equal(contentType, payload.ContentType);
        Assert.Equal(content.LongLength, payload.Size);

        FileData? stored = storage.Get(payload.Id);
        Assert.NotNull(stored);
        Assert.Equal(fileName, stored.FileName);
        Assert.Equal(contentType, stored.ContentType);
    }

    [Fact]
    public async Task UploadFileAsync_WithUnsupportedContentType_ReturnsBadRequest()
    {
        // Arrange
        InMemoryFileStorageService storage = new();
        DefaultHttpContext httpContext = CreateUploadContext(CreateFormFile("notes.txt", "text/plain", [1, 2, 3]));

        // Act
        IResult result = await InvokeUploadFileAsync(httpContext.Request, storage);
        ResultExecution resultExecution = await ExecuteResultAsync(result);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, resultExecution.StatusCode);
        Assert.Contains("not supported", resultExecution.BodyText);
    }

    [Fact]
    public async Task UploadFileAsync_WithOversizedFile_ReturnsBadRequest()
    {
        // Arrange
        InMemoryFileStorageService storage = new();
        byte[] content = new byte[(10 * 1024 * 1024) + 1];
        DefaultHttpContext httpContext = CreateUploadContext(CreateFormFile("huge.png", "image/png", content));

        // Act
        IResult result = await InvokeUploadFileAsync(httpContext.Request, storage);
        ResultExecution resultExecution = await ExecuteResultAsync(result);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, resultExecution.StatusCode);
        Assert.Contains("File too large", resultExecution.BodyText);
    }

    [Fact]
    public async Task GetFile_WithMissingAttachment_ReturnsPlaceholderSvg()
    {
        // Arrange
        InMemoryFileStorageService storage = new();

        // Act
        FileContentHttpResult result = InvokeGetFile("missing-file", storage);
        ResultExecution resultExecution = await ExecuteResultAsync(result);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, resultExecution.StatusCode);
        Assert.Equal("image/svg+xml", resultExecution.ContentType);
        Assert.Contains("Attachment unavailable", resultExecution.BodyText);
    }

    private static DefaultHttpContext CreateUploadContext(FormFile? file)
    {
        DefaultHttpContext context = CreateHttpContext();
        context.Request.ContentType = "multipart/form-data; boundary=test-boundary";

        FormFileCollection files = [];
        if (file is not null)
        {
            files.Add(file);
        }

        context.Request.Form = new FormCollection([], files);
        return context;
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        DefaultHttpContext context = new();
        context.RequestServices = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static FormFile CreateFormFile(string fileName, string contentType, byte[] content)
    {
        MemoryStream stream = new(content);
        return new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };
    }

    private static async Task<IResult> InvokeUploadFileAsync(HttpRequest request, IFileStorageService storage)
    {
        MethodInfo method = typeof(FileUploadEndpoints).GetMethod("UploadFileAsync", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("UploadFileAsync not found.");

        Task<IResult> invocation = (Task<IResult>)(method.Invoke(null, [request, storage])
            ?? throw new InvalidOperationException("UploadFileAsync returned null."));

        return await invocation;
    }

    private static FileContentHttpResult InvokeGetFile(string id, IFileStorageService storage)
    {
        MethodInfo method = typeof(FileUploadEndpoints).GetMethod("GetFile", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("GetFile not found.");

        return (FileContentHttpResult)(method.Invoke(null, [id, storage])
            ?? throw new InvalidOperationException("GetFile returned null."));
    }

    private static async Task<ResultExecution> ExecuteResultAsync(IResult result)
    {
        DefaultHttpContext context = CreateHttpContext();
        await result.ExecuteAsync(context);

        MemoryStream body = (MemoryStream)context.Response.Body;
        return new ResultExecution(
            context.Response.StatusCode,
            context.Response.ContentType,
            body.ToArray(),
            body.Length == 0 ? string.Empty : System.Text.Encoding.UTF8.GetString(body.ToArray()));
    }

    private static async Task DrainAsync(IAsyncEnumerable<AgentResponseUpdate> updates)
    {
        await foreach (AgentResponseUpdate _ in updates)
        {
        }
    }

    private sealed record ResultExecution(int StatusCode, string? ContentType, byte[] Body, string BodyText);

    private sealed class RecordingAgent : AIAgent
    {
        public List<ChatMessage> ReceivedMessages { get; } = [];

        protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken = default) =>
            new(new RecordingAgentSession());

        protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
            AgentSession session,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default) =>
            new(JsonSerializer.SerializeToElement(new { }));

        protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
            JsonElement serializedState,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default) =>
            new(new RecordingAgentSession());

        protected override Task<AgentResponse> RunCoreAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ChatMessage[] captured = Capture(messages);
            return Task.FromResult(new AgentResponse(captured));
        }

        protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _ = Capture(messages);
            yield return new AgentResponseUpdate(ChatRole.Assistant, "ok");
            await Task.CompletedTask;
        }

        private ChatMessage[] Capture(IEnumerable<ChatMessage> messages)
        {
            ChatMessage[] captured = messages.ToArray();
            ReceivedMessages.AddRange(captured);
            return captured;
        }
    }

    private sealed class RecordingAgentSession : AgentSession;
}
