# Task Manager backend

The backend uses C# on .NET 10, ASP.NET Core, EF Core, and PostgreSQL. The task board (Blazor) is served by the API process and calls the API over HTTP.

## Run

All application, build, test, formatting, and migration commands run in Docker. Docker Compose is the host-side entry point.

Configure `.env` using `.env.example` before running Compose.

```sh
docker compose up --build -d api worker
```

The `migrate` service applies EF Core database migrations before API and worker startup. Database files are persisted in `csharp_postgres_data`. Uploaded files and generated reports use `csharp_media`.

- API: http://localhost:18000/api/tasks/
- Task board: http://localhost:18000/board
- Sign-in page: http://localhost:18000/login
- Swagger: http://localhost:18000/swagger/
- OpenAPI: http://localhost:18000/swagger.json
- ReDoc: http://localhost:18000/redoc/
- Health: http://localhost:18000/health
- PostgreSQL: internal service `db-csharp:5432`; database name and credentials come from `POSTGRES_DB`, `POSTGRES_USER`, and `POSTGRES_PASSWORD`

The database starts without application users. To create an account, export `BOOTSTRAP_PASSWORD` in your shell and run:

```sh
docker compose run --rm -e BOOTSTRAP_USERNAME=admin -e BOOTSTRAP_PASSWORD api --create-user
```

The password must have at least eight characters. The command creates a staff account with the `admin` role by default, rejects duplicate usernames, and does not overwrite existing accounts. Optional environment variables are `BOOTSTRAP_EMAIL`, `BOOTSTRAP_IS_STAFF`, and `BOOTSTRAP_ROLE`. API user creation does not set a password or staff privileges.

Stop services with `docker compose stop`. Do not use database-drop commands or `docker compose down -v` during development.

## Demo data

```sh
docker compose --profile demo run --build --rm seed
```

See [demo data and API checks](docs/demo-api.md) for context and code entry points.

## Authentication and API

`POST /api/token/` accepts `username` and `password` and returns `access` and `refresh`. Access tokens carry an `is_staff` claim and last five minutes; refresh tokens last one day. `POST /api/token/refresh/` accepts `refresh` and returns a new access token. Send `Authorization: Bearer <access>` to protected routes. Swagger supports bearer authentication through its Authorize button.

| Route | Methods | Behavior |
| --- | --- | --- |
| `/api/users/` | GET, POST | List or create users; `name` filters first name case-insensitively |
| `/api/users/{id}/` | GET, PUT, PATCH, DELETE | User details and changes |
| `/api/current-user/` | GET, PUT, PATCH, POST | Current profile; POST creates a user |
| `/api/current-user/{ignoredId}/` | GET, PUT, PATCH, DELETE | Current profile; the lookup value is ignored |
| `/api/tasks/` | GET, POST | List or create tasks |
| `/api/tasks/{id}/` | GET, PUT, PATCH, DELETE | Task details and changes |
| `/api/users/{id}/tasks/` | GET | Tasks assigned to the specified user |
| `/api/users/{id}/tasks/{taskId}/` | GET | An assigned task, or 404 when it belongs to another executor |
| `/api/tags/` | GET, POST | List or create tags |
| `/api/tags/{id}/` | GET, PUT, PATCH, DELETE | Tag details and changes |
| `/api/tasks/{id}/tags/` | GET, POST | List tags or create and attach a new tag |
| `/api/tasks/{id}/tags/{tagId}/` | GET, PUT, PATCH, DELETE | Operate on a tag belonging to the task |
| `/api/countdown/` | POST | Queue a background job with integer `seconds` |
| `/api/jobs/{id}/` | GET | Job status, result, or errors |

Trailing slashes are accepted. JSON properties use snake_case names, including singular `tag`. Lists are unpaginated and sorted by ID. Task filters are `condition`, `author`, `executor`, and repeated `tag` parameters. All specified tags must match.

User responses contain `id`, `username`, `first_name`, `last_name`, `email`, `role`, and `avatar_picture`. Avatars are uploaded using multipart form data and accept JPEG/JPG/PNG extensions up to 1 MiB. Validation checks extensions and size; it does not decode image content. User model fields such as date of birth and phone are stored but are not exposed by the API.

## Permissions and task behavior

All main data routes require authentication. Authenticated users can read all records. Writes depend on `IsStaff`, not `Role`; a non-staff user cannot edit their own profile. Background job creation requires authentication but does not require staff privileges.

Creating a task with nested `author`, `executor`, or `tag` data creates new records. Nested relationship updates are unsupported and return a server error. Scalar task updates are supported. Deleting a user sets task author/executor references to null. Deleting a tag through a task route deletes the tag globally, including links to other tasks.

The seven status strings are `new_task`, `in_development`, `in_qa`, `in_code_review`, `ready_for_release`, `released`, and `archived`. Transitions are unrestricted. Dates are UTC calendar dates. Creation/update dates are managed by the backend and cannot be set through API input.

Task assignment does not automatically send email.

## Background jobs and email

A separate worker processes background jobs. The queue and job results are persisted in PostgreSQL. Workers claim jobs using row locks with `SKIP LOCKED`, renew leases while running, and recover expired leases after a restart. Processing has at-least-once semantics; SMTP delivery may repeat if a worker stops after sending and before recording success.

A countdown creates a report containing `test data`. Creation returns 201 and a `Location` pointing to the job. A job has `started`, `success`, or `failure` status. Successful retrieval returns 201 and a `Location` pointing to the file; failure responses include `errors.non_field_errors`. Unknown IDs return 404.

Queue an assignment notification explicitly:

```sh
docker compose run --rm api --send-assignment 123
```

Generic HTML emails can be queued from a JSON file containing `Subject`, `Html`, and a `Recipients` string array:

```sh
docker compose run --rm -v /absolute/path/email.json:/app/email.json:ro api --send-email /app/email.json
```

`EMAIL_BACKEND_CSHARP=console` is the default. Use `smtp` with `EMAIL_HOST`, `EMAIL_PORT`, `EMAIL_HOST_USER`, `EMAIL_HOST_PASSWORD`, `EMAIL_USE_TLS=true`, and `DEFAULT_FROM_EMAIL` for SMTP delivery. The console backend logs the rendered email instead of delivering it.

## Storage and diagnostics

Local storage is the default. Set `AWS_STORAGE_BUCKET_NAME`, `AWS_REGION_NAME`, `AWS_ACCESS_KEY_ID`, and `AWS_SECRET_ACCESS_KEY` to use S3. Avatars use public-read object ACLs; report files are private and receive signed URLs valid for ten minutes. The bucket must allow the corresponding ACLs and access.

Request logs include method, path, status, authenticated user ID, and elapsed time. `GET /rollbar-error` produces a diagnostic 500 response. Setting `POST_SERVER_ITEM_ACCESS_TOKEN` enables Rollbar error reports; `ROLLBAR_ENVIRONMENT` defaults to `development`. Failed error reporting does not replace the API response.

The only screens are the task board at `/board` (also `/`) and the sign-in page at `/login`; there are no other management screens. The board calls the API at the address in `Ui__ApiBaseUrl` (default `http://localhost:8080`, set in `docker-compose.yml`). New Relic is not enabled in the image.

## Tests and development

```sh
docker compose --profile test run --build --rm tests
```

Tests use a real PostgreSQL service (`db-test`) and the separate persistent `csharp_test_postgres_data` volume. They apply migrations, create uniquely named fixtures, and never drop the database. Reports are written to `TestResults/backend.trx`. Tests cover authentication and token expiry, permissions, CRUD, validation, filters, nested records, deletion semantics, avatars, job processing and lease recovery, SMTP delivery, and API documentation. S3 and real Rollbar accounts are not contacted by the integration tests.

Build and verify formatting in the SDK container:

```sh
docker compose --profile test run --rm --no-deps tests dotnet build TaskManager.slnx -c Release --no-restore
docker compose --profile test run --rm --no-deps tests dotnet format TaskManager.slnx --no-restore --verify-no-changes
```

To format source files, bind the repository's `backend` directory into the SDK container:

```sh
docker compose --profile test run --rm --no-deps -v "$PWD/backend:/workspace/backend" tests dotnet format TaskManager.slnx
```

To generate a migration after changing the model:

```sh
docker compose --profile test run --rm --no-deps -v "$PWD/backend:/workspace/backend" tests sh -c 'dotnet tool restore && dotnet ef migrations add MigrationName --project backend/TaskManager.Api --output-dir Data/Migrations'
```

Remove generated comments/docstrings before committing migrations, following repository rules. Apply migrations with `docker compose run --rm migrate`. Restart API and worker on the rebuilt image after model changes.

Reference documentation: [ASP.NET Core JWT middleware](https://learn.microsoft.com/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0), [Npgsql EF Core provider](https://www.npgsql.org/efcore/release-notes/10.0.html), and [Rollbar item API](https://docs.rollbar.com/reference/create-item).
