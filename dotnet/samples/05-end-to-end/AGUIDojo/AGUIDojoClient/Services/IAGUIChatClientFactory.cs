using Microsoft.Extensions.AI;

namespace AGUIDojoClient.Services;

/// <summary>
/// Factory interface for creating AGUI chat clients that target the unified /chat endpoint.
/// </summary>
public interface IAGUIChatClientFactory
{
    /// <summary>
     /// Creates an <see cref="IChatClient"/> for the unified AG-UI route.
    /// </summary>
    /// <returns>An <see cref="IChatClient"/> configured to communicate with the unified endpoint.</returns>
    IChatClient CreateClient();
}
