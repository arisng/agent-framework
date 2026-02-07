// Copyright (c) Microsoft. All rights reserved.

using Microsoft.JSInterop;

namespace AGUIDojoClient.Services;

/// <summary>
/// Scoped service that detects viewport width via JS interop and provides
/// an <see cref="IsMobile"/> property with a configurable breakpoint (default 768px).
/// Fires <see cref="OnViewportChanged"/> when the viewport crosses the breakpoint.
/// Implements <see cref="IAsyncDisposable"/> to clean up JS resize listeners.
/// </summary>
/// <remarks>
/// Usage:
/// <list type="number">
///   <item>Inject <see cref="ViewportService"/> into a Blazor component.</item>
///   <item>Call <see cref="InitializeAsync"/> in <c>OnAfterRenderAsync(firstRender)</c>.</item>
///   <item>Read <see cref="IsMobile"/> for conditional rendering.</item>
///   <item>Subscribe to <see cref="OnViewportChanged"/> for dynamic updates.</item>
/// </list>
/// </remarks>
public sealed class ViewportService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;

    /// <summary>
    /// The viewport width breakpoint in pixels. Widths strictly less than
    /// this value are considered mobile.
    /// </summary>
    public const int MobileBreakpoint = 768;

    private IJSObjectReference? _jsModule;
    private DotNetObjectReference<ViewportService>? _dotNetRef;
    private int _viewportWidth;
    private bool _initialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="ViewportService"/> class.
    /// </summary>
    /// <param name="jsRuntime">The Blazor JS interop runtime.</param>
    public ViewportService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Gets a value indicating whether the current viewport width is below
    /// the <see cref="MobileBreakpoint"/> threshold.
    /// </summary>
    public bool IsMobile => _viewportWidth < MobileBreakpoint;

    /// <summary>
    /// Gets the current viewport width in pixels. Returns 0 before
    /// <see cref="InitializeAsync"/> has been called.
    /// </summary>
    public int ViewportWidth => _viewportWidth;

    /// <summary>
    /// Raised when the viewport crosses the <see cref="MobileBreakpoint"/> threshold
    /// (i.e., when <see cref="IsMobile"/> changes). Not raised for resizes that
    /// stay on the same side of the breakpoint.
    /// </summary>
    public event EventHandler? OnViewportChanged;

    /// <summary>
    /// Initializes the JS interop module, reads the current viewport width,
    /// and registers the resize listener. Must be called from
    /// <c>OnAfterRenderAsync(firstRender: true)</c>.
    /// </summary>
    /// <returns>A task that completes when initialization is done.</returns>
    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _jsModule = await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/viewport.js");

        _viewportWidth = await _jsModule.InvokeAsync<int>("getViewportWidth");

        _dotNetRef = DotNetObjectReference.Create(this);

        await _jsModule.InvokeVoidAsync("onResize", _dotNetRef, nameof(OnBrowserResize));

        _initialized = true;
    }

    /// <summary>
    /// Called from JavaScript when the browser viewport is resized.
    /// Fires <see cref="OnViewportChanged"/> only when crossing the breakpoint.
    /// </summary>
    /// <param name="newWidth">The new viewport width in pixels.</param>
    [JSInvokable]
    public void OnBrowserResize(int newWidth)
    {
        bool wasMobile = IsMobile;
        _viewportWidth = newWidth;
        bool isMobileNow = IsMobile;

        // Only notify when the mobile/desktop state actually changes
        if (wasMobile != isMobileNow)
        {
            OnViewportChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Disposes the JS module and removes the resize listener to prevent
    /// memory leaks in the browser.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_jsModule is not null)
        {
            try
            {
                await _jsModule.InvokeVoidAsync("dispose");
                await _jsModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit disconnected — JS cleanup is no longer possible.
                // This is expected during Blazor Server circuit teardown.
            }
        }

        _dotNetRef?.Dispose();
        _jsModule = null;
        _dotNetRef = null;
        _initialized = false;
    }
}
