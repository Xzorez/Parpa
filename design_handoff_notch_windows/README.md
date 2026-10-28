# Handoff: Parpa (notch para Windows)

## Overview
**Nombre del producto: Parpa** (de “parpadeo”). Úsalo como nombre de la app, del ejecutable (`Parpa.exe`), de la ventana en bandeja y de la ficha de la tienda. “Notch” se mantiene solo como nombre genérico de la isla en el código.

Una "isla" negra pegada al borde superior de la pantalla en Windows 10/11. En reposo es una píldora pequeña con mascota o reloj; crece para mostrar actividades en vivo (música, llamada, micro/cámara, descarga, foco), avisos de apps con acciones, indicadores del sistema (volumen, brillo, batería, Wi-Fi, auriculares, No molestar), y al abrirse da acceso a cinco ventanas: Inicio, Portapapeles, Bandeja (archivos), Tareas y Notas, además de Ajustes rápidos. Incluye pantalla de bienvenida (primer uso), menú del icono de bandeja y ventana de Ajustes completa.

## About the Design Files
Los archivos `design/*.dc.html` son **referencias de diseño hechas en HTML**: prototipos que muestran aspecto y comportamiento, **no código de producción**. La tarea es **recrearlos como app nativa**. Stack recomendado: **WinUI 3 + C# (.NET 8), Windows App SDK**. Para abrirlos: servir la carpeta `design/` con cualquier servidor estático (`npx serve design`) y abrir `Notch Prototipo.dc.html` — necesitan `support.js` al lado.

## Fidelity
**Alta fidelidad.** Colores, tipografía, medidas, curvas de animación y sonidos son finales. Reproducir al píxel.

---

## Arquitectura propuesta (WinUI 3)

```
Parpa.App/
  App.xaml(.cs)                 – single instance, arranque oculto, tray
  Windows/NotchWindow           – ventana sin borde, topmost, no activable
  Windows/SettingsWindow        – Ajustes completos (1000×680)
  Windows/WelcomeWindow         – primer uso (620×460)
  Controls/DotMatrix            – dibuja glifos 7×7 / 13×5 (Win2D o Composition)
  Controls/NotchShape           – forma + orejas cóncavas, anima W/H/radio
  Views/{Compact,Minimal,Osd,Peek,Drop,Launcher,Home,Clipboard,Shelf,Tasks,Notes,Quick}View
  Services/
    MediaService                – GlobalSystemMediaTransportControlsSessionManager
    NotificationService         – UserNotificationListener
    AudioService                – IAudioEndpointVolume + IMMNotificationClient
    BrightnessService           – WMI WmiMonitorBrightnessEvent
    PowerService, NetworkService, PrivacyIndicatorService (micro/cámara)
    ClipboardService, ShelfService, TasksService, NotesService
    HotkeyService (RegisterHotKey), FullscreenWatcher (SHQueryUserNotificationState)
    SoundService                – síntesis (NAudio) según data/sounds.json
    SettingsService             – %APPDATA%\Parpa\settings.json
  ViewModels/NotchViewModel     – máquina de estados (ver abajo)
  Assets/Fonts/                 – Geist, Geist Mono, Doto (SIL OFL)
  Data/glyphs.json, tokens.json, sounds.json, settings.default.json
```

## Ventana del notch
- Estilos: `WS_POPUP`, `WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST | WS_EX_LAYERED`. Fondo transparente; la forma se dibuja dentro.
- Responder `MA_NOACTIVATE` en `WM_MOUSEACTIVATE`. Solo activar la ventana cuando hay un campo de texto con foco (respuesta rápida, notas, nueva tarea) y devolver el foco al cerrar.
- Tamaño de ventana fijo = mayor estado (580×280 + 14 px de oreja a cada lado + margen de sombra). Hit-test: transparente al ratón fuera de la forma actual (`WM_NCHITTEST` → `HTTRANSPARENT`).
- Posición: borde superior del monitor elegido; horizontal = 22 % / 50 % / 78 % del ancho según `pos`, + `offset` px. Escala global `size` (80–120 %) con origen centro-superior. DPI PerMonitorV2; recolocar en `WM_DISPLAYCHANGE` / `WM_DPICHANGED`.
- Zona de activación por hover: la forma + `hoverSens` px (6 por defecto) alrededor.

## Forma
- Rectángulo con esquinas inferiores redondeadas (radio según estado) y dos "orejas" de 14×14 px a izquierda y derecha arriba: cuarto de círculo cóncavo (relleno = color de fondo, recorte circular de radio 14 en la esquina exterior inferior).
- Fondo `#0b0b0c` (oscuro) / `#f6f5f2` (claro). En claro, sombra: `0 1px 0 rgba(0,0,0,.06)` + `0 8px 18px rgba(0,0,0,.14)`.
- `overflow: hidden` — el contenido se recorta a la forma durante la animación.

## Máquina de estados

Prioridad (gana el primero): **Arrastre > Abierto > Aviso > Indicador sistema > Actividad > Reposo**. Además `minimal` sustituye a Actividad/Reposo si está activado.

| Estado | W×H (px) | Radio | Entrada | Salida |
|---|---|---|---|---|
| Reposo | 120×34 | 16 | sin actividad | — |
| Actividad | ver tabla abajo ×34 | 16 | sesión activa | fin de sesión |
| Minimal | max(96, 0.6×W actividad)×24 | 12 | ajuste `minimal` | — |
| Indicador | 340×40 | 16 | evento sistema | 1,6 s |
| Aviso | 420×112 | 26 | notificación permitida | `notifDur` s (4,5); hover pausa |
| Soltar | 420×84 | 26 | DragEnter | DragLeave/Drop |
| Menú burbujas | 580×264 | 30 | clic botón central | elegir burbuja |
| Abierto | 580×(176/196/214/236/280/222/246) | 30 | hover 250 ms, clic, atajo | salir ratón + 400 ms, o clic fuera |

Anchos de actividad: música 290, llamada 250, mensaje 250, Claude 230, tarea 240, descarga 220, micro 190, foco 160.
Altos abierto: Inicio 176 (196 con letra de canción), Portapapeles 214, Bandeja 236, Tareas 280, Notas 222, Ajustes rápidos 246.

Reglas:
- Avisos no interrumpen si está abierto o anclado (pin): se encolan.
- Indicadores se descartan si hay aviso visible o está abierto.
- Pantalla completa (`QUNS_BUSY`, `QUNS_RUNNING_D3D_FULL_SCREEN`, `QUNS_PRESENTATION_MODE`) + `hideFull` → opacidad 0 y avisos encolados.
- No molestar: bloquea avisos (sonido `decline` + toast "☾ NO MOLESTAR ACTIVO"), muestra glifo `luna` junto a la actividad.
- Apps desactivadas en Ajustes → toast "✕ AVISOS DE ESTA APP DESACTIVADOS".

## Vistas (ver `design/Notch Prototipo.dc.html`)

**Compacto (34 px alto)**: padding `0 14px 2px 12px`, gap 10. Glifo 7×7 (puntos 2 px, gap 1) + etiqueta Geist Mono 500 10px tracking .1em + elemento derecho: texto mono 11px `#8d8d92`, número Doto 700 16px, punto vivo 6 px en acento (parpadeo 1↔0,25 en pasos de 0,5 s) o barra de 12 puntos de 3 px (progreso).

**Indicador sistema (40 px)**: padding `0 16px 2px`, gap 12. Glifo + etiqueta (mono 500 10px .1em) y subetiqueta (mono 9px `#8d8d92`) + 16 barras de 4 px de ancho, altura proporcional, gap 3 + valor Doto 700 15px, mín. 40 px, alineado a la derecha.

**Aviso (420 px)**: padding `10px 16px 12px`, gap 8. Cabecera: glifo + "APP · REMITENTE" mono 500 10px `#8d8d92`. Cuerpo Geist 13px/1.35, máx. 2 líneas. Dos botones píldora (mono 500 10px, padding 6×10, borde `#2a2a2e`; primario fondo `#f2f2f0` texto `#0b0b0c`). Responder → input píldora fondo `#151517`, borde `#2a2a2e`, padding 7×12.

**Abierto (580 px)**: padding `12px 18px 18px`, gap 12. Cabecera 28 px en grid `1fr auto 1fr`: glifo + título de ventana a la izquierda, botón de menú circular 28 px centrado, controles (pin, ajustes) a la derecha. Tarjetas: fondo `#151517`, radio 14, padding 12. Filas de listas: padding `9px 4px`, borde superior `1px #1e1e21`, Geist 12px; índice Doto 700 14px `#5a5a60`.

**Menú burbujas**: área 192 px alto. Centro: mascota 108×62 (matriz 13×5, puntos 4 px, gap 1,5) en (50 %, 56 px). Burbujas 54 px Ø, glifo 7×7 de 3 px, etiqueta mono 500 9px tracking .14em debajo (7 px), visible en hover. Hasta 5 en semicírculo, reparto 2-1-2; si se ocultan ventanas, redistribuir uniformemente. Insignia: 18 px alto, radio 9, mín. 18 de ancho, esquina superior derecha (−3, −3).

**Inicio**: grid de tarjetas `MÚSICA 1.5fr | FOCO 1fr | SISTEMA 1fr | PRÓXIMA TAREA 1fr` (cada una activable). Música: carátula 48 px radio 8, título Geist 600 14px, artista mono 10px, progreso y controles. Foco: Doto 900 34px + 25 puntos de 4 px. Sistema: filas `BAT/VOL/WIFI` con puntos 3 px.

**Ajustes rápidos**: 6 tiles de 62 px alto, radio 14, padding `10px 12px` (Wi-Fi, Bluetooth, No molestar, Modo foco, Micro, Tema). Fila inferior: volumen de sonidos (barras 5 px) + botón "AJUSTES" que abre la ventana completa.

**Ajustes completos** (`design/Ajustes Notch.dc.html`, 1000×680): navegación lateral; secciones General, Posición, Apariencia, Avisos, Ventanas (reordenar y ocultar), Sonido, Sistema, Atajos. Cambios se aplican al instante.

**Bienvenida** (`design/Bienvenida y Bandeja.dc.html` #9a, 620×460, fondo `#111113`, radio 14): 4 pasos con indicador de barras (activo 18 px, resto 4 px). 1 Hola (mascota + "HOLA, SOY PARPA" Doto 900 40px). 2 Monitor y posición. 3 Permisos (notificaciones, inicio con Windows). 4 Listo + atajo.

**Menú de bandeja** (#9b, título “Parpa”): abrir, pausar avisos 1 h / hasta mañana, No molestar, Ajustes, Salir.

## Interacciones
- Hover 250 ms (`openTime`) abre en **Inicio**. Clic en el notch o atajo (Win+Alt+N, `RegisterHotKey`) abre en la última ventana.
- Rueda del ratón sobre el notch abierto cambia de ventana (si `wheel`).
- Cerrar: salir ratón + `closeDelay` (0,4 s), o solo clic fuera (`closeOn: "click"`). Pin lo mantiene.
- Arrastrar: archivo → Bandeja (acciones: comprimir imagen, a PNG, unir PDF, ZIP de carpeta, copiar ruta); texto → nueva nota; URL → Portapapeles. Arrastrar desde Bandeja hacia fuera saca el archivo.
- Ctrl+C en cualquier app → aviso breve "COPIADO" + entrada en Portapapeles (límite `clipLimit` 50).
- Notas: Ctrl+Enter guarda.
- Tareas: clic en la fila alterna hecha; sonido `confirm`.
- Llamada entrante: sonido `ring` en bucle cada 1,2 s hasta responder/rechazar.

## Animación
- Cambio de forma (W, H, radio): **450 ms `cubic-bezier(.3, 1.2, .5, 1)`** (rebote leve). Implementar con Composition (`Compositor.CreateVector2KeyFrameAnimation` + `CubicBezierEasingFunction`) sobre un `CompositionRoundedRectangleGeometry` / clip.
- Contenido: cross-fade; el nuevo entra tras el ~40 % del cambio de tamaño.
- Modo reducido o "Efectos de animación" de Windows desactivado (`UISettings.AnimationsEnabled`): 150 ms `ease`, sin rebote.
- Burbujas: escala 0.6→1 + opacidad, 300 ms, escalonado 30 ms.
- Indicador de pasos: ancho 4→18 px, 300 ms.
- Ecualizador: tick 100 ms; se congela al pausar.

## Mascota
Matriz 13×5 procedural, tres modos (`curMode()` en el prototipo):
- **awake**: dos ojos de 2×3 en columnas 3 y 8; miran a izquierda/derecha (desplazamiento ±1 en ciclos de 8 s); parpadeo cada 4,5 s (solo fila central 0,2 s).
- **sleep**: ojos cerrados (fila 3) en `#7d7d83`, "z" subiendo en columnas 11–12.
- **happy/excited**: ojos en "^" que botan 1 fila cada 0,3 s.

## Sonidos
Sintetizados — `data/sounds.json`. Cada tono: oscilador (sine/triangle/square), rampa exponencial de frecuencia f0→f1, ganancia: 0 → `level × volumen` en 5 ms lineal, luego exponencial a 0,0001 en `dur`. Volumen maestro 60 %. Usar NAudio (`SignalGenerator` + `MixingSampleProvider`) o pre-renderizar a WAV al iniciar. Silenciar con No molestar o pantalla completa.

| Nombre | Uso |
|---|---|
| click | botones, conmutadores |
| tick | pasos de rueda / volumen |
| page | cambio de ventana |
| open / close | expandir / contraer |
| notify | aviso nuevo |
| ring | llamada entrante |
| confirm | tarea hecha, archivo guardado |
| send | respuesta enviada |
| decline | rechazo / acción bloqueada |

## APIs de Windows
| Función | API |
|---|---|
| Música | `GlobalSystemMediaTransportControlsSessionManager` |
| Avisos de apps | `UserNotificationListener` (pedir acceso en bienvenida; si se deniega, ocultar sección Avisos y mostrar aviso en Ajustes) |
| Micro / cámara en uso | `HKCU\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\{microphone,webcam}\…\LastUsedTimeStop == 0` |
| Volumen | Core Audio `IAudioEndpointVolume` + callback |
| Brillo | WMI `root\wmi` `WmiMonitorBrightnessEvent` |
| Batería | `Windows.System.Power.PowerManager` |
| Wi-Fi | `NetworkInformation.NetworkStatusChanged` |
| Auriculares | `IMMNotificationClient`; batería BT vía `DeviceWatcher` |
| No molestar | Estado de "Asistente de concentración" (WNF, no documentado) — o modo propio de la app |
| Pantalla completa | `SHQueryUserNotificationState` (sondeo 1 s) |
| Portapapeles | `Clipboard.ContentChanged` |
| Atajo | `RegisterHotKey` |
| Inicio con Windows | `StartupTask` (MSIX) o `HKCU\…\Run` |
| Icono de bandeja | `Shell_NotifyIcon` (H.NotifyIcon.WinUI) |

## Estado y persistencia
`%APPDATA%\Parpa\settings.json` con el esquema de `data/settings.default.json` (`s` = ajustes, `wins` = orden/visibilidad de ventanas `[id, nombre, visible]`). Es el mismo que el prototipo guarda en `localStorage["punto.settings.v1"]`, así que se puede importar. Notas, tareas, portapapeles y bandeja en archivos JSON separados en la misma carpeta.

## Idiomas (es / en)
- Todos los textos visibles están en `data/strings.json`, agrupados por zona, con clave estable y versión `es` y `en`. No escribir textos en el código.
- Ajuste `lang`: `auto` (por defecto, sigue el idioma de pantalla de Windows; si no es es/en, usar `en`), `es`, `en`. Se cambia al instante sin reiniciar. En Ajustes › General › IDIOMA (control segmentado SISTEMA · ESPAÑOL · ENGLISH).
- WinUI: generar `Strings/es-ES/Resources.resw` y `Strings/en-US/Resources.resw` desde el JSON; `x:Uid` en XAML y `ResourceLoader` en código.
- Etiquetas mono en MAYÚSCULAS: máx. ~28 caracteres en el notch de 340–580 px. Si un texto no cabe, recortar con “…” y mostrar el completo en tooltip; nunca reducir el cuerpo de letra.
- Plurales con claves `_one` / `_other`. Fechas y horas con la cultura activa (24 h en es, 12 h por defecto en en salvo ajuste `h24`).
- Ficha de la tienda y política de privacidad: publicar en los dos idiomas.

## Design Tokens
`data/tokens.json` — colores oscuro/claro (los mismos nombres), acentos (OKLCH + hex aproximado), radios, medidas por estado, movimiento.
Tipografía:
- **Doto** 700/900 — números (reloj, temporizadores, valores) 13–40 px.
- **Geist Mono** 400/500 — etiquetas en MAYÚSCULAS 9–11 px, tracking .06–.14 em.
- **Geist** 400/500/600 — texto 12–15 px.
Todas SIL OFL: incluir en `Assets/Fonts`.
El acento solo para estados vivos (micro/cámara, grabación, llamada, vencido). Verde éxito `oklch(0.78 0.14 150)`.

## Icono (dirección elegida: 10b · mirada de la mascota)
- App: cuadrado redondeado (radio 22 %) `#0b0b0c` con dos ojos de 2×3 puntos `#f2f2f0` (columnas 0-1 y 5-6 de una rejilla de 7; ancho total 56 % del icono, centrado).
- ≤ 24 px: los ojos pasan a dos rectángulos redondeados sólidos (proporción 1:1,5).
- Bandeja del sistema: solo los dos ojos, monocromo — blanco sobre barra oscura, `#1c1c1c` sobre barra clara (detectar con `UISettings` / `SystemUsesLightTheme`).
- PNG en `assets/icon/` (app 16–256, bandeja 16–32 en claro y oscuro). Generar `.ico` multi-tamaño y los logos MSIX (Square44x44, Square150x150, StoreLogo) a partir de ellos.
- Referencia: `design/Icono y Estados.dc.html` #10b.

## Estados vacíos y errores (`design/Icono y Estados.dc.html` #11a–11f)
Regla: la mascota explica, una línea dice qué hacer y, si hay arreglo, un botón lo hace.
- **Inicio sin música**: la tarjeta MÚSICA se mantiene (fondo de puntos `#2c2c31` 7 px, borde interior `#1e1e21`) con “Nada sonando” + “Dale al play en cualquier app y aparecerá aquí.” Foco sin iniciar: “25:00” en `#5a5a60` + botón EMPEZAR.
- **Bandeja vacía**: área de puntos con mascota contenta y “ARRASTRA ARCHIVOS HASTA EL NOTCH” / “SE GUARDAN AQUÍ HASTA QUE LOS SAQUES”.
- **Portapapeles vacío**: dos filas fantasma (barras `#151517`) + “COPIA ALGO CON [CTRL] [C] Y APARECERÁ AQUÍ”.
- **Notas vacías**: campo con placeholder + “Sin notas todavía · También puedes arrastrar texto desde cualquier app hasta el notch.”
- **Tareas completas**: contador “5 / 5” en verde, mascota contenta, “TODO HECHO” (Doto 900 22px), botón “+ NUEVA TAREA”.
- **Permiso de avisos denegado**: tarjeta fija arriba en Inicio con mascota apagada, “No puedo leer tus avisos” y botón ACTIVAR → `ms-settings:privacy-notifications`. Se puede descartar; en Ajustes › Avisos la sección queda gris con el mismo botón.
- **Errores breves** (punto rojo de acento + sonido `decline`, duración de aviso): acción de Bandeja fallida (con REINTENTAR), reproductor no compatible (indicador 340×40), atajo ocupado por otra app (al arrancar, una vez, con enlace a Ajustes).

## Assets
- Iconos: no hay imágenes; todo son glifos de puntos 7×7 en `data/glyphs.json` (`X` = encendido). Encendido `primary`, apagado `glyphOff` o transparente.
- Carátulas de música: las da la sesión de medios del sistema.

## Files
- `design/Notch Prototipo.dc.html` — prototipo principal (todos los estados, botones de demostración debajo del escritorio simulado).
- `design/Ajustes Notch.dc.html` — ventana de Ajustes.
- `design/Bienvenida y Bandeja.dc.html` — primer uso y menú de bandeja.
- `design/Icono y Estados.dc.html` — icono (10b elegido) y estados vacíos/errores.
- `assets/icon/*.png` — icono de app y de bandeja.
- `design/Entrega Desarrollo.dc.html` — especificación visual (con reproductor de sonidos).
- `design/support.js` — runtime necesario para abrir los anteriores.
- `data/*.json` — tokens, glifos, sonidos, ajustes por defecto y `strings.json` (todos los textos en es/en).

## Limitaciones del prototipo web
La tecla Win, elegir monitor y "no robar foco" están simulados en web; en nativo funcionan con las APIs indicadas.
