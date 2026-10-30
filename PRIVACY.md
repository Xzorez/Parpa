# Privacidad · Privacy

## Español

**Todo se queda en tu PC.** Parpa no tiene cuentas, no envía nada a ningún servidor y no incluye telemetría ni analítica.

Qué lee Parpa y para qué:

| Dato | Para qué | Dónde se guarda |
|---|---|---|
| Avisos de tus apps (con tu permiso de Windows) | Enseñarlos en el notch | No se guardan |
| Portapapeles (texto) | Historial de Portapapeles | `%APPDATA%\Parpa\clipboard.json` |
| Música que suena (sesión de medios de Windows) | Tarjeta y actividad de música | No se guarda |
| Volumen, brillo, batería, Wi‑Fi, auriculares | Indicadores | No se guardan |
| Uso del micrófono y la cámara | Punto de aviso | No se guarda |
| Notas, tareas, archivos de la Bandeja | Las ventanas del notch | `%APPDATA%\Parpa\` |
| Ajustes | Recordar tus preferencias | `%APPDATA%\Parpa\settings.json` |

- Los gestores de contraseñas (1Password, Bitwarden…) se excluyen del portapapeles por defecto, y Parpa ignora lo que Windows marca como privado.
- Los errores se apuntan en `%APPDATA%\Parpa\parpa.log`, que nunca sale del equipo.
- Al desinstalar, Parpa te pregunta si quieres borrar también esa carpeta.
- Parpa solo se conecta a internet si tú lo pides: al abrir un enlace o al pulsar BUSCAR en Ajustes › Sistema › Actualizaciones, que pregunta a GitHub cuál es la última versión publicada, y al pulsar ACTUALIZAR, que descarga de GitHub el instalador de esa versión.

## English

**Everything stays on your PC.** Parpa has no accounts, sends nothing to any server and has no telemetry or analytics.

What Parpa reads and why:

| Data | Why | Where it is stored |
|---|---|---|
| Notifications from your apps (with your Windows permission) | Show them in the notch | Not stored |
| Clipboard (text) | Clipboard history | `%APPDATA%\Parpa\clipboard.json` |
| What is playing (Windows media session) | Music card and activity | Not stored |
| Volume, brightness, battery, Wi‑Fi, headphones | Indicators | Not stored |
| Microphone and camera use | Privacy dot | Not stored |
| Notes, tasks, Shelf files | The notch windows | `%APPDATA%\Parpa\` |
| Settings | Remember your preferences | `%APPDATA%\Parpa\settings.json` |

- Password managers (1Password, Bitwarden…) are excluded from the clipboard by default, and Parpa skips anything Windows marks as private.
- Errors are written to `%APPDATA%\Parpa\parpa.log`, which never leaves your computer.
- When you uninstall, Parpa asks whether to delete that folder too.
- Parpa only goes online when you ask it to: when you open a link, or when you press CHECK in Settings › System › Updates, which asks GitHub for the latest published version, and when you press UPDATE, which downloads that version’s installer from GitHub.
