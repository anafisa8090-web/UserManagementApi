namespace UserManagementAPI.Options;

/// <summary>
/// Bound from the "Authentication" configuration section. Named
/// ApiTokenOptions (not just "AuthenticationOptions") to avoid colliding
/// with Microsoft.AspNetCore.Authentication.AuthenticationOptions, which
/// is a different, unrelated type from the framework's own auth system.
///
/// This project intentionally uses a lightweight static-token check
/// (see TokenAuthenticationMiddleware) rather than the full
/// AddAuthentication()/AddAuthorization() + [Authorize] pipeline, since
/// TechHive's requirement is "only callers with a valid token get in,"
/// not per-user login/roles. If TechHive later needs real user accounts,
/// swapping this for ASP.NET Core's built-in JWT bearer authentication
/// is the natural next step.
/// </summary>
public class ApiTokenOptions
{
    public const string SectionName = "Authentication";

    /// <summary>Every valid caller token, each with a friendly name used in audit logs.</summary>
    public List<ApiTokenEntry> Tokens { get; set; } = new();
}

public class ApiTokenEntry
{
    /// <summary>Friendly identifier for whichever internal tool/service holds this token, e.g. "hr-tool". Used only for logging, never for authorization decisions.</summary>
    public string Name { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;
}
