using System.Diagnostics;

namespace UserManagementAPI.Middleware;

/// <summary>
/// Logs the method, path, authenticated caller, response status code, and
/// duration of every request that reaches it. Useful for HR/IT support
/// staff diagnosing "the API isn't working" reports without needing a
/// debugger attached, and satisfies TechHive's "log all incoming requests
/// and outgoing responses for auditing purposes" policy for every request
/// that gets as far as this middleware.
///
/// Registered last in the pipeline (see Program.cs), immediately before
/// MapControllers - which means a request rejected by
/// TokenAuthenticationMiddleware never reaches this middleware and won't
/// appear in this log. That's intentional (see the phase-3 notes for the
/// full rationale): it keeps this middleware focused on successful,
/// authenticated traffic, while TokenAuthenticationMiddleware logs its own
/// allow/deny audit line for every request regardless of outcome.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            // finally (not just after the await) so a request that throws
            // still gets logged with whatever status code was set before
            // ExceptionHandlingMiddleware (registered outside this one)
            // rewrites the response.
            stopwatch.Stop();

            var caller = context.User.Identity?.IsAuthenticated == true
                ? context.User.Identity!.Name
                : "anonymous";

            _logger.LogInformation(
                "{Method} {Path} by {Caller} responded {StatusCode} in {ElapsedMilliseconds}ms",
                context.Request.Method,
                context.Request.Path,
                caller,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
    }
}
