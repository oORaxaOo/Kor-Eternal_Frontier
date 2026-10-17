#!/usr/bin/env bash
set -euo pipefail
HERE="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
cd "$HERE"
if [[ ! -x ./KorFrontier.Gondwana ]]; then
  echo "Kor Frontier executable is missing. Download and extract the Linux x64 build artifact first." >&2
  exit 1
fi
exec ./KorFrontier.Gondwana "$@"
