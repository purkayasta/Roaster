#!/usr/bin/env bash
set -euo pipefail

COMPOSE_PROJECT="roaster"

echo "Stopping containers, removing images and networks..."
docker compose -p "$COMPOSE_PROJECT" down --remove-orphans --rmi local 2>/dev/null || true

echo "Removing volumes..."
docker volume rm "${COMPOSE_PROJECT}_postgres_data" 2>/dev/null || echo "Volume not found, skipping."

echo "Pruning build cache..."
docker builder prune -f

echo "Done. Run 'docker compose up --build' to start fresh."
