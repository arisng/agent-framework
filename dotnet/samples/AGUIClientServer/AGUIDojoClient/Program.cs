// Copyright (c) Microsoft. All rights reserved.

// =============================================================================
// AGUIDojoClient - BFF (Backend-for-Frontend) with YARP Reverse Proxy
// =============================================================================
//
// This Blazor Server application serves as the BFF layer. It does NOT store
// any secrets (API keys, JWT signing keys) itself. All secrets reside on
// the backend (AGUIDojoServer). The BFF only needs to know the backend URL.
//
// REQUIRED ENVIRONMENT VARIABLES (Production):
// ----------------------------------------------
//   SERVER_URL                  - Backend server URL for direct AG-UI SSE connections
//                                 (default: http://localhost:5100)
//
//   ReverseProxy__Clusters__backend__Destinations__primary__Address
//                               - Backend URL for YARP reverse proxy
//                                 (e.g., https://api.production.example.com/)
//
// OPTIONAL ENVIRONMENT VARIABLES:
// --------------------------------
//   ASPNETCORE_ENVIRONMENT      - Environment name (Development, Staging, Production)
//   ASPNETCORE_URLS             - BFF listen URLs (default: http://localhost:5001)
//   OTEL_EXPORTER_OTLP_ENDPOINT - OpenTelemetry OTLP endpoint for production tracing
//
// SECURITY NOTES:
// ----------------
// - The BFF does NOT store API keys or JWT signing keys
// - Authorization headers from client requests are forwarded to the backend via YARP
// - All authentication and secret management is handled by AGUIDojoServer
// - In production, use HTTPS for all connections (User → BFF and BFF → Backend)
// - Never commit environment-specific URLs to source control; use environment variables
// - Development URLs are in appsettings.Development.json (localhost only)
//
// CONFIGURATION PRECEDENCE:
// --------------------------
// Configuration sources are loaded in order of precedence (highest first):
// 1. Environment variables (highest priority - use for production URLs)
// 2. appsettings.{Environment}.json (environment-specific settings)
// 3. appsettings.json (base/default configuration)
//
// For nested configuration keys, use double underscore in environment variables:
//   ReverseProxy__Clusters__backend__Destinations__primary__Address=https://api.example.com/
//
// =============================================================================

using System.Net.Http.Headers;
using AGUIDojoClient.Components;
using AGUIDojoClient.Services;
using Microsoft.Agents.AI.AGUI;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http.Resilience;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// -----------------------------------------------------------------------------
// BACKEND CONNECTION CONFIGURATION
// -----------------------------------------------------------------------------
// SERVER_URL is used by the direct HttpClient for AG-UI SSE streaming endpoints.
// This bypasses YARP to avoid potential buffering issues with Server-Sent Events.
//
// The YARP proxy URL is configured separately in appsettings.json (ReverseProxy section)
// and can be overridden via environment variable:
//   export ReverseProxy__Clusters__backend__Destinations__primary__Address=https://api.example.com/
//
// Override for production:
//   export SERVER_URL=https://api.production.example.com
// -----------------------------------------------------------------------------
string serverUrl = builder.Configuration["SERVER_URL"] ?? "http://localhost:5100";

// Register HttpClient for AGUIDojoServer (AG-UI SSE streaming)
// IMPORTANT: NO retry policy for this client. Retrying SSE/streaming requests would
// cause duplicate event streams and confuse the client. Only a circuit breaker is applied
// for fail-fast behavior when the backend is consistently unavailable.
// Reference: Research Q1.29 - NO retry for SSE; circuit breaker for fail-fast.
builder.Services.AddHttpClient("aguiserver", httpClient => httpClient.BaseAddress = new Uri(serverUrl))
    .AddResilienceHandler("aguiserver-resilience", pipeline =>
    {
        // Circuit breaker: stops sending requests when the backend is failing consistently.
        // After 5 failures within the sampling window, the circuit opens for 30 seconds,
        // allowing the backend time to recover before accepting new requests.
        pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(30),
            MinimumThroughput = 5,
            BreakDuration = TimeSpan.FromSeconds(30),
        });
    });

// -----------------------------------------------------------------------------
// OPENTELEMETRY CONFIGURATION
// -----------------------------------------------------------------------------
// OpenTelemetry provides distributed tracing and metrics for the BFF layer.
// This configuration enables:
// - ASP.NET Core request tracing (incoming HTTP requests from browser)
// - HttpClient tracing (outgoing HTTP calls including YARP-proxied requests)
// - YARP reverse proxy activity tracing ("Yarp.ReverseProxy" ActivitySource)
// - Console exporter for development visibility
// - OTLP exporter for production observability platforms (Jaeger, Zipkin, etc.)
//
// W3C Trace Context headers (traceparent, tracestate) are automatically propagated
// through YARP to the backend AGUIDojoServer, enabling end-to-end distributed tracing.
//
// Reference: Q1.25 - YARP telemetry consumers and OpenTelemetry integration
// -----------------------------------------------------------------------------
const string ServiceName = "AGUIDojoClient";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: ServiceName, serviceVersion: "1.0.0")
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName
        }))
    .WithTracing(tracing => tracing
        // Instrument incoming ASP.NET Core requests (browser → BFF)
        .AddAspNetCoreInstrumentation(options => options.RecordException = true)
        // Instrument outgoing HttpClient calls (YARP proxy → backend, AG-UI SSE calls)
        .AddHttpClientInstrumentation(options => options.RecordException = true)
        // Add YARP reverse proxy ActivitySource for proxy-specific spans
        // This captures YARP forwarding activities (inbound → outbound correlation)
        .AddSource("Yarp.ReverseProxy")
        // Console exporter for development visibility
        .AddConsoleExporter()
        // OTLP exporter for production observability platforms
        // Configure OTEL_EXPORTER_OTLP_ENDPOINT environment variable to enable
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        // Instrument ASP.NET Core metrics (request count, duration, etc.)
        .AddAspNetCoreInstrumentation()
        // Instrument HttpClient metrics (outgoing request count, duration, etc.)
        .AddHttpClientInstrumentation()
        // Runtime metrics (GC, thread pool, etc.)
        .AddRuntimeInstrumentation()
        // Console exporter for development visibility
        .AddConsoleExporter()
        // OTLP exporter for production observability platforms
        .AddOtlpExporter());

Console.WriteLine($"[Config] OpenTelemetry configured for service: {ServiceName}");

// Validate backend connection configuration at startup
ValidateBffConfiguration(builder.Configuration, builder.Environment.EnvironmentName);

// Register AGUIChatClientFactory for dynamic endpoint selection
// The factory allows creating IChatClient instances for any of the 7 AG-UI endpoints
builder.Services.AddSingleton<IAGUIChatClientFactory, AGUIChatClientFactory>();

// Register ApprovalHandler for Human-in-the-Loop feature
// This service handles approval requests and responses for tool calls requiring user consent
builder.Services.AddScoped<IApprovalHandler, ApprovalHandler>();

// Register JsonPatchApplier for Agentic Generative UI feature
// This service applies JSON Patch operations to Plan models for incremental state updates
builder.Services.AddSingleton<IJsonPatchApplier, JsonPatchApplier>();

// Register ToolComponentRegistry for Tool-Based UI Rendering feature
// This service maps tool names to Blazor component types for dynamic rendering
builder.Services.AddSingleton<IToolComponentRegistry, ToolComponentRegistry>();

// Register StateManager for Shared State feature
// This service manages bidirectional state sync with the server for Recipe data
builder.Services.AddScoped<IStateManager, StateManager>();

// Register MarkdownService for rendering LLM markdown responses as HTML
builder.Services.AddSingleton<IMarkdownService, MarkdownService>();

// Register a default AGUIChatClient for backward compatibility
// Components can also use IAGUIChatClientFactory to create clients for specific endpoints
builder.Services.AddChatClient(sp => new AGUIChatClient(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("aguiserver"), "agentic_chat"));

// -----------------------------------------------------------------------------
// ERROR HANDLING CONFIGURATION (ProblemDetails - RFC 7807)
// -----------------------------------------------------------------------------
// AddProblemDetails() provides consistent, machine-readable error responses
// for BFF-originated errors and YARP proxy errors in RFC 7807 ProblemDetails
// JSON format. This mirrors the AGUIDojoServer error handling pattern.
//
// YARP-specific errors (backend unreachable, timeout, etc.) are detected via
// IForwarderErrorFeature and included in ProblemDetails responses.
//
// Reference: Q1.28-Q1.31 (ProblemDetails, YARP error handling)
// -----------------------------------------------------------------------------
builder.Services.AddProblemDetails(options =>
{
    // Customize ProblemDetails to include additional context
    options.CustomizeProblemDetails = context =>
    {
        // Always include the trace ID for correlation with distributed traces
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        // In Development, include exception details for easier debugging
        // In Production, exception details are hidden to avoid information leakage
        if (context.HttpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            IExceptionHandlerFeature? exceptionFeature = context.HttpContext.Features.Get<IExceptionHandlerFeature>();
            if (exceptionFeature?.Error is not null)
            {
                context.ProblemDetails.Extensions["exception"] = exceptionFeature.Error.Message;
                context.ProblemDetails.Extensions["stackTrace"] = exceptionFeature.Error.StackTrace;
            }

            // Include YARP forwarder error details in Development for debugging proxy issues
            IForwarderErrorFeature? forwarderError = context.HttpContext.Features.Get<IForwarderErrorFeature>();
            if (forwarderError is not null)
            {
                context.ProblemDetails.Extensions["proxyError"] = forwarderError.Error.ToString();
                if (forwarderError.Exception is not null)
                {
                    context.ProblemDetails.Extensions["proxyException"] = forwarderError.Exception.Message;
                }
            }
        }
    };
});

// Register typed HttpClient for consuming business APIs through YARP proxy
// This client uses relative /api/ URLs which YARP proxies to the backend (AGUIDojoServer)
// The base address is empty because requests are made to the same origin (BFF pattern)
// and YARP handles routing /api/* requests to the backend cluster
//
// Resilience: Retry + circuit breaker for idempotent REST GET requests.
// Unlike the SSE client above, REST API calls are safe to retry on transient failures.
builder.Services.AddHttpClient<IWeatherApiClient, WeatherApiClient>()
    .AddResilienceHandler("weather-api-resilience", pipeline =>
    {
        // Retry with exponential backoff for transient HTTP errors (5xx, 408, 429).
        // Safe because WeatherApiClient only performs idempotent GET requests.
        pipeline.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            Delay = TimeSpan.FromSeconds(1),
            UseJitter = true,
        });

        // Circuit breaker: stops sending requests when the backend is failing consistently.
        pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(30),
            MinimumThroughput = 5,
            BreakDuration = TimeSpan.FromSeconds(30),
        });
    });

// Register YARP Reverse Proxy for proxying /api/* requests to backend
// Configuration loaded from appsettings.json ReverseProxy section (see task-2.1, task-2.2)
// This enables BFF pattern: Blazor UI served locally, API requests proxied to AGUIDojoServer
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(transformBuilderContext =>
    {
        // Configure request transform to forward Authorization header to backend (Q1.20)
        // This enables authenticated requests through YARP proxy for protected /api/* endpoints
        transformBuilderContext.AddRequestTransform(transformContext =>
        {
            // Get Authorization header from incoming request
            string authorizationHeader = transformContext.HttpContext.Request.Headers.Authorization.ToString();

            // Forward Authorization header if present; gracefully pass through if missing
            // This supports both authenticated and unauthenticated requests
            if (!string.IsNullOrEmpty(authorizationHeader))
            {
                transformContext.ProxyRequest.Headers.Authorization =
                    AuthenticationHeaderValue.Parse(authorizationHeader);
            }

            return ValueTask.CompletedTask;
        });
    });

// Add health checks for operational monitoring
// Health checks verify:
// - BFF is running (self check)
// - Backend server (AGUIDojoServer) is accessible via the configured server URL
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("AGUIDojoClient (BFF) is running."), tags: ["live"])
    .AddCheck<BackendHealthCheck>("backend", tags: ["ready"]);

// Register BackendHealthCheck as a service for DI
// The health check uses IHttpClientFactory to verify backend availability
builder.Services.AddSingleton(sp =>
    new BackendHealthCheck(
        sp.GetRequiredService<IHttpClientFactory>(),
        sp.GetRequiredService<IConfiguration>()));

WebApplication app = builder.Build();

// -----------------------------------------------------------------------------
// ERROR HANDLING MIDDLEWARE
// -----------------------------------------------------------------------------
// UseExceptionHandler() catches unhandled exceptions and returns ProblemDetails.
// UseStatusCodePages() handles non-exception errors (401, 404, etc.) uniformly.
//
// YARP proxy errors (backend unreachable, timeout) are detected via
// IForwarderErrorFeature and transformed into descriptive ProblemDetails.
//
// Middleware ordering:
// 1. UseExceptionHandler - catches unhandled exceptions before response starts
// 2. UseStatusCodePages  - formats error status codes as ProblemDetails
// 3. UseHsts / UseHttpsRedirection - security headers
// 4. UseAntiforgery      - CSRF protection for Blazor
//
// Reference: Q1.28-Q1.31 (ProblemDetails, YARP error handling)
// -----------------------------------------------------------------------------
app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        // Retrieve the exception that was caught
        IExceptionHandlerFeature? exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
        Exception? exception = exceptionFeature?.Error;

        // Determine the appropriate status code based on exception type
        int statusCode = exception switch
        {
            HttpRequestException => StatusCodes.Status502BadGateway,
            TaskCanceledException => StatusCodes.Status504GatewayTimeout,
            OperationCanceledException => StatusCodes.Status499ClientClosedRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        // Return a ProblemDetails response using the built-in IProblemDetailsService
        // This ensures consistent RFC 7807 formatting across all error responses
        context.Response.StatusCode = statusCode;
        await context.RequestServices
            .GetRequiredService<IProblemDetailsService>()
            .WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails =
                {
                    Status = statusCode,
                    Title = exception switch
                    {
                        HttpRequestException => "Bad Gateway",
                        TaskCanceledException => "Gateway Timeout",
                        OperationCanceledException => "Client Closed Request",
                        _ => "An error occurred while processing your request"
                    }
                }
            });
    });
});

// UseStatusCodePages formats non-exception HTTP error codes as ProblemDetails.
// This handles YARP proxy errors (502, 504) and other status codes uniformly.
// YARP converts downstream failures to status codes which this middleware then
// wraps in RFC 7807 ProblemDetails for consistent client error handling.
app.UseStatusCodePages(async statusCodeContext =>
{
    // Only format non-success status codes that haven't already been handled
    if (!statusCodeContext.HttpContext.Response.HasStarted)
    {
        int statusCode = statusCodeContext.HttpContext.Response.StatusCode;

        // Check for YARP forwarder errors to provide more descriptive titles
        IForwarderErrorFeature? forwarderError = statusCodeContext.HttpContext.Features.Get<IForwarderErrorFeature>();

        string title = forwarderError is not null
            ? forwarderError.Error switch
            {
                ForwarderError.Request => "Proxy Request Error",
                ForwarderError.RequestTimedOut => "Backend Request Timeout",
                ForwarderError.RequestCanceled => "Backend Request Canceled",
                ForwarderError.RequestBodyCanceled => "Backend Request Body Canceled",
                ForwarderError.RequestBodyClient => "Client Request Body Error",
                ForwarderError.RequestBodyDestination => "Backend Request Body Error",
                ForwarderError.ResponseBodyCanceled => "Backend Response Canceled",
                ForwarderError.ResponseBodyClient => "Client Response Body Error",
                ForwarderError.ResponseBodyDestination => "Backend Response Body Error",
                ForwarderError.UpgradeRequestCanceled => "Upgrade Request Canceled",
                ForwarderError.UpgradeResponseDestination => "Upgrade Response Error",
                ForwarderError.NoAvailableDestinations => "No Available Backend Destinations",
                _ => "Proxy Error"
            }
            : statusCode switch
            {
                StatusCodes.Status401Unauthorized => "Unauthorized",
                StatusCodes.Status403Forbidden => "Forbidden",
                StatusCodes.Status404NotFound => "Not Found",
                StatusCodes.Status405MethodNotAllowed => "Method Not Allowed",
                StatusCodes.Status408RequestTimeout => "Request Timeout",
                StatusCodes.Status429TooManyRequests => "Too Many Requests",
                StatusCodes.Status502BadGateway => "Bad Gateway",
                StatusCodes.Status503ServiceUnavailable => "Service Unavailable",
                StatusCodes.Status504GatewayTimeout => "Gateway Timeout",
                _ => "An error occurred"
            };

        await statusCodeContext.HttpContext.RequestServices
            .GetRequiredService<IProblemDetailsService>()
            .WriteAsync(new ProblemDetailsContext
            {
                HttpContext = statusCodeContext.HttpContext,
                ProblemDetails =
                {
                    Status = statusCode,
                    Title = title
                }
            });
    }
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();

// Map health check endpoint for operational monitoring
// Returns aggregate status of BFF health and backend availability
app.MapHealthChecks("/health");

// CRITICAL: Blazor routes MUST be registered BEFORE YARP reverse proxy
// Per research Q1.1-Q1.2, ASP.NET Core endpoint routing matches first registered endpoint
// This ensures /_blazor SignalR hub and Blazor pages are handled locally, not proxied
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Register YARP reverse proxy AFTER Blazor routes
// Order = int.MaxValue ensures YARP has lowest priority and only handles /api/* requests
// that weren't matched by Blazor or static asset endpoints
app.MapReverseProxy().Add(
    builder => ((RouteEndpointBuilder)builder).Order = int.MaxValue);

app.Run();

// Validates BFF configuration at startup. Logs the configured backend URLs
// and warns if production defaults are still in use.
static void ValidateBffConfiguration(IConfiguration configuration, string environment)
{
    string serverUrl = configuration["SERVER_URL"] ?? "http://localhost:5100";
    string? yarpBackendUrl = configuration["ReverseProxy:Clusters:backend:Destinations:primary:Address"];

    Console.WriteLine($"[Config] Environment: {environment}");
    Console.WriteLine($"[Config] SERVER_URL (AG-UI SSE): {serverUrl}");
    Console.WriteLine($"[Config] YARP Backend URL: {yarpBackendUrl ?? "(from appsettings.json)"}");

    // Warn if production environment is using localhost defaults
    if (string.Equals(environment, "Production", StringComparison.OrdinalIgnoreCase))
    {
        if (serverUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[Config] WARNING: SERVER_URL is set to localhost in Production. "
                + "Set the SERVER_URL environment variable to your production backend URL.");
        }

        if (yarpBackendUrl?.Contains("localhost", StringComparison.OrdinalIgnoreCase) != false)
        {
            Console.WriteLine("[Config] WARNING: YARP backend URL points to localhost in Production. "
                + "Set the ReverseProxy__Clusters__backend__Destinations__primary__Address environment variable.");
        }
    }
}

/// <summary>
/// Health check that verifies the backend server (AGUIDojoServer) is accessible.
/// Uses the "aguiserver" HttpClient to check the backend's /health endpoint.
/// </summary>
internal sealed class BackendHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _serverUrl;

    public BackendHealthCheck(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        this._httpClientFactory = httpClientFactory;
        this._serverUrl = configuration["SERVER_URL"] ?? "http://localhost:5100";
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            HttpClient httpClient = this._httpClientFactory.CreateClient("aguiserver");
            HttpResponseMessage response = await httpClient.GetAsync(new Uri("/health", UriKind.Relative), cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy($"Backend server at {this._serverUrl} is healthy.");
            }

            return HealthCheckResult.Unhealthy(
                $"Backend server at {this._serverUrl} returned {response.StatusCode}.");
        }
        catch (HttpRequestException ex)
        {
            return HealthCheckResult.Unhealthy(
                $"Backend server at {this._serverUrl} is unreachable: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            return HealthCheckResult.Unhealthy(
                $"Backend server at {this._serverUrl} health check timed out.");
        }
    }
}
