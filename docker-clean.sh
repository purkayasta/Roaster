#!/usr/bin/env bash
set -euo pipefail

COMPOSE_PROJECT="roaster"

echo "🛑 Stopping containers and removing images & networks..."
docker compose -p "$COMPOSE_PROJECT" down --remove-orphans --rmi local 2>/dev/null || true
echo "✅ Containers stopped and images removed."

echo "🗑️  Removing volumes..."
docker volume rm "${COMPOSE_PROJECT}_postgres_data" 2>/dev/null && echo "✅ Volume removed." || echo "⚠️  Volume not found, skipping."

echo "📊 Current Docker disk usage:"
docker system df

echo "🧹 Pruning ALL build cache..."
docker builder prune -a -f
echo "✅ Build cache cleared."

echo "🎉 Done! Run 'docker compose up --build' to start fresh."
