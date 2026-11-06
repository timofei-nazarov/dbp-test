.PHONY: up test format check migrate

up:
	docker compose up --build -d api worker

test:
	docker compose --profile test run --build --rm tests

format:
	docker compose --profile test run --rm --no-deps -v $(CURDIR)/backend:/workspace/backend tests dotnet format backend/TaskManager.Api/TaskManager.Api.csproj

check:
	docker compose --profile test run --rm --no-deps tests dotnet build backend/TaskManager.Api/TaskManager.Api.csproj -c Release --no-restore

migrate:
	docker compose run --rm migrate
