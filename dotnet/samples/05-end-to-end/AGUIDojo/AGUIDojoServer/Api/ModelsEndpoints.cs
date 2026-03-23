using AGUIDojoServer.Models;

namespace AGUIDojoServer.Api;

/// <summary>
/// Minimal API endpoints for the model catalog.
/// </summary>
public static class ModelsEndpoints
{
    /// <summary>
    /// Maps the /api/models endpoints to the route group.
    /// </summary>
    public static RouteGroupBuilder MapModelsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/models", (IModelRegistry registry) =>
        {
            return Results.Ok(new
            {
                models = registry.GetAvailableModels(),
                activeModelId = registry.ActiveModelId,
            });
        })
        .WithName("GetModels")
        .WithDescription("Returns the available model catalog and the currently active model.")
        .Produces<object>(StatusCodes.Status200OK);

        return group;
    }
}
