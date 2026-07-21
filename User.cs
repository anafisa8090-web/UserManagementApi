using System.ComponentModel.DataAnnotations;

namespace UserManagementAPI.Models;

/// <summary>
/// Represents a user record managed by HR/IT through the API.
/// </summary>
public class User
{
    /// <summary>
    /// Unique identifier for the user. Assigned by the server; ignored on create.
    /// </summary>
    public int Id { get; set; }

    [Required(ErrorMessage = "First name is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "First name must be between 1 and 50 characters.")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Last name must be between 1 and 50 characters.")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email is not a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Department is required.")]
    [StringLength(50, ErrorMessage = "Department must be 50 characters or fewer.")]
    public string Department { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the record was created. Assigned by the server.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// UTC timestamp when the record was last updated. Assigned by the server.
    /// </summary>
    public DateTime? UpdatedAtUtc { get; set; }
}
