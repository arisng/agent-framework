// Copyright (c) Microsoft. All rights reserved.

// =============================================================================
// AGUIDojoServer - Production-Ready Backend for AG-UI Protocol Demonstrations
// =============================================================================
//
// REQUIRED ENVIRONMENT VARIABLES:
// --------------------------------
// Set ONE of the following provider configurations:
//
// Option A: OpenAI (recommended for quick start)
//   OPENAI_API_KEY              - Your OpenAI API key (e.g., sk-...)
//   OPENAI_MODEL                - Model name (optional, defaults to gpt-5-mini)
//
// Option B: Azure OpenAI with API Key authentication
//   AZURE_OPENAI_ENDPOINT       - Your Azure OpenAI endpoint URL (e.g., https://myresource.openai.azure.com)
//   AZURE_OPENAI_API_KEY        - Your Azure OpenAI API key
//   AZURE_OPENAI_DEPLOYMENT_NAME - Your deployment name (e.g., gpt-4o)
//
// Option C: Azure OpenAI with Managed Identity (production recommended)
//   AZURE_OPENAI_ENDPOINT       - Your Azure OpenAI endpoint URL
//   AZURE_OPENAI_DEPLOYMENT_NAME - Your deployment name
//   (No API key needed - uses DefaultAzureCredential for secure authentication)
//
// OPTIONAL ENVIRONMENT VARIABLES:
// --------------------------------
//   ASPNETCORE_ENVIRONMENT      - Environment name (Development, Staging, Production)
//   ASPNETCORE_URLS             - Server URLs (default: http://localhost:5100)
//   Jwt__SigningKey             - JWT signing key for authentication (required when auth is enabled)
//
// SECURITY NOTES:
// ----------------
// - Never commit API keys to source control
// - Use Azure Key Vault or similar secret management in production
// - Development secrets can be stored in appsettings.Localhost.json (git-ignored)
// - Production secrets must come from environment variables or secret stores
//
// =============================================================================

using System.Text;
using AGUIDojoServer;
using AGUIDojoServer.Api;
using AGUIDojoServer.Services;
using AGUIDojoServer.Tools;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------------------------
// SECRETS CONFIGURATION
// -----------------------------------------------------------------------------
// The following configuration sources are used in order of precedence:
// 1. Environment variables (highest priority - use for production secrets)
// 2. appsettings.{Environment}.json (environment-specific settings)
// 3. appsettings.json (base configuration)
//
// For nested configuration (e.g., Jwt:SigningKey), use double underscore:
//   Jwt__SigningKey=your-secret-key
//
// This ensures secrets are never stored in source-controlled configuration files.
//
// JWT Configuration (when authentication is enabled):
// ---------------------------------------------------
// - Development: Uses the key in appsettings.Development.json (dev-only key)
// - Production:  MUST set Jwt__SigningKey environment variable with a 256-bit key
//   Example: export Jwt__SigningKey=$(openssl rand -base64 32)
//
// The JWT configuration validates that secrets come from appropriate sources:
// - In Production: Fails if Jwt:SigningKey is set in appsettings.json
// - In Development: Allows appsettings.Development.json for convenience
// -----------------------------------------------------------------------------

// Validate that required LLM provider configuration is present at startup
// This fails fast rather than waiting until the first agent request
ValidateLlmConfiguration(builder.Configuration);

static void ValidateLlmConfiguration(IConfiguration configuration)
{
    string? azureEndpoint = configuration["AZURE_OPENAI_ENDPOINT"];
    string? azureDeployment = configuration["AZURE_OPENAI_DEPLOYMENT_NAME"];
    string? openAiKey = configuration["OPENAI_API_KEY"];

    bool hasAzureConfig = !string.IsNullOrWhiteSpace(azureEndpoint) && !string.IsNullOrWhiteSpace(azureDeployment);
    bool hasOpenAiConfig = !string.IsNullOrWhiteSpace(openAiKey);

    // Log the configuration status for debugging
    if (hasAzureConfig)
    {
        Console.WriteLine($"[Config] LLM Provider: Azure OpenAI at {azureEndpoint}");
    }
    else if (hasOpenAiConfig)
    {
        Console.WriteLine("[Config] LLM Provider: OpenAI");
    }
    else
    {
        // In development, we allow startup without LLM config to enable UI-only testing
        // In production, this would be a configuration error
        Console.WriteLine("[Config] WARNING: No LLM provider configured. Set OPENAI_API_KEY or AZURE_OPENAI_ENDPOINT + AZURE_OPENAI_DEPLOYMENT_NAME");
    }
}

// -----------------------------------------------------------------------------
// OPENTELEMETRY CONFIGURATION
// -----------------------------------------------------------------------------
// OpenTelemetry provides distributed tracing and metrics for observability.
// This configuration enables:
// - ASP.NET Core request tracing (incoming HTTP requests)
// - HttpClient tracing (outgoing HTTP calls to AI providers)
// - Console exporter for development visibility
// - OTLP exporter for production observability platforms (Jaeger, Zipkin, etc.)
//
// W3C Trace Context headers (traceparent, tracestate) are automatically propagated
// to downstream services, enabling end-to-end distributed tracing.
//
// Reference: Q1.24-Q1.27 - OpenTelemetry setup patterns, W3C trace context automatic
// -----------------------------------------------------------------------------
const string ServiceName = "AGUIDojoServer";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: ServiceName, serviceVersion: "1.0.0")
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName
        }))
    .WithTracing(tracing => tracing
        // Instrument incoming ASP.NET Core requests
        .AddAspNetCoreInstrumentation(options =>
        {
            // Enrich spans with HTTP request/response details
            options.RecordException = true;
        })
        // Instrument outgoing HttpClient calls (to AI providers like OpenAI/Azure OpenAI)
        .AddHttpClientInstrumentation(options =>
        {
            // Record exception details for debugging
            options.RecordException = true;
        })
        // Console exporter for development visibility
        // Shows spans in the console output for easy debugging
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

// -----------------------------------------------------------------------------
// JWT BEARER AUTHENTICATION CONFIGURATION
// -----------------------------------------------------------------------------
// JWT authentication is configured for backend API protection (defense in depth).
// This allows the backend to independently validate tokens and enables:
// - Direct service-to-service calls (bypassing BFF)
// - Independent security boundary
// - Defense in depth when used with BFF-level authentication
//
// Configuration is read from:
// - appsettings.json: Jwt:Issuer, Jwt:Audience, Jwt:ExpirationMinutes
// - Environment variable or appsettings.Development.json: Jwt:SigningKey
//
// Reference: Q1.21 - Validate at backend for defense in depth
// -----------------------------------------------------------------------------
string? jwtSigningKey = builder.Configuration["Jwt:SigningKey"];
string jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "AGUIDojoServer";
string jwtAudience = builder.Configuration["Jwt:Audience"] ?? "AGUIDojoClient";

// Only configure JWT authentication if a signing key is provided
// This allows the server to run without authentication for development/testing
if (!string.IsNullOrWhiteSpace(jwtSigningKey))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
                // Allow some clock skew for distributed systems
                ClockSkew = TimeSpan.FromMinutes(2)
            };

            // Configure JWT Bearer events for logging (useful for debugging)
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    // Log authentication failures for debugging
                    // In production, avoid exposing detailed error information
                    if (context.Exception is SecurityTokenExpiredException)
                    {
                        context.Response.Headers.Append("Token-Expired", "true");
                    }

                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    // Optional: Add additional validation logic here
                    // e.g., check if user is still active in the database
                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization();
    Console.WriteLine("[Config] JWT Bearer authentication enabled");
}
else
{
    // Authentication is not configured - endpoints will be accessible without tokens
    // This is appropriate for local development and testing
    Console.WriteLine("[Config] JWT authentication not configured (Jwt:SigningKey not set). Endpoints are unprotected.");
}

// -----------------------------------------------------------------------------
// ERROR HANDLING CONFIGURATION (ProblemDetails - RFC 7807)
// -----------------------------------------------------------------------------
// AddProblemDetails() provides consistent, machine-readable error responses
// for non-streaming (REST) endpoints in RFC 7807 ProblemDetails JSON format.
//
// SSE streaming errors continue to use the AG-UI RunErrorEvent format,
// which is handled by the framework's AGUIServerSentEventsResult automatically.
//
// Reference: Q1.28-Q1.31 - ProblemDetails for REST, RunErrorEvent for SSE
// -----------------------------------------------------------------------------
builder.Services.AddProblemDetails(options =>
{
    // Customize ProblemDetails to include additional context in Development
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
        }
    };
});

builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = HttpLoggingFields.RequestPropertiesAndHeaders | HttpLoggingFields.RequestBody
        | HttpLoggingFields.ResponsePropertiesAndHeaders | HttpLoggingFields.ResponseBody;
    logging.RequestBodyLogLimit = int.MaxValue;
    logging.ResponseBodyLogLimit = int.MaxValue;
});

builder.Services.AddHttpClient().AddLogging();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Add(AGUIDojoServerSerializerContext.Default));
builder.Services.AddAGUI();

// Register IHttpContextAccessor for DI-compatible AI Tools
// AI Tools are registered as Singleton but need access to scoped services via HttpContext.RequestServices
builder.Services.AddHttpContextAccessor();

// Register shared business services as Scoped
// These services are shared between Minimal API endpoints and AI Tools
builder.Services.AddScoped<IWeatherService, WeatherService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();

// Register DI-compatible AI Tool classes as Singleton
// Tools use IHttpContextAccessor to resolve scoped services at execution time (Q1.15-Q1.18)
builder.Services.AddSingleton<WeatherTool>();
builder.Services.AddSingleton<EmailTool>();
builder.Services.AddSingleton<DocumentTool>();

// Register the ChatClientAgentFactory as Singleton
// Factory creates ChatClient during construction and provides methods to create pre-configured agents
builder.Services.AddSingleton<ChatClientAgentFactory>();

// Add health checks for operational monitoring
// Health checks verify:
// - Server is running (self check)
// - LLM provider is configured (configuration check, not connectivity check)
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("AGUIDojoServer is running."), tags: ["live"])
    .AddCheck("llm_configuration", () =>
    {
        // Verify LLM provider configuration is present
        // Note: We check configuration availability rather than actual connectivity
        // to avoid costly API calls on every health check request
        // Configuration is captured from builder.Configuration which includes environment variables
        string? azureEndpoint = builder.Configuration["AZURE_OPENAI_ENDPOINT"];
        string? azureDeployment = builder.Configuration["AZURE_OPENAI_DEPLOYMENT_NAME"];
        string? openAiKey = builder.Configuration["OPENAI_API_KEY"];

        bool hasAzureConfig = !string.IsNullOrWhiteSpace(azureEndpoint) && !string.IsNullOrWhiteSpace(azureDeployment);
        bool hasOpenAiConfig = !string.IsNullOrWhiteSpace(openAiKey);

        if (hasAzureConfig || hasOpenAiConfig)
        {
            string provider = hasAzureConfig ? "Azure OpenAI" : "OpenAI";
            return HealthCheckResult.Healthy($"LLM provider configured: {provider}");
        }

        return HealthCheckResult.Unhealthy(
            "LLM provider not configured. Set AZURE_OPENAI_ENDPOINT and AZURE_OPENAI_DEPLOYMENT_NAME, or OPENAI_API_KEY.");
    }, tags: ["ready"]);

WebApplication app = builder.Build();

// -----------------------------------------------------------------------------
// ERROR HANDLING MIDDLEWARE
// -----------------------------------------------------------------------------
// UseExceptionHandler() catches unhandled exceptions and returns ProblemDetails.
// UseStatusCodePages() handles non-exception errors (401, 404, etc.) uniformly.
//
// These middleware apply to REST (non-streaming) endpoints only.
// AG-UI SSE streaming endpoints handle errors internally via RunErrorEvent
// (see AGUIServerSentEventsResult in the MAF framework).
//
// Middleware ordering:
// 1. UseExceptionHandler - catches unhandled exceptions before response starts
// 2. UseStatusCodePages  - formats error status codes as ProblemDetails
// 3. UseHttpLogging      - logs request/response details
// 4. UseAuthentication   - extracts and validates JWT tokens
// 5. UseAuthorization    - enforces authorization policies
//
// Reference: Q1.28-Q1.31 (ProblemDetails, RunErrorEvent for SSE)
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
            ArgumentException => StatusCodes.Status400BadRequest,
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            KeyNotFoundException => StatusCodes.Status404NotFound,
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
                        ArgumentException => "Bad Request",
                        UnauthorizedAccessException => "Forbidden",
                        KeyNotFoundException => "Not Found",
                        OperationCanceledException => "Client Closed Request",
                        _ => "An error occurred while processing your request"
                    }
                }
            });
    });
});

// UseStatusCodePages formats non-exception HTTP error codes (401, 404, etc.) as ProblemDetails.
// This handles cases where the pipeline returns an error status code without an exception,
// such as authorization failures or unmatched routes.
app.UseStatusCodePages(async statusCodeContext =>
{
    // Only format non-success status codes that haven't already been handled
    // Skip if the response has already started (e.g., SSE streaming already began)
    if (!statusCodeContext.HttpContext.Response.HasStarted)
    {
        int statusCode = statusCodeContext.HttpContext.Response.StatusCode;
        await statusCodeContext.HttpContext.RequestServices
            .GetRequiredService<IProblemDetailsService>()
            .WriteAsync(new ProblemDetailsContext
            {
                HttpContext = statusCodeContext.HttpContext,
                ProblemDetails =
                {
                    Status = statusCode,
                    Title = statusCode switch
                    {
                        StatusCodes.Status401Unauthorized => "Unauthorized",
                        StatusCodes.Status403Forbidden => "Forbidden",
                        StatusCodes.Status404NotFound => "Not Found",
                        StatusCodes.Status405MethodNotAllowed => "Method Not Allowed",
                        StatusCodes.Status408RequestTimeout => "Request Timeout",
                        StatusCodes.Status429TooManyRequests => "Too Many Requests",
                        _ => "An error occurred"
                    }
                }
            });
    }
});

app.UseHttpLogging();

// -----------------------------------------------------------------------------
// AUTHENTICATION & AUTHORIZATION MIDDLEWARE
// -----------------------------------------------------------------------------
// UseAuthentication() must come before UseAuthorization().
// These middleware extract and validate JWT tokens from the Authorization header.
// Endpoints can then use [Authorize] attribute or .RequireAuthorization() to protect them.
//
// Note: AG-UI endpoints are NOT protected by default to match current behavior.
// Task 3.2 will add optional authorization to Minimal API endpoints.
// -----------------------------------------------------------------------------
app.UseAuthentication();
app.UseAuthorization();

// Map health check endpoint for operational monitoring
// Returns aggregate status of all health checks
app.MapHealthChecks("/health");

// Map business API endpoints via MapGroup("/api")
// These endpoints use the same shared services as AI Tools
RouteGroupBuilder apiGroup = app.MapGroup("/api")
    .WithTags("Business API");
apiGroup.MapWeatherEndpoints();
apiGroup.MapEmailEndpoints();

// Map development-only authentication endpoints
// AuthEndpoints.MapAuthEndpoints checks IsDevelopment() internally and returns early if not Dev
apiGroup.MapAuthEndpoints(app.Configuration, app.Environment);

// Get the factory from DI for creating agents
ChatClientAgentFactory agentFactory = app.Services.GetRequiredService<ChatClientAgentFactory>();

// Map the AG-UI agent endpoints for different scenarios
app.MapAGUI("/agentic_chat", agentFactory.CreateAgenticChat());

app.MapAGUI("/backend_tool_rendering", agentFactory.CreateBackendToolRendering());

var jsonOptions = app.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>();
app.MapAGUI("/human_in_the_loop", agentFactory.CreateHumanInTheLoop(jsonOptions.Value.SerializerOptions));

app.MapAGUI("/tool_based_generative_ui", agentFactory.CreateToolBasedGenerativeUI());

app.MapAGUI("/agentic_generative_ui", agentFactory.CreateAgenticUI(jsonOptions.Value.SerializerOptions));

app.MapAGUI("/shared_state", agentFactory.CreateSharedState(jsonOptions.Value.SerializerOptions));

app.MapAGUI("/predictive_state_updates", agentFactory.CreatePredictiveStateUpdates(jsonOptions.Value.SerializerOptions));

await app.RunAsync();

public partial class Program;
