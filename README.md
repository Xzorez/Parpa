# Parpa

**Tu Windows, con un notch que parpadea.** Música, avisos, archivos y notas en una isla arriba de la pantalla. Se abre cuando pasas el ratón y se esconde cuando no la necesitas.

*Your Windows, with a notch that blinks. Music, notifications, files and notes in an island at the top of the screen.* → [English](#english)

Gratis · código abierto (MIT) · Windows 10 (2004+) y 11 · 64 bits · español e inglés · [web](https://xzorez.github.io/Parpa/).

## Instalar

- **Instalador:** descarga `Parpa-Setup-<versión>-x64.exe` (o `-arm64`) de [Releases](../../releases/latest). No pide permisos de administrador.
- **Terminal:**

  ```bash
  winget install Parpa
  ```

## Qué hace

- **Música** de cualquier app mientras suena, con carátula y controles.
- **Avisos** de tus apps con dos acciones, cola por prioridad y No molestar.
- **Indicadores** de volumen, brillo, batería, Wi‑Fi y auriculares; punto de aviso si el micro o la cámara están en uso.
- **Ventanas:** Inicio, Portapapeles, Bandeja (arrastra archivos al notch: comprimir, a PNG, unir PDF, ZIP), Tareas, Notas y Ajustes rápidos.
- Atajo `Win + Alt + N`, rueda del ratón, tema claro y oscuro, sonidos y una mascota que te mira.

## Privacidad

Todo se queda en tu PC: sin cuentas, sin servidores, sin telemetría. Detalles en [PRIVACY.md](PRIVACY.md).

## Política de firma de código · Code signing policy

**Free code signing provided by [SignPath.io](https://signpath.io), certificate by [SignPath Foundation](https://signpath.org).**

- Autores y revisores · Committers and reviewers: [Xzorez](https://github.com/Xzorez)
- Aprobación de firmas · Approvers: [Xzorez](https://github.com/Xzorez)
- Privacidad · Privacy: this program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it. See [PRIVACY.md](PRIVACY.md).

## Compilar

Requisitos: .NET 8 SDK y Windows 10 SDK (o Visual Studio 2022 con la carga de trabajo de WinUI).

```bash
dotnet build Parpa.App/Parpa.App.csproj -c Debug -p:Platform=x64
```

Instalador (necesita [Inno Setup 6](https://jrsoftware.org/isinfo.php)):

```bash
pwsh tools/build-installer.ps1 -Version 1.0.0
```

Los textos están en `Parpa.App/Data/strings.json` (es/en); los `.resw` se generan al compilar. Cómo publicar una versión: [docs/RELEASING.md](docs/RELEASING.md).

---

## English

**Your Windows, with a notch that blinks.** Music, notifications, files and notes in an island at the top of the screen. It opens when you hover it and hides when you don't need it.

Free · open source (MIT) · Windows 10 (2004+) and 11 · 64-bit · Spanish and English.

- **Install:** download `Parpa-Setup-<version>-x64.exe` (or `-arm64`) from [Releases](../../releases/latest) — no admin rights needed — or run `winget install Parpa`.
- **Features:** music from any app while it plays, notifications with two actions and a priority queue, Do Not Disturb, volume/brightness/battery/Wi‑Fi/headphone indicators, mic and camera dot, Home, Clipboard, Shelf (drop files on the notch), Tasks, Notes and Quick settings.
- **Privacy:** everything stays on your PC — no accounts, no servers, no telemetry. See [PRIVACY.md](PRIVACY.md).
- **Build:** .NET 8 SDK + Windows 10 SDK, then `dotnet build Parpa.App/Parpa.App.csproj -c Debug -p:Platform=x64`.
