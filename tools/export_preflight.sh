#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$repo_root"

dotnet build pokegame.csproj
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj
