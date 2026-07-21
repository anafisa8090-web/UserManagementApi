using System.Net;
using System.Text.Json;
using UserManagementAPI.Models;

namespace UserManagementAPI.Middleware;

/// <summary>
/// Catches any unhandled exception thrown further down the pipeline and
/// converts it into a consistent JSON problem response instead of letting
/// a raw 500 / stack trace leak back to the caller. Registered first in
/// Program.cs so it wraps every other middleware and endpoint - including
/// the authentication and logging middleware added in this pass, so a bug
/// in either of those still comes back as clean JSON rather than crashing
/// the request.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception processing {Method} {Path}",
                context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted)
            {
                // Response body/headers already flushed to the client (e.g.
                // mid-stream) - nothing left we can safely rewrite, so just
                // rethrow and let Kestrel/hosting log it as a fault.
                throw;
            }

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            // Same ApiErrorResponse shape every other error path in the API
            // uses (see Models/ApiErrorResponse.cs) - this used to be its
            // own one-off anonymous object with different casing/fields.
            var problem = new ApiErrorResponse
            {
                Error = "An unexpected error occurred.",
                Status = context.Response.StatusCode,
                TraceId = context.TraceIdentifier
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
        }
    }
}
