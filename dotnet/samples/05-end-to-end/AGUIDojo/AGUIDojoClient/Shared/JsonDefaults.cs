using System.Text.Json;
using System.Text.Json.Serialization;

namespace AGUIDojoClient.Shared;

/// <summary>
/// Provides shared <see cref="JsonSerializerOptions"/> instances to avoid
/// repeated allocations across the application. The options are configured
/// with web-compatible defaults (camelCase property names, case-insensitive
/// property matching) and common converters.
/// </summary>
public static class JsonDefaults
{
    /// <summary>
    /// Gets the default <see cref="JsonSerializerOptions"/> instance configured
    /// with <see cref="JsonSerializerDefaults.Web"/> defaults,
    /// case-insensitive property name matching, and a
    /// <see cref="JsonStringEnumConverter"/> for enum serialization.
    /// </summary>
    /// <remarks>
    /// This instance is thread-safe and should be used in place of creating
    /// <c>new JsonSerializerOptions { PropertyNameCaseInsensitive = true }</c>
    /// throughout the codebase.
    /// </remarks>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Gets a <see cref="JsonSerializerOptions"/> instance configured for
    /// indented (pretty-printed) JSON output, suitable for display purposes.
    /// </summary>
    public static JsonSerializerOptions Indented { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
}
