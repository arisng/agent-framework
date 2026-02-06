// Copyright (c) Microsoft. All rights reserved.

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace AGUIDojoServer.Api;

/// <summary>
/// Authentication endpoints for development and testing purposes.
/// </summary>
/// <remarks>
/// These endpoints are intended for use in Development environment only
/// to facilitate manual testing of authenticated API flows.
/// DO NOT enable these endpoints in production.
/// </remarks>
internal static class AuthEndpoints
{
    /// <summary>
    /// Maps development authentication endpoints to the application.
    /// </summary>
    /// <param name="group">The route group builder for the /api prefix.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <returns>The route group builder with auth endpoints mapped.</returns>
    public static RouteGroupBuilder MapAuthEndpoints(
        this RouteGroupBuilder group,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        // Only map these endpoints in Development environment
        if (!environment.IsDevelopment())
        {
            return group;
        }

        group.MapPost("/dev/token", (TokenRequest? request) => GenerateToken(request, configuration))
            .WithName("GenerateDevToken")
            .WithSummary("Generates a JWT token for development testing")
            .WithDescription(
                "Creates a JWT token with configurable claims for testing authenticated endpoints. " +
                "This endpoint is ONLY available in Development environment.")
            .Produces<TokenResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Development");

        return group;
    }

    /// <summary>
    /// Generates a JWT token for development and testing purposes.
    /// </summary>
    /// <param name="request">Optional token request with custom claims.</param>
    /// <param name="configuration">The application configuration containing JWT settings.</param>
    /// <returns>A result containing the generated token or an error.</returns>
    private static IResult GenerateToken(TokenRequest? request, IConfiguration configuration)
    {
        // Read JWT configuration
        string? signingKey = configuration["Jwt:SigningKey"];
        string issuer = configuration["Jwt:Issuer"] ?? "AGUIDojoServer";
        string audience = configuration["Jwt:Audience"] ?? "AGUIDojoClient";
        int expirationMinutes = configuration.GetValue("Jwt:ExpirationMinutes", 60);

        // Validate that JWT signing key is configured
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            return TypedResults.Problem(
                detail: "JWT signing key is not configured. Set Jwt:SigningKey in appsettings.Development.json or via environment variable.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "JWT Configuration Error");
        }

        // Use defaults if no request provided
        request ??= new TokenRequest();

        // Build claims
        List<Claim> claims =
        [
            new Claim(JwtRegisteredClaimNames.Sub, request.Subject ?? "dev-user"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new Claim(ClaimTypes.Name, request.Name ?? "Development User"),
            new Claim(ClaimTypes.Email, request.Email ?? "dev@localhost"),
        ];

        // Add custom roles
        if (request.Roles is { Count: > 0 })
        {
            foreach (string role in request.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }
        else
        {
            // Default role for development
            claims.Add(new Claim(ClaimTypes.Role, "Developer"));
        }

        // Add any custom claims
        if (request.CustomClaims is { Count: > 0 })
        {
            foreach (KeyValuePair<string, string> customClaim in request.CustomClaims)
            {
                claims.Add(new Claim(customClaim.Key, customClaim.Value));
            }
        }

        // Create the token
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(signingKey));
        SigningCredentials credentials = new(key, SecurityAlgorithms.HmacSha256);

        DateTime expires = DateTime.UtcNow.AddMinutes(expirationMinutes);

        JwtSecurityToken token = new(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: credentials);

        string tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return TypedResults.Ok(new TokenResponse
        {
            Token = tokenString,
            ExpiresAt = expires,
            TokenType = "Bearer"
        });
    }
}

/// <summary>
/// Request model for generating development JWT tokens.
/// </summary>
/// <remarks>
/// All properties are optional. Default values will be used if not provided.
/// </remarks>
public sealed record TokenRequest
{
    /// <summary>
    /// Gets or sets the subject (user ID) for the token. Defaults to "dev-user".
    /// </summary>
    public string? Subject { get; init; }

    /// <summary>
    /// Gets or sets the display name for the user. Defaults to "Development User".
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets or sets the email address for the user. Defaults to "dev@localhost".
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Gets or sets the roles to assign to the user. Defaults to ["Developer"].
    /// </summary>
    public List<string>? Roles { get; init; }

    /// <summary>
    /// Gets or sets additional custom claims to include in the token.
    /// </summary>
    public Dictionary<string, string>? CustomClaims { get; init; }
}

/// <summary>
/// Response model containing the generated JWT token.
/// </summary>
public sealed record TokenResponse
{
    /// <summary>
    /// Gets or sets the generated JWT token string.
    /// </summary>
    public required string Token { get; init; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the token expires.
    /// </summary>
    public required DateTime ExpiresAt { get; init; }

    /// <summary>
    /// Gets or sets the token type. Always "Bearer".
    /// </summary>
    public required string TokenType { get; init; }
}
