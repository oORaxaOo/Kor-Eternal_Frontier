# KOR Eternal Frontier — Linux x86_64

## So startest du das Spiel

**Im fertigen Download:** ZIP vollständig in einen normalen Benutzerordner entpacken und `start-kor.sh` doppelklicken. Falls dein Dateimanager Skripte nicht direkt ausführt, im entpackten Ordner ein Terminal öffnen und `./start-kor.sh` eingeben. Kein .NET-SDK und keine Administratorrechte nötig.

**Aus dem Git-Quellcode:** .NET 8 SDK und SDL3 installieren, im Projektordner `./launch-kor.sh` ausführen. Der Starter verwendet automatisch ein bereits gebautes `artifacts/linux-x64/GondwanaGame`; wenn noch kein fertiges Programm vorliegt, versucht er den Quellcode direkt mit .NET zu starten.

## Diagnose

Die automatisierten Simulations- und Integrationsprüfungen laufen ohne Spielfenster:

```bash
./GondwanaGame --self-test
```

Für einen vollständigen lokalen Linux-Build:

```bash
./build-linux.sh
./launch-kor.sh
```

Das portable Paket enthält die .NET-Laufzeit und die SDL3-Bibliothek. Benötigt wird eine normale Linux-Desktop-Sitzung unter X11 oder Wayland mit XWayland-Unterstützung.

## Spielstände

Der Spielstand liegt hier:

`~/.local/share/kor-eternal-frontier/session.json`

Bitte den Ordner `data/` im Paket neben der ausführbaren Datei belassen.
