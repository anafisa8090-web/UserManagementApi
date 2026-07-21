using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Options;
using UserManagementAPI.Models;
using UserManagementAPI.Options;

namespace UserManagementAPI.Middleware;

/// <summary>
/// Requires a valid bearer token on every request that reaches this
/// middleware, per TechHive's "secure API endpoints using token-based
/// authentication" policy. Registered after the exception-handling
/// middleware but before request logging (see Program.cs for the full
/// pipeline and the reasoning behind that order).
///
/// Expects: <c>Authorization: Bearer &lt;token&gt;</c>. Valid tokens are
/// configured under the "Authentication:Tokens" section (see
/// Options/ApiTokenOptions.cs) - not hardcoded - so they can be rotated
/// per environment without a code change.
/// </summary>
public class TokenAuthenticationMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string BearerPrefix = "Bearer ";

    private readonly RequestDelegate _next;
    private readonly ILogger<TokenAuthenticationMiddleware> _logger;

    // token -> friendly caller name. Built once from configuration at
    // startup (this middleware type is instantiated once per app by
    // UseMiddleware<T>, not per request), so validating a token is an O(1)
    // dictionary lookup rather than scanning the configured token list on
    // every single request - the same index-instead-of-scan pattern used
    // for email-uniqueness checks in the phase-2 debugging pass.
    private readonly IReadOnlyDictionary<string, string> _tokenLookup;

    public TokenAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<TokenAuthenticationMiddleware> logger,
        IOptions<ApiTokenOptions> options)
    {
        _next = next;
        _logger = logger;

        var lookup = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in options.Value.Tokens)
        {
            if (!string.IsNullOrWhiteSpace(entry.Token))
            {
                lookup[entry.Token] = string.IsNullOrWhiteSpace(entry.Name) ? "unnamed-caller" : entry.Name;
            }
        }
        _tokenLookup = lookup;

        if (_tokenLookup.Count == 0)
        {
            _logger.LogWarning(
                "No API tokens configured under \"Authentication:Tokens\" - every request will be rejected with 401 until at least one token is configured.");
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();

        if (string.IsNullOrEmpty(header) || !header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await RejectAsync(context, "Missing or malformed Authorization header. Expected: Bearer <token>.");
            return;
        }

        var token = header[BearerPrefix.Length..].Trim();

        if (token.Length == 0 || !_tokenLookup.TryGetValue(token, out var callerName))
        {
            await RejectAsync(context, "Invalid or unrecognized token.");
            return;
        }

        // Attach an identity so downstream middleware/controllers know who
        // made the request. RequestLoggingMiddleware (registered after this
        // one) reads context.User.Identity.Name to put the caller's name in
        // the audit log alongside method/path/status.
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, callerName) },
            authenticationType: "ApiToken"));

        // This middleware's own audit line for a successful auth check.
        // Deliberately logged here (not left solely to RequestLoggingMiddleware)
        // because this middleware runs before the logging middleware in the
        // pipeline - a rejected (401) request below never reaches logging
        // middleware at all, so without this line here, denied requests
        // would never show up in the audit trail. See Program.cs / phase-3
        // notes for the full ordering rationale.
        _logger.LogInformation("Authenticated request from {Caller}: {Method} {Path}",
            callerName, context.Request.Method, context.Request.Path);

        await _next(context);
    }

    private async Task RejectAsync(HttpContext context, string reason)
    {
        _logger.LogWarning("Rejected unauthenticated request: {Method} {Path} - {Reason}",
            context.Request.Method, context.Request.Path, reason);

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";

        var body = JsonSerializer.Serialize(new ApiErrorResponse
        {
            Error = reason,
            Status = StatusCodes.Status401Unauthorized,
            TraceId = context.TraceIdentifier
        }, JsonOptions);

        await context.Response.WriteAsync(body);
    }
}
