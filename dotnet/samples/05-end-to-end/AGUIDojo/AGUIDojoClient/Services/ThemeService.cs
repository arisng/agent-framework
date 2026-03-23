using Microsoft.JSInterop;

namespace AGUIDojoClient.Services;

/// <summary>
/// Scoped service that manages the application color theme by calling
/// <c>window.themeInterop</c> methods via <see cref="IJSRuntime"/>.
/// </summary>
/// <remarks>
/// <para>
/// The companion <c>wwwroot/js/theme.js</c> file exposes three functions
/// on <c>window.themeInterop</c>: <c>getTheme()</c>, <c>setTheme(theme)</c>,
/// and <c>toggleTheme()</c>. Each one reads/writes <c>localStorage['theme']</c>
/// and toggles the <c>.dark</c> class on <c>&lt;html&gt;</c>.
/// </para>
/// <para>
/// To prevent a flash of unstyled content (FOUC) on first paint, include
/// the following inline script in the <c>&lt;head&gt;</c> of <c>App.razor</c>,
/// <b>before</b> any stylesheet references:
/// <code>
/// &lt;script&gt;
///   (function() {
///     var theme = localStorage.getItem('theme');
///     if (theme === 'dark') {
///       document.documentElement.classList.add('dark');
///     }
///   })();
/// &lt;/script&gt;
/// </code>
/// </para>
/// <para>
/// Register this service in <c>Program.cs</c>:
/// <code>
/// builder.Services.AddScoped&lt;IThemeService, ThemeService&gt;();
/// </code>
/// </para>
/// </remarks>
public sealed class ThemeService : IThemeService
{
    private readonly IJSRuntime _jsRuntime;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThemeService"/> class.
    /// </summary>
    /// <param name="jsRuntime">The Blazor JS interop runtime.</param>
    public ThemeService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <inheritdoc />
    public async Task<string> GetThemeAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string>("themeInterop.getTheme");
        }
        catch (JSDisconnectedException)
        {
            // Circuit disconnected — return a safe default.
            return "light";
        }
    }

    /// <inheritdoc />
    public async Task SetThemeAsync(string theme)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("themeInterop.setTheme", theme);
        }
        catch (JSDisconnectedException)
        {
            // Circuit disconnected — JS cleanup is no longer possible.
        }
    }

    /// <inheritdoc />
    public async Task<string> ToggleThemeAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string>("themeInterop.toggleTheme");
        }
        catch (JSDisconnectedException)
        {
            // Circuit disconnected — return a safe default.
            return "light";
        }
    }
}
