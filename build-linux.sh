#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
dotnet publish "$SCRIPT_DIR/GondwanaGame.csproj" \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained true \
  --output "$SCRIPT_DIR/artifacts/linux-x64"
GAME="$SCRIPT_DIR/artifacts/linux-x64/GondwanaGame"
if [[ ! -x "$GAME" ]]; then
  echo "Publish completed without an executable at: $GAME" >&2
  exit 1
fi
echo "Linux x86_64 build ready: $GAME"
