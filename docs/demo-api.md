# Demo data and API checks

The demo dataset supports manual API checks: three accounts with different write permissions, seven tasks covering all statuses, and three shared tags. Credentials come from the `DEMO_*` variables in `.env`. See the root [README](../README.md#demo-data) for loading the data.

## Where to look

| Area | Entry point |
| --- | --- |
| Dataset, validation, and repeat-run behavior | [DemoSeeder.cs](../src/TaskManager.Api/Demo/DemoSeeder.cs) |
| Environment wiring and service dependencies | [docker-compose.yml](../docker-compose.yml) |
| Routes and request handling | [Controllers](../src/TaskManager.Api/Controllers) |
| Report generation and job processing | [JobWorker.cs](../src/TaskManager.Api/Jobs/JobWorker.cs), [FileStorage.cs](../src/TaskManager.Infrastructure/Services/FileStorage.cs) |
| Expected behavior and test examples | [DemoSeedTests.cs](../tests/TaskManager.Api.Tests/DemoSeedTests.cs), [BackendTests.cs](../tests/TaskManager.Api.Tests/BackendTests.cs) |
| Error responses | [Program.cs](../src/TaskManager.Api/Program.cs), [ApiException.cs](../src/TaskManager.Api/Errors/ApiException.cs), [StaffWriteFilter.cs](../src/TaskManager.Api/Auth/StaffWriteFilter.cs), [RequestData.cs](../src/TaskManager.Api/Requests/RequestData.cs) |

Use Swagger or curl to check login, staff/non-staff permissions, CRUD, combined task filters, and nested routes. Enqueue a countdown through the API to verify the running worker and download its generated file; seeding does not enqueue jobs. Request schemas and API behavior are described in Swagger and the root README.

## Error responses

Errors are JSON objects returned with `Content-Type: application/json`. Property names are snake_case. The HTTP status code is the primary signal; message texts are informational and may change. Two body shapes are common, and one case deviates from the first of them.

### Body shapes

| Shape | Body | Used for |
| --- | --- | --- |
| Detail error | `{ "detail": "<message>" }`, optionally with `"code": "<machine_code>"` | 401, 403, 404, 500 |
| Field errors | `{ "<field>": ["<message>"] }` | 400 |
| Non-field errors | `{ "non_field_errors": ["<message>"] }` | 400 |

Codes that exist today are `token_not_valid` and `user_not_found`. `user_not_found` is produced only if the user is deactivated or deleted between token validation and the permission check; in normal use an inactive or deleted user gets the general 401 text.

For field errors, the key is the JSON request field, form field, or query parameter name, and the value is always an array of strings. Errors in nested `author`, `executor`, or `tag` objects appear under the inner field name at the top level.

Exception: unparsable JSON gives `{ "detail": ["JSON parse error."] }`, where `detail` is an array, unlike every other detail error.

### Status codes

| Status | When | Example body |
| --- | --- | --- |
| 400 | Body validation (missing, null, blank, wrong type, invalid choice, length, range, date format `YYYY-MM-DD`, avatar extension and size) and task query filters. A JSON body that is not an object and a unique-constraint violation give `non_field_errors`. Unparsable JSON gives the exception above. A repeated `date_to_finish_from` or `date_to_finish_to` parameter gives a "Date has wrong format" message, and an invalid `author`, `executor`, or `tag` filter gives a "Select a valid choice" message. | `POST /api/tasks/` with `{}`: `{ "title": ["This field is required."] }`. `GET /api/tasks/?condition=invalid`: a `condition` array. |
| 401 | No, invalid, expired, or non-access bearer token, or an inactive or deleted user. The response carries `WWW-Authenticate: Bearer realm="api"`. | `{ "detail": "Authentication credentials were not provided or are invalid." }` |
| 401 | `POST /api/token/` with wrong credentials. | `{ "detail": "No active account found with the given credentials" }` |
| 401 | `POST /api/token/refresh/` with an invalid or expired refresh token. | `{ "detail": "Token is invalid or expired", "code": "token_not_valid" }` |
| 401 | `POST /api/token/refresh/` for an inactive or deleted user. | `{ "detail": "No active account found for the given token." }` |
| 403 | A non-staff user sends POST, PUT, PATCH, or DELETE to `/api/users/`, `/api/current-user/`, `/api/tasks/`, `/api/tags/`, or their nested routes. `POST /api/countdown/` does not require staff. | `{ "detail": "You do not have permission to perform this action." }` |
| 404 | A missing user, task, tag, or job; a task of another executor on `/api/users/{id}/tasks/{taskId}/`; a tag not attached to the task on `/api/tasks/{id}/tags/{tagId}/`; a job id that is not a GUID. | `{ "detail": "Not found." }` |
| 500 | An unhandled error. It is logged and sent to Rollbar when `POST_SERVER_ITEM_ACCESS_TOKEN` is set, as described in the root [README](../README.md); `GET /rollbar-error` produces one on purpose. | `{ "detail": "Internal server error." }` |
| 500 | A PUT or PATCH on an existing task sends an object in `author` or `executor`, or an array in `tag`. Other invalid values in those fields give 400, and `null` clears `author` or `executor` without an error. | `{ "detail": "Writable nested updates are not supported." }` |

### Background job failures

A failed countdown job is not an HTTP error. `POST /api/countdown/` returns 201 with `status` `STARTED`. If the job then fails, for example with `seconds` set to -1, `GET /api/jobs/{id}/` returns 200 with lowercase `status` `failure` and `errors.non_field_errors` as an array holding the failure message. The message comes from the exception, so its exact text is not guaranteed. A 201 with a `Location` header is returned only for `success`. See [Background jobs and email](../README.md#background-jobs-and-email).

## Operational notes

- Seeding preserves existing records and edits. It is not a reset command; changing env credentials does not update stored passwords, and conflicting accounts stop the load.
- Changing database credentials in `.env` does not rotate credentials in an existing PostgreSQL volume.
