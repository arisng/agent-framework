namespace AGUIDojoClient.Services;

/// <summary>
/// Abstraction for reading and writing the application color theme
/// (<c>"light"</c> or <c>"dark"</c>).
/// </summary>
/// <remarks>
/// The backing implementation uses JavaScript interop to persist the
/// user's preference in <c>localStorage</c> and to toggle the
/// <c>.dark</c> CSS class on the <c>&lt;html&gt;</c> element.
/// </remarks>
public interface IThemeService
{
    /// <summary>
    /// Reads the currently persisted theme from <c>localStorage</c>.
    /// Returns <c>"light"</c> when no value has been stored yet.
    /// </summary>
    /// <returns>
    /// A task whose result is <c>"light"</c> or <c>"dark"</c>.
    /// </returns>
    Task<string> GetThemeAsync();

    /// <summary>
    /// Applies the specified theme by toggling the <c>.dark</c> CSS class
    /// on <c>&lt;html&gt;</c> and persists the value to <c>localStorage</c>.
    /// </summary>
    /// <param name="theme">
    /// The theme to apply — either <c>"light"</c> or <c>"dark"</c>.
    /// </param>
    /// <returns>A task that completes when the JS interop call finishes.</returns>
    Task SetThemeAsync(string theme);

    /// <summary>
    /// Toggles between <c>"light"</c> and <c>"dark"</c>, persists the
    /// new value, and returns it.
    /// </summary>
    /// <returns>
    /// A task whose result is the theme after toggling
    /// (<c>"light"</c> or <c>"dark"</c>).
    /// </returns>
    Task<string> ToggleThemeAsync();
}
