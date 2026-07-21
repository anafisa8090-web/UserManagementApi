using System.ComponentModel.DataAnnotations;

namespace UserManagementAPI.Models;

/// <summary>
/// Payload accepted when creating a new user. Deliberately excludes
/// server-assigned fields (Id, CreatedAtUtc, UpdatedAtUtc) to prevent
/// clients from overposting values they should not control.
///
/// Bug fix (phase 2): properties trim incoming values on assignment.
/// Without this, a name of "   " (all whitespace) satisfied both
/// [Required] and [StringLength(MinimumLength = 1)] - neither attribute
/// considers a non-empty, all-whitespace string invalid - so blank-looking
/// users could be created. Trimming before validation runs means an
/// all-whitespace input collapses to "" and correctly fails MinimumLength.
/// It also means stray leading/trailing spaces in an otherwise valid
/// email no longer produce a false validation failure.
/// </summary>
public class UserCreateDto
{
    private string _firstName = string.Empty;
    private string _lastName = string.Empty;
    private string _email = string.Empty;
    private string _department = string.Empty;

    [Required(ErrorMessage = "First name is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "First name must be between 1 and 50 characters.")]
    public string FirstName
    {
        get => _firstName;
        set => _firstName = value?.Trim() ?? string.Empty;
    }

    [Required(ErrorMessage = "Last name is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Last name must be between 1 and 50 characters.")]
    public string LastName
    {
        get => _lastName;
        set => _lastName = value?.Trim() ?? string.Empty;
    }

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email is not a valid email address.")]
    public string Email
    {
        get => _email;
        set => _email = value?.Trim() ?? string.Empty;
    }

    [Required(ErrorMessage = "Department is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Department must be between 1 and 50 characters.")]
    public string Department
    {
        get => _department;
        set => _department = value?.Trim() ?? string.Empty;
    }
}

/// <summary>
/// Payload accepted when updating an existing user via PUT.
/// All fields are required since PUT represents a full replacement
/// of the editable fields. Same trim-on-set fix as <see cref="UserCreateDto"/>.
/// </summary>
public class UserUpdateDto
{
    private string _firstName = string.Empty;
    private string _lastName = string.Empty;
    private string _email = string.Empty;
    private string _department = string.Empty;

    [Required(ErrorMessage = "First name is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "First name must be between 1 and 50 characters.")]
    public string FirstName
    {
        get => _firstName;
        set => _firstName = value?.Trim() ?? string.Empty;
    }

    [Required(ErrorMessage = "Last name is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Last name must be between 1 and 50 characters.")]
    public string LastName
    {
        get => _lastName;
        set => _lastName = value?.Trim() ?? string.Empty;
    }

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email is not a valid email address.")]
    public string Email
    {
        get => _email;
        set => _email = value?.Trim() ?? string.Empty;
    }

    [Required(ErrorMessage = "Department is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Department must be between 1 and 50 characters.")]
    public string Department
    {
        get => _department;
        set => _department = value?.Trim() ?? string.Empty;
    }
}
