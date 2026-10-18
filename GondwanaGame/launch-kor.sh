#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
GAME="$SCRIPT_DIR/artifacts/linux-x64/GondwanaGame"
if [[ ! -x "$GAME" ]]; then
  echo "KOR-ETERNAL FRONTIER is not packaged yet. Run: $SCRIPT_DIR/build-linux.sh" >&2
  exit 2
fi
exec "$GAME" "$@"
