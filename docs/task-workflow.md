# Task workflow

## Task lifecycle

A task stores its status in the `condition` field. A new task starts as `new_task`. The backend accepts these seven values:

| Status | Meaning |
| --- | --- |
| `new_task` | Default status of a created task |
| `in_development` | Work has started |
| `in_qa` | Under testing |
| `in_code_review` | Waiting for code review |
| `ready_for_release` | Accepted and waiting for release |
| `released` | Delivered |
| `archived` | No longer active |

Transitions are not restricted: a task can be moved from any status to any other. Any other value is rejected by validation. A usual path is `new_task` -> `in_development` -> `in_qa` -> `in_code_review` -> `ready_for_release` -> `released` -> `archived`.

## Working with tasks through the API

Get an access token with `POST /api/token/` (body: `username`, `password`) and send it as `Authorization: Bearer <access>`. Access tokens last five minutes; renew them with `POST /api/token/refresh/` (body: `refresh`). All task routes require authentication. Reading is open to any authenticated user; creating, updating and deleting require a staff account.

| Action | Request |
| --- | --- |
| List tasks | `GET /api/tasks/` (filters: `condition`, `author`, `executor`, repeated `tag`) |
| Get a task | `GET /api/tasks/{id}/` |
| List tasks of a user | `GET /api/users/{id}/tasks/` |
| Create a task | `POST /api/tasks/` |
| Update a task | `PATCH /api/tasks/{id}/` (partial) or `PUT /api/tasks/{id}/` (full) |
| Delete a task | `DELETE /api/tasks/{id}/` |

Create a task. `title` is required (up to 100 characters). Optional fields are `description`, `date_to_finish`, `condition`, `priority` (0-32767), `author`, `executor` and `tag`.

```sh
curl -X POST http://localhost:18000/api/tasks/ \
  -H "Authorization: Bearer $ACCESS" \
  -H "Content-Type: application/json" \
  -d '{"title": "Write task workflow doc", "priority": 1}'
```

Move a task to another status by updating `condition`:

```sh
curl -X PATCH http://localhost:18000/api/tasks/42/ \
  -H "Authorization: Bearer $ACCESS" \
  -H "Content-Type: application/json" \
  -d '{"condition": "in_development"}'
```

Complete a task the same way, by setting `condition` to `released`. Use `archived` for tasks that should no longer be active. Creation and update dates are set by the backend. Nested `author`, `executor` and `tag` data on creation creates new records; updating nested relationships through a task is not supported.

## Branch naming

Name the branch after the task with a short description in lowercase words joined by hyphens:

- `feature/<very_short_task_description>` for new functionality or documentation, for example `feature/task-workflow-doc`
- `fix/<very_short_task_description>` for bug fixes, for example `fix/task-filter-by-tag`

Open a pull request from the branch into `develop` and mention the card number in the description.
