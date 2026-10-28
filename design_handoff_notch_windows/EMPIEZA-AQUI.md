# EMPIEZA AQUÍ — Parpa con Claude Code

Pega esto como primer mensaje en Claude Code, abierto en esta carpeta:

> Lee `EMPIEZA-AQUI.md`, `README.md` y todos los archivos de `data/`. Los `.dc.html` de `design/` son referencias visuales (ábrelos con `npx serve design`). Crea la app siguiendo el README y ve fase por fase: no pases a la siguiente hasta que yo confirme que la anterior funciona.

## Decisiones ya tomadas
- **Nombre:** Parpa. Ejecutable `Parpa.exe`, carpeta de datos `%APPDATA%\Parpa\`.
- **Stack:** WinUI 3 + C# (.NET 8), Windows App SDK. Sin Electron.
- **Licencia:** MIT, open source en GitHub. Sin monetización, sin telemetría, sin cuentas.
- **Idiomas:** español e inglés desde el día 1 (`data/strings.json` → `.resw`). Nada de textos escritos en el código.
- **No molestar:** modo propio de Parpa (no depender del estado no documentado de Windows).
- **Monitores:** el notch aparece solo en uno, el elegido en la bienvenida/Ajustes.
- **Icono:** 10b (ojos de la mascota). PNG en `assets/icon/`.

## Fases
1. **Base** — proyecto, ventana sin borde, siempre encima, sin robar foco, forma con orejas cóncavas, estados Reposo ↔ Abierto con la animación `cubic-bezier(.3,1.2,.5,1)` 450 ms, hover/atajo para abrir. Glifos de puntos desde `data/glyphs.json`.
2. **Sistema** — música (SMTC), indicadores de volumen, brillo, batería, Wi-Fi y auriculares; mascota animada en reposo.
3. **Avisos** — `UserNotificationListener` con permiso, aviso con 2 acciones y respuesta rápida, cola y prioridades, No molestar.
4. **Ventanas** — Inicio, Portapapeles, Bandeja (arrastrar y soltar + acciones), Tareas, Notas, menú de burbujas, estados vacíos y errores.
5. **Producto** — Ajustes completos (`settings.default.json`), bienvenida, icono de bandeja con menú, sonidos (`sounds.json`), tema claro, idiomas, inicio con Windows.
6. **Publicar** — instalador, firma (SignPath, gratis para open source), winget, GitHub Releases.

## Reglas
- Medidas, colores y tiempos: exactos según `README.md` y `data/tokens.json`.
- Cada fase en su rama/commit, con una lista breve de cómo probarla.
- Si algo del diseño no es posible en Windows, propón alternativa antes de cambiarlo.
- Consumo objetivo: < 100 MB RAM, 0 % CPU en reposo.
