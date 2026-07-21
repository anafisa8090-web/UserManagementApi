using Microsoft.AspNetCore.Mvc;
using UserManagementAPI.Data;
using UserManagementAPI.Models;

namespace UserManagementAPI.Controllers;

/// <summary>
/// CRUD endpoints for managing TechHive user records.
/// Base route: /api/users
///
/// Every request that reaches these actions has already passed through
/// ExceptionHandlingMiddleware and TokenAuthenticationMiddleware (see
/// Program.cs), so callers here don't need their own auth checks - an
/// unauthenticated/invalid-token request never gets this far.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _repository;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserRepository repository, ILogger<UsersController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves users, one page at a time.
    /// </summary>
    /// <param name="page">1-based page number. Defaults to 1; values below 1 are clamped to 1.</param>
    /// <param name="pageSize">Items per page. Values &lt;= 0 fall back to the default of 50; values above 200 are capped at 200.</param>
    /// <response code="200">Returns the requested page of users plus paging metadata.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<User>), StatusCodes.Status200OK)]
    public ActionResult<PagedResult<User>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = _repository.GetPage(page, pageSize);
        _logger.LogInformation(
            "Retrieved page {Page} ({Count} of {Total} user(s)).",
            result.Page, result.Items.Count, result.TotalCount);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a single user by id.
    /// </summary>
    /// <response code="200">Returns the requested user.</response>
    /// <response code="400">The id in the route was not a valid positive integer.</response>
    /// <response code="404">No user exists with the given id.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(User), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public ActionResult<User> GetById(string id)
    {
        if (!TryParseId(id, out var parsedId, out var badRequest))
        {
            return badRequest!;
        }

        var user = _repository.GetById(parsedId);
        if (user is null)
        {
            _logger.LogWarning("GET failed: user {UserId} not found.", parsedId);
            return NotFound(NotFoundError(parsedId));
        }

        return Ok(user);
    }

    /// <summary>
    /// Creates a new user.
    /// </summary>
    /// <response code="201">The user was created.</response>
    /// <response code="400">The request body failed validation.</response>
    /// <response code="409">Another user already has this email address.</response>
    [HttpPost]
    [ProducesResponseType(typeof(User), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public ActionResult<User> Create([FromBody] UserCreateDto dto)
    {
        // No manual ModelState.IsValid check needed here: an invalid body
        // never reaches this line. [ApiController] intercepts it first and
        // returns via the centralized InvalidModelStateResponseFactory
        // configured in Program.cs, which already produces the same
        // ApiErrorResponse shape used everywhere else in the API.
        if (_repository.EmailInUseByAnotherUser(dto.Email, excludingId: 0))
        {
            _logger.LogWarning("POST rejected: email {Email} already in use.", dto.Email);
            return Conflict(EmailConflictError(dto.Email));
        }

        // dto.FirstName/LastName/Email/Department are already trimmed by the
        // DTO's property setters, so no need to re-trim here.
        var user = new User
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            Department = dto.Department
        };

        var created = _repository.Add(user);
        _logger.LogInformation("Created user {UserId} ({Email}).", created.Id, created.Email);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Updates an existing user's details.
    /// </summary>
    /// <response code="204">The user was updated.</response>
    /// <response code="400">The request body failed validation, or the id in the route was not a valid positive integer.</response>
    /// <response code="404">No user exists with the given id.</response>
    /// <response code="409">Another user already has this email address.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public IActionResult Update(string id, [FromBody] UserUpdateDto dto)
    {
        if (!TryParseId(id, out var parsedId, out var badRequest))
        {
            return badRequest!;
        }

        if (_repository.EmailInUseByAnotherUser(dto.Email, excludingId: parsedId))
        {
            _logger.LogWarning("PUT rejected: email {Email} already in use by another user.", dto.Email);
            return Conflict(EmailConflictError(dto.Email));
        }

        var updatedUser = new User
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            Department = dto.Department
        };

        // Checking Update()'s own return value (rather than a separate
        // Exists() pre-check) closes the TOCTOU race fixed in phase 2: the
        // existence check and the write happen as one call into the
        // (locked) repository method instead of two.
        var updated = _repository.Update(parsedId, updatedUser);
        if (!updated)
        {
            _logger.LogWarning("PUT failed: user {UserId} not found.", parsedId);
            return NotFound(NotFoundError(parsedId));
        }

        _logger.LogInformation("Updated user {UserId}.", parsedId);
        return NoContent();
    }

    /// <summary>
    /// Deletes a user by id.
    /// </summary>
    /// <response code="204">The user was deleted.</response>
    /// <response code="400">The id in the route was not a valid positive integer.</response>
    /// <response code="404">No user exists with the given id.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public IActionResult Delete(string id)
    {
        if (!TryParseId(id, out var parsedId, out var badRequest))
        {
            return badRequest!;
        }

        var deleted = _repository.Delete(parsedId);
        if (!deleted)
        {
            _logger.LogWarning("DELETE failed: user {UserId} not found.", parsedId);
            return NotFound(NotFoundError(parsedId));
        }

        _logger.LogInformation("Deleted user {UserId}.", parsedId);
        return NoContent();
    }

    /// <summary>
    /// Parses a route id segment as a positive integer, producing a
    /// consistent 400 ApiErrorResponse for anything else (routes deliberately
    /// don't use the {id:int} constraint - see the phase-2 debugging notes
    /// for why that constraint silently broke the error contract).
    /// </summary>
    private bool TryParseId(string id, out int parsedId, out ActionResult? badRequest)
    {
        if (int.TryParse(id, out parsedId) && parsedId > 0)
        {
            badRequest = null;
            return true;
        }

        _logger.LogWarning("Rejected malformed id {RawId}.", id);
        badRequest = BadRequest(new ApiErrorResponse
        {
            Error = $"'{id}' is not a valid positive integer id.",
            Status = StatusCodes.Status400BadRequest,
            TraceId = HttpContext.TraceIdentifier
        });
        return false;
    }

    private ApiErrorResponse NotFoundError(int id) => new()
    {
        Error = $"No user exists with id {id}.",
        Status = StatusCodes.Status404NotFound,
        TraceId = HttpContext.TraceIdentifier
    };

    private ApiErrorResponse EmailConflictError(string email) => new()
    {
        Error = $"A user with email '{email}' already exists.",
        Status = StatusCodes.Status409Conflict,
        TraceId = HttpContext.TraceIdentifier
    };
}
