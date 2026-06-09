infra:
	docker compose -f docker-compose.infra.yml up --build -d

infra-down:
	docker compose -f docker-compose.infra.yml down

api:
	dotnet watch run --project src/Roaster.Api

consumer:
	dotnet watch run --project src/Roaster.Consumer

editor:
	dotnet watch run --project src/Roaster.Editor
