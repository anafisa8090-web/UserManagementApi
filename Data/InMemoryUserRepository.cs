using UserManagementAPI.Models;

namespace UserManagementAPI.Data;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IUserRepository"/>.
/// Registered as a singleton in Program.cs, so all requests share the
/// same backing dictionary. A lock guards every read/write because
/// ASP.NET Core can process requests concurrently on different threads.
///
/// This is intentionally simple for the scaffold phase of the project;
/// swapping in a real database later just means writing a new class
/// that implements IUserRepository and re-pointing the DI registration.
///
/// Bug-fix pass (phase 2): added an email -> id index so uniqueness
/// checks are O(1) instead of an O(n) scan on every write, added
/// GetPage() so GET /api/users doesn't have to return the whole table,
/// and fixed GetAll() actually cloning its results (it previously handed
/// back live references to the objects inside _users, despite a comment
/// claiming otherwise).
/// </summary>
public class InMemoryUserRepository : IUserRepository
{
    private readonly Dictionary<int, User> _users = new();

    // Normalized (trimmed) email -> user id. Case-insensitivity is handled
    // by the dictionary's own StringComparer.OrdinalIgnoreCase, so Normalize()
    // only needs to trim. Kept in sync with _users on every Add/Update/Delete
    // so uniqueness checks don't have to walk the whole table.
    private readonly Dictionary<string, int> _emailIndex = new(StringComparer.OrdinalIgnoreCase);

    private readonly object _lock = new();
    private int _nextId = 1;

    public InMemoryUserRepository()
    {
        // Seed a couple of sample records so GET requests return
        // something meaningful immediately after startup.
        Add(new User
        {
            FirstName = "Grace",
            LastName = "Hopper",
            Email = "grace.hopper@techhive.example",
            Department = "Engineering"
        });

        Add(new User
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada.lovelace@techhive.example",
            Department = "IT"
        });
    }

    public IReadOnlyList<User> GetAll()
    {
        lock (_lock)
        {
            // Bug fix: this used to return _users.Values directly (after an
            // OrderBy/ToList projection, which still just copies the
            // references, not the objects). Callers - including a future
            // endpoint that might tweak a field on a "read-only" result -
            // could mutate repository-owned state without ever taking the
            // lock. Clone every entry so the caller only ever gets a copy.
            return _users.Values
                .OrderBy(u => u.Id)
                .Select(Clone)
                .ToList();
        }
    }

    public PagedResult<User> GetPage(int page, int pageSize)
    {
        const int defaultPageSize = 50;
        const int maxPageSize = 200;

        if (page < 1) page = 1;
        if (pageSize <= 0) pageSize = defaultPageSize;
        if (pageSize > maxPageSize) pageSize = maxPageSize;

        lock (_lock)
        {
            var totalCount = _users.Count;

            // Compute the skip count in long arithmetic: with page clamped
            // only on the low end, a very large page number times pageSize
            // could otherwise overflow a 32-bit int and wrap negative,
            // which LINQ's Skip(int) would silently treat as Skip(0) -
            // quietly handing back page 1's data for a bogus page number
            // instead of an empty page. Guard it explicitly instead.
            var skip = (long)(page - 1) * pageSize;

            var items = skip >= totalCount
                ? new List<User>()
                : _users.Values
                    .OrderBy(u => u.Id)
                    .Skip((int)skip)
                    .Take(pageSize)
                    .Select(Clone)
                    .ToList();

            return new PagedResult<User>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }
    }

    public User? GetById(int id)
    {
        lock (_lock)
        {
            return _users.TryGetValue(id, out var user) ? Clone(user) : null;
        }
    }

    public User Add(User user)
    {
        lock (_lock)
        {
            user.Id = _nextId++;
            user.CreatedAtUtc = DateTime.UtcNow;
            user.UpdatedAtUtc = null;
            _users[user.Id] = user;
            _emailIndex[Normalize(user.Email)] = user.Id;
            return Clone(user);
        }
    }

    public bool Update(int id, User updatedUser)
    {
        lock (_lock)
        {
            if (!_users.TryGetValue(id, out var existing))
            {
                return false;
            }

            // Email may have changed - keep the index in sync so it never
            // points at a stale address for this id.
            var oldEmailKey = Normalize(existing.Email);
            var newEmailKey = Normalize(updatedUser.Email);
            if (!string.Equals(oldEmailKey, newEmailKey, StringComparison.OrdinalIgnoreCase))
            {
                _emailIndex.Remove(oldEmailKey);
                _emailIndex[newEmailKey] = id;
            }

            existing.FirstName = updatedUser.FirstName;
            existing.LastName = updatedUser.LastName;
            existing.Email = updatedUser.Email;
            existing.Department = updatedUser.Department;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            return true;
        }
    }

    public bool Delete(int id)
    {
        lock (_lock)
        {
            if (!_users.TryGetValue(id, out var user))
            {
                return false;
            }

            _emailIndex.Remove(Normalize(user.Email));
            return _users.Remove(id);
        }
    }

    public bool Exists(int id)
    {
        lock (_lock)
        {
            return _users.ContainsKey(id);
        }
    }

    public bool EmailInUseByAnotherUser(string email, int excludingId)
    {
        lock (_lock)
        {
            // O(1) index lookup instead of the previous _users.Values.Any(...)
            // linear scan, which re-walked the entire table on every single
            // POST/PUT once TechHive's user count grew past a handful of rows.
            return _emailIndex.TryGetValue(Normalize(email), out var ownerId) && ownerId != excludingId;
        }
    }

    private static string Normalize(string email) => email.Trim();

    private static User Clone(User user) => new()
    {
        Id = user.Id,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Email = user.Email,
        Department = user.Department,
        CreatedAtUtc = user.CreatedAtUtc,
        UpdatedAtUtc = user.UpdatedAtUtc
    };
}
