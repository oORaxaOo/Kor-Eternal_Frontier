# KOR Eternal Frontier — Linux x86_64

## Start the game

1. Extract the complete ZIP archive into a folder you can write to.
2. Open a terminal in that folder and run:

   `./start-kor.sh`

   If the executable bit was not preserved by your file manager, run
   `chmod +x start-kor.sh` once and then start it again.

The package contains the self-contained .NET runtime and the SDL3 shared library
used by this build; a separate .NET SDK installation is not required. It targets
Linux x86_64 and expects a desktop graphics session with X11, or XWayland when
running from a Wayland desktop.

## Diagnostics

Run the built-in deterministic simulation/integration tests without opening the
game window:

`./GondwanaGame --self-test`

The normal launcher forwards additional arguments to the game, but most players
should start it without arguments.

## Saves and data

Game session saves are stored at:

`~/.local/share/kor-eternal-frontier/session.json`

The bundled `data/` directory contains the game's research and item definitions.
Keep it alongside the executable.

## Package notes

This is a portable Linux x86_64 package, not a distribution-specific installer.
No root privileges are required to extract or run it. The SDL3 library is bundled
with the game; the desktop's normal graphics/system libraries must still be
available.
