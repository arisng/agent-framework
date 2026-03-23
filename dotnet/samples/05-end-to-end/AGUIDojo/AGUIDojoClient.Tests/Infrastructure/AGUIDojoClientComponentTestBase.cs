using BlazorBlueprint.Components;
using Fluxor;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace AGUIDojoClient.Tests.Infrastructure;

public abstract class AGUIDojoClientComponentTestBase : BunitContext
{
    protected AGUIDojoClientComponentTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddAuthorizationCore();
        Services.AddBlazorBlueprintComponents();
        Services.AddFluxor(options => options.ScanAssemblies(typeof(AGUIDojoClient.Services.IStateManager).Assembly));
        Services.AddLogging();
        Services.AddOptions();
    }
}
