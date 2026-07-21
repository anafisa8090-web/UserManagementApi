using Microsoft.AspNetCore.Mvc;
using UserManagementAPI.Data;
using UserManagementAPI.Middleware;
using UserManagementAPI.Models;
using UserManagementAPI.Options;

var builder = WebApplication.CreateBuilder(args);

// ---- Services ----------------------------------------------------------

// Controllers + built-in [ApiController] model validation.
builder.Services.AddControllers();

// Every automatic 400 that [ApiController] generates for invalid ModelState
// (e.g. a POST/PUT body that fails a DataAnnotations check) now returns the
// same ApiErrorResponse shape as every other error path in the API - see
// Models/ApiErrorResponse.cs. Must be configured after AddControllers() so
// this delegate runs after (and overrides) the default one AddControllers()
// wires up internally.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var fieldErrors = context.ModelState
            .Where(kvp => kvp.Value is { Errors.Count: > 0 })
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

        var response = new ApiErrorResponse
        {
            Error = "One or more fields failed validation.",
            Status = StatusCodes.Status400BadRequest,
            TraceId = context.HttpContext.TraceIdentifier,
            Errors = fieldErrors
        };

        return new BadRequestObjectResult(response);
    };
});

// Swagger / OpenAPI so HR and IT can explore and try endpoints from a browser.
// Kept reachable without a token (see the middleware pipeline below) so
// developers can still read the API docs; a bearer token is only required
// to actually call /api/* endpoints.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "TechHive User Management API",
        Version = "v1",
        Description = "Internal API for HR and IT to create, retrieve, update, and delete user records."
    });

    // Lets Swagger UI's "Authorize" button attach a bearer token to try-it-out
    // requests, matching the token-based auth TokenAuthenticationMiddleware enforces.
    var bearerScheme = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "token",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Paste just the token value; Swagger UI adds the \"Bearer \" prefix for you."
    };
    options.AddSecurityDefinition("Bearer", bearerScheme);

    // Bug fix: AddSecurityRequirement needs a scheme that carries a
    // Reference (Type/Id pointing back at the "Bearer" definition above) -
    // reusing the bearerScheme instance itself (which has no Reference set)
    // compiles fine but Microsoft.OpenApi silently drops the requirement
    // during serialization, so swagger.json would end up with no security
    // requirement at all: no padlock icons, and the Authorize button's
    // token would never actually get attached to try-it-out calls.
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// In-memory user store, shared across all requests for the lifetime of the app.
builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();

// Valid API tokens, bound from the "Authentication" config section (see
// appsettings.json / appsettings.Development.json and Options/ApiTokenOptions.cs).
// Read once at startup by TokenAuthenticationMiddleware - not hardcoded, so
// tokens can be rotated per environment without a code change.
builder.Services.Configure<ApiTokenOptions>(builder.Configuration.GetSection(ApiTokenOptions.SectionName));

// Allow the internal tools front end(s) to call this API from a browser.
// Wide open here (AllowAnyOrigin) so it works out of the box against any
// local dev tool during this scaffold phase. Before shipping to TechHive,
// replace AllowAnyOrigin with .WithOrigins("https://tools.techhive.internal", ...)
// scoped to the actual internal tool URL(s).
builder.Services.AddCors(options =>
{
    options.AddPolicy("InternalTools", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// ---- Middleware pipeline -------------------------------------------------
//
// Required relative order per TechHive's policy: error handling first,
// authentication next, logging last. The reasoning, in registration order:
//
// 1. ExceptionHandlingMiddleware - outermost, so it wraps every other
//    middleware and endpoint below it (including auth and logging - a bug
//    in either still comes back as clean JSON instead of crashing).
// 2. Swagger/HTTPS-redirect/CORS - transport- and docs-level concerns that
//    need to run before any app-specific logic. Swagger deliberately stays
//    reachable without a token; CORS preflight (OPTIONS) requests carry no
//    token by design and must not be rejected by the auth middleware.
// 3. TokenAuthenticationMiddleware - runs before logging so an invalid or
//    missing token is rejected immediately, before the app spends any time
//    logging the request or touching the repository. That's the
//    "optimal performance" half of this ordering.
// 4. RequestLoggingMiddleware - last, right before the endpoint. Only ever
//    sees requests that already passed authentication, so it logs the
//    authenticated caller's name alongside method/path/status. The
//    tradeoff: a rejected (401) request never reaches this middleware, so
//    it won't show up in this particular log. TokenAuthenticationMiddleware
//    covers that gap with its own allow/deny audit line for every request,
//    regardless of outcome - see the phase-3 notes for the full discussion.
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "TechHive User Management API v1");
    });
}

app.UseHttpsRedirection();

app.UseCors("InternalTools");

app.UseMiddleware<TokenAuthenticationMiddleware>();

app.UseMiddleware<RequestLoggingMiddleware>();

app.MapControllers();

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program { }
