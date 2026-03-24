using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace AGUIDojoServer.ChatSessions;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by ChatClientAgentFactory.CreateUnifiedAgent")]
internal sealed class ConversationPersistenceAgent : DelegatingAIAgent
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ConversationPersistenceAgent(AIAgent innerAgent, IHttpContextAccessor httpContextAccessor)
        : base(innerAgent)
    {
        this._httpContextAccessor = httpContextAccessor;
    }

    protected override Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return this.RunCoreStreamingAsync(messages, session, options, cancellationToken)
            .ToAgentResponseAsync(cancellationToken);
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<ChatMessage> inputMessages = messages.ToList();
        List<AgentResponseUpdate> responseUpdates = [];

        await foreach (AgentResponseUpdate update in this.InnerAgent.RunStreamingAsync(inputMessages, session, options, cancellationToken).ConfigureAwait(false))
        {
            responseUpdates.Add(update);
            yield return update;
        }

        await PersistConversationAsync(inputMessages, responseUpdates, cancellationToken).ConfigureAwait(false);
    }

    private async Task PersistConversationAsync(
        List<ChatMessage> inputMessages,
        List<AgentResponseUpdate> responseUpdates,
        CancellationToken cancellationToken)
    {
        HttpContext? httpContext = this._httpContextAccessor.HttpContext;
        if (httpContext is null ||
            !httpContext.Items.TryGetValue(ChatSessionHttpContextItems.SessionId, out object? rawSessionId) ||
            rawSessionId is not string sessionId ||
            string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        ChatConversationService? conversationService = httpContext.RequestServices.GetService<ChatConversationService>();
        if (conversationService is null)
        {
            return;
        }

        List<ChatMessage> conversationPath = [.. inputMessages];
        if (responseUpdates.Count > 0)
        {
            AgentResponse response = responseUpdates.ToAgentResponse();
            conversationPath.AddRange(response.Messages);
        }

        await conversationService.PersistConversationAsync(sessionId, conversationPath, cancellationToken).ConfigureAwait(false);
    }
}
