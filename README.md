# TechHive User Management API

Internal API for HR and IT to create, retrieve, update, and delete user records.
ASP.NET Core 8 Web API, controller-based, in-memory data store.

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Running locally

```bash
cd UserManagementAPI
dotnet restore
dotnet run
```

The console output will print the URL(s) it's listening on (by default
`http://localhost:5223`, see `Properties/launchSettings.json`). In the
`Development` environment it launches straight into Swagger UI at
`/swagger`, which lets you try every endpoint from the browser — click
**Authorize** and paste one of the tokens from `appsettings.Development.json`
to try the protected endpoints without any extra tooling.

## Authentication

Every `/api/users*` request must include a bearer token:

```
Authorization: Bearer <token>
```

Valid tokens live under `Authentication:Tokens` in configuration, not in
code (see `Options/ApiTokenOptions.cs`) so they can be rotated per
environment. `appsettings.Development.json` ships two obviously-fake
tokens (`dev-hr-token-please-rotate`, `dev-it-token-please-rotate`) so
`dotnet run` works out of the box locally. **Do not reuse those in any
environment real traffic could reach** — for anything beyond your own
machine, set real tokens via environment variables
(`Authentication__Tokens__0__Name`, `Authentication__Tokens__0__Token`, ...),
`dotnet user-secrets`, or your hosting platform's secret store.
`appsettings.json` (the shared base config) intentionally ships an empty
token list.

A missing or invalid token returns `401 Unauthorized` before the request
ever reaches a controller or the repository. Swagger's own UI at `/swagger`
does **not** require a token, so the API docs stay reachable even if you
don't have one handy.

## Endpoints

| Method | Route             | Description                          | Success | Failure cases                              |
|--------|-------------------|---------------------------------------|---------|---------------------------------------------|
| GET    | `/api/users?page=&pageSize=` | List users, one page at a time | 200 | 401 missing/invalid token |
| GET    | `/api/users/{id}` | Get one user by id                    | 200     | 400 malformed id, 401 missing/invalid token, 404 if id doesn't exist |
| POST   | `/api/users`      | Create a user                         | 201     | 400 invalid body, 401 missing/invalid token, 409 email already in use |
| PUT    | `/api/users/{id}` | Replace an existing user's details    | 204     | 400 invalid body or malformed id, 401 missing/invalid token, 404 not found, 409 duplicate email |
| DELETE | `/api/users/{id}` | Delete a user                         | 204     | 400 malformed id, 401 missing/invalid token, 404 if id doesn't exist |

`GET /api/users` returns a page, not a bare array (fixed in the phase-2
debugging pass — see `DEBUGGING_NOTES.md` — since always returning every
row didn't scale):

```json
{
  "items": [
    {
      "id": 1,
      "firstName": "Grace",
      "lastName": "Hopper",
      "email": "grace.hopper@techhive.example",
      "department": "Engineering",
      "createdAtUtc": "2026-07-21T20:00:00Z",
      "updatedAtUtc": null
    }
  ],
  "totalCount": 2,
  "page": 1,
  "pageSize": 50,
  "totalPages": 1
}
```

`page` defaults to 1, `pageSize` defaults to 50 and is capped at 200.
`GET /api/users/{id}` still returns a single `User` object directly (not
wrapped), matching the shape above minus the paging envelope.

`POST`/`PUT` request bodies only accept `firstName`, `lastName`, `email`,
and `department` — `id`, `createdAtUtc`, and `updatedAtUtc` are always
assigned by the server, never taken from the client. All four fields are
trimmed automatically, so incidental leading/trailing whitespace (e.g.
from a pasted spreadsheet cell) doesn't cause a false validation failure.

A route `{id}` that isn't a positive integer (e.g. `/api/users/abc`)
returns `400 Bad Request` rather than a bare empty 404 — see
`DEBUGGING_NOTES.md` for why that changed.

### Error shape

Every error response — 400, 401, 404, 409, or 500 — uses the same shape
(see `Models/ApiErrorResponse.cs` and `MIDDLEWARE_NOTES.md`):

```json
{
  "error": "No user exists with id 999.",
  "status": 404,
  "traceId": "0HN...",
  "errors": null
}
```

`errors` is only populated for a 400 caused by field validation, where
it's a `{ "fieldName": ["message", ...] }` dictionary.

## Testing

Three equivalent ways to exercise the API, pick whichever is convenient:

1. **Postman** — import `UserManagementAPI.postman_collection.json` (in the
   repo root, one level up from this file) and run the requests against
   `http://localhost:5223`.
2. **VS Code REST Client** — open `UserManagementAPI.http` and click "Send
   Request" above each block.
3. **curl**, for example:

   ```bash
   curl http://localhost:5223/api/users \
     -H "Authorization: Bearer dev-hr-token-please-rotate"

   curl -X POST http://localhost:5223/api/users \
     -H "Authorization: Bearer dev-hr-token-please-rotate" \
     -H "Content-Type: application/json" \
     -d '{"firstName":"Alan","lastName":"Turing","email":"alan.turing@techhive.example","department":"Engineering"}'

   # Omit the header (or use a bad token) to see the 401 path:
   curl -i http://localhost:5223/api/users
   ```

The app seeds two sample users (Grace Hopper, Ada Lovelace) on startup so
`GET /api/users` returns data immediately.

## Project layout

```
UserManagementAPI/
  Program.cs                          # host setup, middleware pipeline (order matters - see MIDDLEWARE_NOTES.md), DI registrations
  Controllers/UsersController.cs      # CRUD endpoints
  Models/User.cs                      # persisted entity
  Models/UserDtos.cs                  # request DTOs (create/update) - no overposting of server-owned fields, trims on set
  Models/PagedResult.cs               # paging envelope returned by GET /api/users
  Models/ApiErrorResponse.cs          # standardized error shape for every 400/401/404/409/500
  Options/ApiTokenOptions.cs          # bearer-token configuration model, bound from "Authentication" config section
  Data/IUserRepository.cs             # storage abstraction
  Data/InMemoryUserRepository.cs      # thread-safe in-memory implementation + email index
  Middleware/ExceptionHandlingMiddleware.cs   # converts unhandled exceptions to a clean JSON 500 (registered first)
  Middleware/TokenAuthenticationMiddleware.cs # bearer-token check, 401s on failure (registered second)
  Middleware/RequestLoggingMiddleware.cs      # logs method/path/caller/status/duration per request (registered last)
```

Swapping the in-memory store for a real database later only requires a new
class implementing `IUserRepository` and updating the DI registration in
`Program.cs` — the controller and DTOs don't need to change.
