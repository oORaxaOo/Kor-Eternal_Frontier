#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

fail() {
  printf 'KOR Eternal Frontier: %s\n' "$1" >&2
  exit "${2:-1}"
}

if [[ "$(uname -s)" != "Linux" || "$(uname -m)" != "x86_64" ]]; then
  fail "Dieser Starter unterstützt derzeit Linux x86_64." 2
fi

# Prefer a packaged executable beside this script. When called from the source
# checkout, also recognize the output of ./build-linux.sh.
GAME_DIR="$ROOT"
if [[ ! -x "$GAME_DIR/GondwanaGame" && -x "$ROOT/artifacts/linux-x64/GondwanaGame" ]]; then
  GAME_DIR="$ROOT/artifacts/linux-x64"
fi

if [[ -x "$GAME_DIR/GondwanaGame" ]]; then
  # SDL3 is bundled beside the executable in the CI download. Local builds can
  # also use a system SDL3 installation if no bundled library is present.
  export LD_LIBRARY_PATH="$GAME_DIR${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
  cd "$GAME_DIR"
  exec "$GAME_DIR/GondwanaGame" "$@"
fi

# A source checkout can be started directly without manually typing dotnet
# commands. This path requires the .NET 8 SDK and the native SDL3 dependency.
if [[ -f "$ROOT/GondwanaGame.csproj" ]] && command -v dotnet >/dev/null 2>&1; then
  printf 'Kein fertiges Spielpaket gefunden; starte den Quellcode mit .NET …\n'
  exec dotnet run --project "$ROOT/GondwanaGame.csproj" --configuration Release -- "$@"
fi

cat >&2 <<'EOF'
Das Spiel wurde nicht gefunden.
- Fertiges Paket: ZIP vollständig entpacken und diesen Starter ausführen.
- Quellcode: .NET 8 SDK installieren, SDL3 bereitstellen und ./build-linux.sh ausführen.
EOF
exit 1
