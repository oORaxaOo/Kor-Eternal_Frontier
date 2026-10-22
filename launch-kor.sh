#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

if [[ "$(uname -s)" != "Linux" || "$(uname -m)" != "x86_64" ]]; then
  echo "Kor Eternal Frontier's bundled launcher supports Linux x86_64." >&2
  exit 1
fi

if [[ ! -x "$ROOT/GondwanaGame" ]]; then
  echo "GondwanaGame is missing or not executable in: $ROOT" >&2
  echo "Extract the complete Linux x86_64 ZIP before starting the game." >&2
  exit 1
fi

# The SDL3 shared library ships beside the game executable in this bundle.
export LD_LIBRARY_PATH="$ROOT${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
cd "$ROOT"
exec "$ROOT/GondwanaGame" "$@"
