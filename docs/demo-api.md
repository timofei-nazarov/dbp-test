# Demo data and API checks

The demo dataset supports manual API checks: three accounts with different write permissions, seven tasks covering all statuses, and three shared tags. Credentials come from the `DEMO_*` variables in `.env`. See the root [README](../README.md#demo-data) for loading the data.

## Where to look

| Area | Entry point |
| --- | --- |
| Dataset, validation, and repeat-run behavior | [DemoSeeder.cs](../backend/TaskManager.Api/Services/DemoSeeder.cs) |
| Environment wiring and service dependencies | [docker-compose.yml](../docker-compose.yml) |
| Routes and request handling | [Controllers](../backend/TaskManager.Api/Controllers) |
| Report generation and job processing | [JobWorker.cs](../backend/TaskManager.Api/Services/JobWorker.cs), [FileStorage.cs](../backend/TaskManager.Api/Services/FileStorage.cs) |
| Expected behavior and test examples | [DemoSeedTests.cs](../backend/TaskManager.Tests/DemoSeedTests.cs), [BackendTests.cs](../backend/TaskManager.Tests/BackendTests.cs) |

Use Swagger or curl to check login, staff/non-staff permissions, CRUD, combined task filters, and nested routes. Enqueue a countdown through the API to verify the running worker and download its generated file; seeding does not enqueue jobs. Request schemas and API behavior are described in Swagger and the root README.

## Operational notes

- Seeding preserves existing records and edits. It is not a reset command; changing env credentials does not update stored passwords, and conflicting accounts stop the load.
- Changing database credentials in `.env` does not rotate credentials in an existing PostgreSQL volume.
