#!/usr/bin/env pwsh
$ErrorActionPreference = 'Stop'

$ComposeProject = "roaster"

Write-Host "🛑 Stopping containers and removing images & networks..."
docker compose -p $ComposeProject down --remove-orphans --rmi local 2>$null
Write-Host "✅ Containers stopped and images removed."

Write-Host "🗑️  Removing volumes..."
docker volume rm "${ComposeProject}_postgres_data" 2>$null
if ($LASTEXITCODE -eq 0) { Write-Host "✅ Volume removed." } else { Write-Host "⚠️  Volume not found, skipping." }

Write-Host "📊 Current Docker disk usage:"
docker system df

Write-Host "🧹 Pruning ALL build cache..."
docker builder prune -a -f
Write-Host "✅ Build cache cleared."

Write-Host "🎉 Done! Run 'docker compose up --build' to start fresh."
