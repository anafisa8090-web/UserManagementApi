namespace UserManagementAPI.Models;

/// <summary>
/// Standardized shape for every error response the API returns.
///
/// Before this pass, 400/404/409 came back as ASP.NET Core's built-in
/// ProblemDetails (title/detail/status), automatic model-validation 400s
/// came back as ValidationProblemDetails (a different shape again with an
/// "errors" dictionary), and the exception-handling middleware's 500s used
/// a third, hand-rolled anonymous object (title/status/traceId). Three
/// different error shapes depending on which code path produced them is
/// exactly the "inconsistent error handling" TechHive's policy is meant to
/// rule out. Every 400/401/404/409/500 in the API now returns this one
/// shape instead.
/// </summary>
public class ApiErrorResponse
{
    /// <summary>Human-readable summary, e.g. "User not found." Matches the
    /// { "error": "..." } shape called out in the assignment.</summary>
    public string Error { get; init; } = string.Empty;

    public int Status { get; init; }

    /// <summary>Correlates this response with the corresponding server log entry.</summary>
    public string? TraceId { get; init; }

    /// <summary>Per-field validation messages. Populated only for 400s coming from model validation; null otherwise.</summary>
    public IDictionary<string, string[]>? Errors { get; init; }
}
