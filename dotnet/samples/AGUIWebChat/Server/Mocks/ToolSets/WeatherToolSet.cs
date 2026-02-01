// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AGUIWebChat.Server.Mocks.ToolSets;

/// <summary>
/// Implements the weather tool set that simulates weather-related queries by emitting
/// a <c>get_weather</c> tool call followed by optional follow-up text.
/// </summary>
/// <remarks>
/// <para>
/// This tool set demonstrates a simple single-tool sequence:
/// </para>
/// <list type="number">
/// <item><description>Emit <c>get_weather</c> with a location argument</description></item>
/// <item><description>Optionally stream follow-up text contextualizing the weather information</description></item>
/// </list>
/// <para>
/// The sequence demonstrates how an LLM would handle a weather-related query by calling
/// an external weather API tool and then providing conversational context about the results.
/// </para>
/// </remarks>
public sealed class WeatherToolSet : IToolSet
{
    /// <summary>
    /// Default location to use when the user message doesn't specify a location.
    /// </summary>
    private const string DefaultLocation = "Seattle, WA";

    /// <summary>
    /// Common location keywords to extract from user messages.
    /// Maps common phrases to standardized location names.
    /// </summary>
    private static readonly Dictionary<string, string> LocationKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["seattle"] = "Seattle, WA",
        ["new york"] = "New York, NY",
        ["nyc"] = "New York, NY",
        ["los angeles"] = "Los Angeles, CA",
        ["la"] = "Los Angeles, CA",
        ["chicago"] = "Chicago, IL",
        ["san francisco"] = "San Francisco, CA",
        ["sf"] = "San Francisco, CA",
        ["boston"] = "Boston, MA",
        ["miami"] = "Miami, FL",
        ["denver"] = "Denver, CO",
        ["austin"] = "Austin, TX",
        ["london"] = "London, UK",
        ["paris"] = "Paris, France",
        ["tokyo"] = "Tokyo, Japan"
    };

    /// <summary>
    /// The configuration options for this tool set.
    /// </summary>
    private readonly MockAgentOptions _options;

    /// <summary>
    /// Whether to stream follow-up text after the tool call.
    /// </summary>
    private readonly bool _includeFollowUpText;

    /// <summary>
    /// The follow-up text template to stream after the tool result.
    /// </summary>
    private readonly string _followUpTextTemplate;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeatherToolSet"/> class with default configuration.
    /// </summary>
    public WeatherToolSet()
        : this(options: null, includeFollowUpText: true, followUpTextTemplate: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WeatherToolSet"/> class with custom configuration.
    /// </summary>
    /// <param name="options">The mock agent options containing delay configuration. If <see langword="null"/>, default options are used.</param>
    /// <param name="includeFollowUpText">Whether to stream follow-up text after the tool call. Defaults to <see langword="true"/>.</param>
    /// <param name="followUpTextTemplate">The follow-up text template. Use {location} placeholder for the location. If <see langword="null"/>, uses a default template.</param>
    public WeatherToolSet(MockAgentOptions? options = null, bool includeFollowUpText = true, string? followUpTextTemplate = null)
    {
        _options = options ?? new MockAgentOptions();
        _includeFollowUpText = includeFollowUpText;
        _followUpTextTemplate = followUpTextTemplate ?? "I'll check the current weather conditions for {location} and provide you with the details.";
    }

    /// <summary>
    /// Gets the unique name of this tool set.
    /// </summary>
    /// <value>Returns "WeatherTools".</value>
    public string Name => "WeatherTools";

    /// <summary>
    /// Gets the keywords or phrases that trigger this tool set.
    /// </summary>
    /// <value>A read-only list containing "weather", "temperature", and "forecast".</value>
    public IReadOnlyList<string> TriggerKeywords { get; } = new[] { "weather", "temperature", "forecast" };

    /// <summary>
    /// Executes the weather tool sequence, emitting <c>get_weather</c> followed by its result and optional follow-up text.
    /// </summary>
    /// <param name="context">The execution context containing response metadata and helper methods.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>
    /// An asynchronous enumerable of <see cref="AgentResponseUpdate"/> instances containing
    /// the <c>get_weather</c> tool call, its result, and optional streamed follow-up text.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The sequence demonstrates a typical single-tool workflow:
    /// </para>
    /// <list type="number">
    /// <item><description>Optionally stream introductory text</description></item>
    /// <item><description>Emit <c>get_weather</c> FunctionCallContent with location argument extracted from user message</description></item>
    /// <item><description>Emit FunctionResultContent with weather data (temperature, conditions, humidity, wind, etc.)</description></item>
    /// </list>
    /// <para>
    /// Location extraction attempts to find city names in the user message. If no location
    /// is found, a default location is used.
    /// </para>
    /// <para>
    /// The FunctionResultContent is essential for proper UI rendering. The client uses the result content
    /// to render the weather card component based on media type detection.
    /// </para>
    /// </remarks>
    public async IAsyncEnumerable<AgentResponseUpdate> ExecuteSequenceAsync(
        MockSequenceContext context,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        ILogger<WeatherToolSet> logger = context.CreateLogger<WeatherToolSet>();
        Stopwatch overallStopwatch = Stopwatch.StartNew();

        // Extract location from user message or use default
        string location = ExtractLocation(context.UserMessage);

        logger.LogInformation("[WeatherToolSet] Starting weather sequence for location: {Location}, loading delay: {DelayMs}ms", location, _options.WeatherLoadingDelayMs);

        // Optional: Stream follow-up text before the tool call
        if (_includeFollowUpText)
        {
            string followUpText = _followUpTextTemplate.Replace("{location}", location);
            await foreach (AgentResponseUpdate update in context.StreamTextAsync(followUpText, cancellationToken).ConfigureAwait(false))
            {
                yield return update;
            }

            // Small delay between text and tool call for natural feel
            if (context.StreamingDelayMs > 0)
            {
                await Task.Delay(context.StreamingDelayMs * 2, cancellationToken).ConfigureAwait(false);
            }
        }

        // Emit get_weather tool call
        Dictionary<string, object?> getWeatherArgs = new()
        {
            ["location"] = location
        };

        Stopwatch toolStopwatch = Stopwatch.StartNew();
        logger.LogInformation("[WeatherToolSet] Emitting tool call: get_weather for {Location}", location);

        AgentResponseUpdate weatherCallUpdate = context.CreateToolCallUpdate("get_weather", getWeatherArgs);
        string weatherCallId = ((FunctionCallContent)weatherCallUpdate.Contents[0]).CallId;
        yield return weatherCallUpdate;

        // Apply loading delay to allow UI to show loading state before weather card appears
        if (_options.WeatherLoadingDelayMs > 0)
        {
            logger.LogDebug("[WeatherToolSet] Waiting {DelayMs}ms to simulate weather data loading", _options.WeatherLoadingDelayMs);
            await Task.Delay(_options.WeatherLoadingDelayMs, cancellationToken).ConfigureAwait(false);
        }

        // Emit FunctionResultContent with weather data
        // Format: {"city":"Seattle","temperature":20,"feelsLike":25,"humidity":50,"wind":10,"conditions":"sunny"}
        Dictionary<string, object> weatherResult = GenerateWeatherData(location);
        yield return context.CreateToolResultUpdate(weatherCallId, "get_weather", weatherResult);

        toolStopwatch.Stop();
        logger.LogInformation("[WeatherToolSet] Completed tool call: get_weather in {ElapsedMs}ms", toolStopwatch.ElapsedMilliseconds);

        overallStopwatch.Stop();
        logger.LogInformation("[WeatherToolSet] Weather sequence completed in {TotalElapsedMs}ms", overallStopwatch.ElapsedMilliseconds);
    }

    /// <summary>
    /// Generates mock weather data for a given location.
    /// </summary>
    /// <param name="location">The location to generate weather data for.</param>
    /// <returns>A dictionary containing weather data with city, temperature, feelsLike, humidity, wind, and conditions.</returns>
    private static Dictionary<string, object> GenerateWeatherData(string location)
    {
        // Extract city name from location (e.g., "Seattle, WA" -> "Seattle")
        string city = location.Contains(',') ? location.Split(',')[0].Trim() : location;

        // Generate deterministic but varied weather based on city name hash
        int hash = city.GetHashCode(StringComparison.OrdinalIgnoreCase);
        int tempBase = Math.Abs(hash % 25) + 10; // Temperature between 10-35
        int humidityBase = Math.Abs((hash >> 4) % 50) + 30; // Humidity between 30-80
        int windBase = Math.Abs((hash >> 8) % 20) + 5; // Wind between 5-25

        string[] conditions = ["sunny", "partly cloudy", "cloudy", "rainy", "windy"];
        string condition = conditions[Math.Abs((hash >> 12) % conditions.Length)];

        return new Dictionary<string, object>
        {
            ["city"] = city,
            ["temperature"] = tempBase,
            ["feelsLike"] = tempBase + (Math.Abs((hash >> 16) % 5) - 2), // +/- 2 degrees from actual
            ["humidity"] = humidityBase,
            ["wind"] = windBase,
            ["conditions"] = condition
        };
    }

    /// <summary>
    /// Extracts a location from the user message by searching for known location keywords.
    /// </summary>
    /// <param name="userMessage">The user's message to search for location keywords.</param>
    /// <returns>The extracted location, or <see cref="DefaultLocation"/> if no location is found.</returns>
    private static string ExtractLocation(string? userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return DefaultLocation;
        }

        // Search for known locations in the user message
        foreach (KeyValuePair<string, string> locationEntry in LocationKeywords)
        {
            if (userMessage.Contains(locationEntry.Key, StringComparison.OrdinalIgnoreCase))
            {
                return locationEntry.Value;
            }
        }

        return DefaultLocation;
    }
}
