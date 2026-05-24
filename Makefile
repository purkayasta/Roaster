DETACH_FLAG = -d
ifdef a
  DETACH_FLAG =
endif

up:
	docker-compose up --build $(DETACH_FLAG)

down:
	docker-compose down

api:
	dotnet watch run --project src/Roaster.Api

consumer:
	dotnet watch run --project src/Roaster.Consumer

editor:
	dotnet watch run --project src/Roaster.Editor
