using UserManagementAPI.Models;

namespace UserManagementAPI.Data;

/// <summary>
/// Abstraction over user storage so the controller depends on a
/// contract rather than a concrete in-memory implementation. Makes it
/// straightforward to swap in EF Core / a real database later without
/// touching the controller or DTOs.
/// </summary>
public interface IUserRepository
{
    /// <summary>Returns every user. Prefer <see cref="GetPage"/> for the API surface;
    /// this is kept for callers (tests, future export tools) that genuinely need the whole set.</summary>
    IReadOnlyList<User> GetAll();

    /// <summary>Returns a single page of users ordered by Id, without loading/copying more than necessary.</summary>
    PagedResult<User> GetPage(int page, int pageSize);

    User? GetById(int id);
    User Add(User user);
    bool Update(int id, User updatedUser);
    bool Delete(int id);
    bool Exists(int id);
    bool EmailInUseByAnotherUser(string email, int excludingId);
}
