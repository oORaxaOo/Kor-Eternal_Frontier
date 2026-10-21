#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

# The local build script and GitHub Actions currently use different output paths.
# Accept both layouts so the launcher works with either package.
CANDIDATES=(
  "$SCRIPT_DIR/artifacts/linux-x64/GondwanaGame"
  "$SCRIPT_DIR/artifacts/Kor-Eternal-Frontier/linux-x64/GondwanaGame"
)

GAME=""
for candidate in "${CANDIDATES[@]}"; do
  if [[ -f "$candidate" && -x "$candidate" ]]; then
    GAME="$candidate"
    break
  fi
done

if [[ -z "$GAME" ]]; then
  printf '%s\n' \
    "KOR-ETERNAL FRONTIER could not find its Linux executable." \
    "Checked:" \
    "  - $SCRIPT_DIR/artifacts/linux-x64/GondwanaGame" \
    "  - $SCRIPT_DIR/artifacts/Kor-Eternal-Frontier/linux-x64/GondwanaGame" \
    "" \
    "If you downloaded the source code, install the .NET 8 SDK and run:" \
    "  $SCRIPT_DIR/build-linux.sh" >&2
  exit 2
fi

# Prefer bundled native libraries shipped by the CI artifact.
GAME_DIR="$(dirname -- "$GAME")"
if [[ -f "$GAME_DIR/libSDL3.so" ]]; then
  export LD_LIBRARY_PATH="$GAME_DIR${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
fi

exec "$GAME" "$@"
