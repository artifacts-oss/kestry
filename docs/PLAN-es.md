# Kestry — Plan del proyecto

Gestor de descargas web (torrents, magnets y enlaces directos) construido en .NET, con aria2 como motor de descarga y una interfaz construida con el design system **Kestrel**. Pensado para correr en un PC o servidor doméstico y gestionarse desde el navegador, en local o en remoto.

---

## 1. Objetivos

| # | Objetivo | Criterio de aceptación |
|---|----------|------------------------|
| 1 | Descargar torrents, magnets y enlaces HTTP/HTTPS/FTP | Los tres tipos se añaden y completan usando aria2 |
| 2 | Descargas simultáneas | Varias descargas activas a la vez; el resto queda en cola |
| 3 | Importación masiva por copy/paste | Pegar N enlaces en un textarea (uno por línea) y añadirlos en un clic |
| 4 | Panel visual de descargas | Lista en tiempo real con progreso, velocidad, ETA y acciones |
| 5 | Configuración | Máx. descargas simultáneas, límite de velocidad, carpeta de descargas, apagado al terminar |
| 6 | Acceso web y remoto | La UI funciona desde cualquier navegador; preparada para publicarse tras un túnel/VPN |
| 7 | Sencillez | Instalación en un paso (Docker o binario) y UI usable sin leer documentación |

### Fuera de alcance (v1)
- Login / gestión de usuarios (se delega en Cloudflare Zero Trust o VPN, ver §8).
- Multiusuario, permisos o cuotas.
- Búsqueda de torrents, RSS o integración con indexadores.
- Streaming / previsualización de archivos.
- App móvil nativa (la web será responsive).
- Reordenar la cola, selección múltiple con acciones masivas y panel de detalle por descarga (ver §15).
- Enlaces `sftp://` (depende de cómo esté compilado aria2).
- Interfaz en inglés (los textos se centralizan para poder traducirlos después).

---

## 2. Stack tecnológico

| Capa | Tecnología | Motivo |
|------|------------|--------|
| Backend | **.NET 10 (LTS)** · ASP.NET Core Minimal APIs | Ligero, multiplataforma, un único proceso |
| Tiempo real | **Server-Sent Events** (`TypedResults.ServerSentEvents` de .NET 10 + `EventSource` del navegador) | El flujo es solo servidor → cliente; sin hub ni librería cliente |
| Motor de descargas | **aria2c** (proceso hijo) vía **JSON-RPC sobre HTTP** (`HttpClient`) | Soporta HTTP/FTP/BitTorrent/Magnet, colas y límites; peticiones sin estado, sin reconexión |
| Persistencia | `settings.json` + fichero de sesión de aria2 (`--save-session`) | Sin base de datos: aria2 ya guarda su cola |
| Frontend | **React + TypeScript + Vite** | SPA ligera |
| UI/UX | **Design system Kestrel** ([artifact](https://claude.ai/artifact/UvfFmAMTTYHUEUiTDM9uQC)): tokens, iconografía, componentes y patrones | Design system pedido; es la referencia de diseño y se reimplementa sobre Tailwind (ver §9) |
| Estilos | **Tailwind CSS v4** (`@tailwindcss/vite`) con un `tokens.css` que traduce los tokens de Kestrel | Utilidades sin CSS ad hoc; tema claro/oscuro vía variables CSS |
| Componentes | HTML nativo + **Radix UI** (paquete único `radix-ui`) solo para `Dialog`, `DropdownMenu` y `Switch` | Radix donde la accesibilidad a mano es difícil (foco, portales, menús); el resto, nativo como en Kestrel |
| Empaquetado | Frontend compilado servido como estáticos desde `wwwroot` | Un solo proceso, un solo puerto |
| Distribución | Imagen **Docker** (aria2 se instala con `apt` al construirla) + zip con binario self-contained y `wwwroot/` (Windows/Linux, sin aria2: ver §7) | Instalación en un paso; aria2 nunca se versiona ni se empaqueta en este repositorio |

---

## 3. Arquitectura

```
 Navegador (React + Tailwind + Radix UI)
        │  HTTP (REST)  +  SSE (/api/events)
        ▼
┌──────────────────────────────────────────────┐
│ Kestry.Server (ASP.NET Core)                 │
│                                              │
│  REST API                                    │
│  /api/events (SSE) ◄── DownloadMonitor (1 s) │
│                                              │
│  Aria2Client (JSON-RPC/HTTP)                 │
│  Aria2ProcessManager (arranca/vigila aria2c) │
│  SettingsService (settings.json)             │
│  ShutdownService (apagado al terminar)       │
└──────────────┬───────────────────────────────┘
               │ http://127.0.0.1:6800/jsonrpc  (rpc-secret aleatorio)
               ▼
           aria2c  ──►  carpeta de descargas
```

### Decisiones clave
- **aria2 nunca se expone**: escucha solo en `127.0.0.1`, con `--rpc-secret` generado al arrancar. El navegador solo habla con Kestry.
- **Kestry gestiona el ciclo de vida de aria2**: lo lanza al iniciar, lo reinicia si muere, y lo cierra limpiamente (`aria2.saveSession` + `aria2.shutdown`) al parar.
- **Un único origen de verdad**: el estado de las descargas vive en aria2. Kestry no duplica la cola; solo la consulta y la reenvía.
- **Push en tiempo real**: `DownloadMonitor` consulta `tellActive`/`tellWaiting`/`tellStopped` + `getGlobalStat` cada segundo en un `system.multicall` (solo si hay clientes conectados) y emite un snapshot por SSE.
- **Sin notificaciones de aria2**: como ya se consulta cada segundo, los cambios de estado (completada, error) se detectan comparando snapshots. El cliente lanza los toasts; el servidor usa la misma comparación para la condición de apagado.

---

## 4. Estructura del repositorio

```
Kestry/
├── README.md
├── AGENTS.md
├── Kestry.slnx
├── design/kestrel/                    # Export del design system Kestrel usada por los tests (ver §9.1)
├── docs/                              # Plan original (referencia de solo lectura)
│   ├── PLAN-es.md
│   ├── PLAN-en.md
│   └── design/kestrel/                # Copia de referencia del export
├── src/
│   ├── Kestry.Server/
│   │   ├── Program.cs                 # Composición, endpoints, estáticos
│   │   ├── KestryOptions.cs           # Configuración Kestry:* (§10)
│   │   ├── ApiErrors.cs               # ErrorBody + ErrorCodes (única lista de códigos de error de la API)
│   │   ├── Aria2/
│   │   │   ├── Aria2Locator.cs        # Busca aria2c o lo descarga (solo Windows, ver §7)
│   │   │   ├── Aria2ProcessManager.cs # IHostedService: lanza/vigila aria2c
│   │   │   ├── Aria2Client.cs         # JSON-RPC sobre HTTP
│   │   │   └── Aria2Models.cs         # DTOs de aria2 (status, files, bittorrent)
│   │   ├── Downloads/
│   │   │   ├── DownloadService.cs     # Añadir, pausar, reanudar, cancelar, eliminar
│   │   │   ├── DownloadEndpoints.cs   # Endpoints REST
│   │   │   ├── LinkParser.cs          # Parseo y validación del textarea
│   │   │   ├── PathGuard.cs           # Comprobación de AllowedRoots y normalización de rutas
│   │   │   ├── Snapshot.cs            # DTOs del snapshot
│   │   │   ├── SnapshotMapper.cs      # Estado de aria2 → fila del snapshot
│   │   │   ├── SnapshotBuilder.cs     # Un multicall → Snapshot
│   │   │   ├── SnapshotHub.cs         # Último snapshot + suscriptores
│   │   │   ├── DownloadMonitor.cs     # BackgroundService: consulta → hub
│   │   │   └── EventsEndpoint.cs      # SSE /api/events
│   │   ├── Settings/
│   │   │   ├── AppSettings.cs
│   │   │   ├── SettingsValidator.cs
│   │   │   ├── SettingsService.cs     # Lee/escribe settings.json, aplica a aria2
│   │   │   └── SettingsEndpoints.cs   # /api/settings, /api/system
│   │   ├── Shutdown/                  # No "System/": ese namespace rompe System.*
│   │   │   ├── ShutdownService.cs     # Cuenta atrás y condición
│   │   │   ├── SystemPower.cs         # ISystemPower: comando real de apagado por SO
│   │   │   └── ShutdownEndpoints.cs
│   │   ├── Security/
│   │   │   └── RequestGuard.cs        # Comprobaciones de Host/Origin en /api
│   │   ├── Health/
│   │   │   └── HealthCheck.cs         # Flag --healthcheck para sondas de contenedor
│   │   ├── appsettings.json
│   │   └── wwwroot/                   # Salida del build del frontend (no versionada)
│   └── kestry-web/
│       ├── package.json
│       ├── vite.config.ts
│       └── src/
│           ├── main.tsx
│           ├── App.tsx                # Layout + clase de tema (claro/oscuro) en <html>
│           ├── downloads.ts           # Filtros, contadores, comparación de snapshots
│           ├── settings.ts            # Modelo de ajustes y unidades de límite
│           ├── theme.ts               # Preferencia claro/oscuro/sistema
│           ├── api/                   # Cliente REST, traducción de errores, useSnapshot (EventSource)
│           ├── i18n/                  # config.ts, index.ts (useTranslations) y messages/{en,es,de,fr}.json
│           ├── styles/
│           │   └── tokens.css         # Escrito a mano desde tokens.json de Kestrel: variables (claro/oscuro) + @theme inline
│           ├── pages/
│           │   └── DownloadsPage.tsx
│           └── components/
│               ├── ui/                # Componentes de Kestrel (tabla del §9.4), Icon, Toaster
│               ├── Header.tsx         # Logotipo, interruptores rápidos, ajustes, LocaleSwitcher
│               ├── Sidebar.tsx        # Filtros NavItem, SpeedStat y estado de aria2
│               ├── DownloadsList.tsx  # Filas DownloadItem
│               ├── AddDownloadsModal.tsx
│               ├── ConfirmDialog.tsx
│               ├── SettingsDrawer.tsx
│               ├── LocaleSwitcher.tsx
│               └── ShutdownBanner.tsx
├── tests/
│   └── Kestry.Server.Tests/           # xUnit: unitarias, endpoints (KestryFactory), Smoke/ con aria2 real
├── docker/
│   ├── Dockerfile
│   ├── Dockerfile.dockerignore
│   ├── docker-compose.yml             # Kestry + (opcional) cloudflared
│   └── .env.example
├── deploy/                            # Unidad systemd + regla polkit para apagar
├── scripts/
│   └── publish.sh                     # Zips self-contained en artifacts/
└── .github/workflows/                 # ci.yml, release.yml
```

---

## 5. Funcionalidades en detalle

### 5.1 Añadir descargas (copy/paste)
Modal **"Añadir descargas"** (`Dialog`), todo en una sola vista:

- **Enlaces**: `TextArea` grande en fuente `mono`. Se aceptan:
  - `http://`, `https://`, `ftp://`
  - `magnet:?xt=urn:btih:...`
  - URLs que terminan en `.torrent` (aria2 las descarga y continúa con el contenido si `follow-torrent=true`)
- **Archivos .torrent**: zona de arrastre `FileDrop` debajo del textarea (múltiples archivos) → `aria2.addTorrent` en base64.
- **Opciones avanzadas** (`<details>`, cerrado por defecto): subcarpeta de destino (`TextField` mono), añadir en pausa (`Checkbox`).

Parseo (`LinkParser`):
1. Separar por saltos de línea, espacios y comas; recortar.
2. Ignorar líneas vacías y comentarios (`#`).
3. Clasificar cada línea: `Http`, `Ftp`, `Magnet`, `TorrentUrl`, `Invalid`.
4. Eliminar duplicados (dentro del lote y contra descargas ya existentes, por URL o info-hash).

UX: debajo del textarea, un resumen en vivo con `StatusBadge`s: *"12 válidos · 2 duplicados · 1 inválido"*, con los inválidos listados. El botón **Añadir** solo envía los válidos. Atajo: pegar enlaces con `Ctrl+V` en cualquier parte de la página (fuera de campos de texto) abre el modal con el texto ya pegado.

### 5.2 Panel de descargas
- **Barra de acciones** (`Toolbar`): buscador por nombre a la izquierda; a la derecha *Pausar todo* y *Reanudar todo* (`Button` quiet) y, el último, el botón primario *Añadir*.
- **Filtros** (`NavItem` con contador, en la barra lateral, en el orden de Kestrel): Todas · Descargando · En cola · Completadas · Error. *En cola* incluye las pausadas, igual que `aria2.tellWaiting`.
- **Lista**: una fila `DownloadItem` por descarga (sirve igual en escritorio y móvil). Cada fila muestra:

  | Dato | Presentación |
  |------|--------------|
  | Tipo | `Icon` según el archivo; el origen (HTTPS / FTP / Torrent / Magnet) abre la línea de detalle |
  | Nombre | Texto con la ruta en el atributo `title` |
  | Estado | `StatusBadge` (estados de descarga del §9.5) |
  | Progreso | `ProgressBar` con el mismo estado; indeterminado mientras un magnet obtiene metadatos |
  | Tamaño, velocidad, ETA | Línea de detalle: `X de Y GB · ↓ / ↑ (↑ solo torrents) · quedan N min` |
  | Peers/Seeds | En la línea de detalle, solo torrents (`connections` / `numSeeders` de `tellStatus`) |
  | Acciones | `IconButton` según el estado (como `DownloadItem` de Kestrel): descargando → Pausar, Cancelar · pausada → Reanudar, Cancelar · en cola → Cancelar · error → Reintentar, Eliminar · completada → Copiar ruta, Eliminar. En móvil van dentro de un `DropdownMenu` |

  - **Cancelar** (icono `x`): pide confirmación, detiene la descarga (`aria2.forceRemove`), la quita de la lista (`aria2.removeDownloadResult`) y Kestry borra los archivos parciales.
  - **Eliminar** (icono `trash`): quita de la lista una descarga terminada o fallida (`aria2.removeDownloadResult`). Pide confirmación, con la opción "borrar también archivos".
  - El borrado de archivos lo hace Kestry (aria2 no borra nada), solo dentro de `AllowedRoots` y con la ruta normalizada.
  - **Copiar ruta** (icono `folder`): copia al portapapeles la ruta de la descarga en el servidor. Desde un navegador remoto no se puede abrir una carpeta del servidor.

- **Totales** (`SpeedStat` en la barra lateral): velocidad total ↓/↑ y espacio libre en disco. El nº de descargas en curso y en cola va en los contadores de los filtros y en un resumen encima de la lista.
- **Notificaciones** (`Toast` abajo a la derecha, una línea y sin exclamaciones), generadas en el cliente al comparar snapshots:
  - `success` al completar una descarga, con la acción *Copiar ruta*.
  - `error` al fallar, con la acción *Reintentar* y `role="alert"`.
  - `info` para confirmaciones ("3 descargas añadidas").
  - Las de `info` y `success` se cierran solas a los ~5 s; las de `error` se quedan hasta que se cierran.

### 5.3 Configuración
Panel lateral (`Drawer`, desde la derecha) que se abre con el botón de ajustes de la cabecera. Guardado inmediato y aplicación en caliente vía `aria2.changeGlobalOption` (sin reiniciar aria2):

| Ajuste | Control | Opción aria2 | Defecto |
|--------|-------------|--------------|---------|
| Descargas simultáneas | `TextField` numérico (1–20) | `max-concurrent-downloads` | 3 |
| Límite de velocidad activo | `Switch` | aplica o pone a 0 los dos límites siguientes | Off |
| Límite de descarga | `TextField` numérico + `Select` KB/s / MB/s | `max-overall-download-limit` | 5 MB/s |
| Límite de subida (torrents) | Ídem | `max-overall-upload-limit` | 1 MB/s |
| Carpeta de descargas | `TextField` mono + validación en servidor | `dir` (solo afecta a descargas nuevas) | `~/Downloads/Kestry` (o `/downloads` en Docker) |
| Apagar al terminar todo | `Switch` | — (lo gestiona Kestry) | Off |
| Seeding tras completar | `Switch` + ratio | `seed-ratio` / `seed-time` | Off (ratio 0) |
| Tema | `Segmented` Claro/Oscuro/Sistema | — (se guarda en el navegador) | Sistema |

En `settings.json` el límite se guarda como valores (`DownloadLimit`, `UploadLimit`) más un booleano `SpeedLimitEnabled`, así el interruptor sabe a qué límite volver. La cabecera tiene un **interruptor rápido** (`Switch` con el icono `gauge`) que cambia ese mismo booleano.

**Carpeta de descargas**: el servidor comprueba que existe (o puede crearse) y es escribible. Para evitar escrituras arbitrarias cuando se gestiona en remoto, solo se permiten rutas dentro de una lista de raíces permitidas (`AllowedRoots`) definida en el fichero de configuración o variable de entorno, no editable desde la UI.

### 5.4 Apagado al terminar
- Activable desde Configuración y desde un `Switch` visible en la cabecera.
- Condición: activado **y** no quedan descargas activas ni en cola **y** al menos una descarga terminó durante la sesión (evita apagar nada más arrancar).
- Al cumplirse, se inicia una **cuenta atrás de 60 s** visible en todos los clientes (va en el snapshot; `ShutdownBanner`: `Banner` sobre `rufous-100` + botón *Cancelar apagado*). Si se cancela o se añade una descarga, se aborta.
- Antes de apagar: `aria2.saveSession` y parada limpia de aria2.
- Comando por SO:
  - Windows: `shutdown /s /t 0`
  - Linux: `systemctl poweroff` (requiere regla polkit o sudoers documentada en el README)
  - **Docker**: el contenedor no puede apagar el host. La opción se muestra deshabilitada con una explicación.
- Flag `AllowShutdown` en la configuración del servidor (no en la UI) para poder desactivarlo por completo en instalaciones remotas.

---

## 6. API

### REST (`/api`)
| Método | Ruta | Descripción |
|--------|------|-------------|
| `GET` | `/api/downloads` | Snapshot completo (el mismo que llega por SSE) |
| `POST` | `/api/downloads/links` | `{ text, subfolder?, paused? }` → resultado por línea (añadido/duplicado/inválido) |
| `POST` | `/api/downloads/torrents` | `multipart/form-data` con 1..N `.torrent` |
| `POST` | `/api/downloads/{gid}/pause` | Pausar |
| `POST` | `/api/downloads/{gid}/resume` | Reanudar |
| `POST` | `/api/downloads/{gid}/retry` | Reintentar (re-añade con la misma URI) |
| `DELETE` | `/api/downloads/{gid}?deleteFiles=bool` | Cancelar o eliminar |
| `POST` | `/api/downloads/pause-all` · `/resume-all` | Acciones globales (`aria2.pauseAll` / `unpauseAll`) |
| `POST` | `/api/downloads/purge` | Limpiar completadas/errores de la lista (`aria2.purgeDownloadResult`) |
| `GET` / `PUT` | `/api/settings` | Leer/guardar configuración |
| `POST` | `/api/shutdown/cancel` | Cancelar cuenta atrás |
| `GET` | `/api/system` | Versión, versión aria2, si el apagado está disponible |
| `GET` | `/health` | Healthcheck (incluye estado de aria2) |

### SSE (`GET /api/events`)
Un único evento `snapshot` cada segundo:

| Campo | Contenido |
|-------|-----------|
| `downloads` | Lista de descargas (campos mínimos) |
| `stats` | Velocidad ↓/↑ global y espacio libre en disco |
| `aria2` | `installing` / `running` / `restarting` / `down` (con el motivo si falta el binario) |
| `shutdown` | `null` o segundos restantes de la cuenta atrás |
| `settingsVersion` | Cambia al guardar ajustes; los demás clientes vuelven a pedir `/api/settings` |

La respuesta lleva `Cache-Control: no-cache` para que proxies y túneles no la retengan.

---

## 7. Integración con aria2

**Arranque** (`Aria2ProcessManager`):
```
aria2c --enable-rpc --rpc-listen-all=false --rpc-listen-port=<Aria2:Port>
       --rpc-secret=<aleatorio> --dir=<dir>
       --input-file=<data>/aria2.session --save-session=<data>/aria2.session
       --save-session-interval=30 --continue=true
       --max-concurrent-downloads=<n> --max-overall-download-limit=<x>
       --follow-torrent=true --bt-save-metadata=true --seed-ratio=0
       --listen-port=<Aria2:BtPort> --dht-listen-port=<Aria2:BtPort>
       --enable-dht=true --bt-enable-lpd=true --enable-peer-exchange=true
```
**Obtención del binario** (`Aria2Locator`). aria2 no se incluye en el repositorio ni en el zip; se obtiene al construir o lo descarga la aplicación:
- **Docker**: `apt-get install aria2` en la etapa final del `Dockerfile` (paquete de Debian, amd64 y arm64).
- **Orden de búsqueda en binario**: `Aria2:Path` en configuración → `PATH` → `<DataDir>/aria2/aria2c[.exe]` (descargado antes por Kestry).
- **Windows, si no lo encuentra**: Kestry descarga en segundo plano el zip oficial de la release de aria2 en GitHub (`aria2-<versión>-win-64bit-build1.zip`), comprueba su SHA-256 (versión y hash fijados en el código), extrae `aria2c.exe` en `<DataDir>/aria2/` y lo arranca. Mientras tanto la UI muestra "Instalando aria2…".
- **Linux, si no lo encuentra**: aria2 no publica binarios oficiales para Linux, así que no se descarga. Se queda en `down` con el mensaje "Instala aria2 (`apt install aria2`, `dnf install aria2`…)" en la UI y en el log.
- **Sin Internet o con hash incorrecto**: no se ejecuta nada; queda en `down` con el motivo y se indica cómo copiar `aria2c.exe` a mano o fijar `Aria2:Path`.
- Puerto RPC fijo y configurable (`Aria2:Port`); si está ocupado, error claro en el log.
- Vigilancia: si el proceso termina inesperadamente, se reinicia a los 5 s; el estado (`restarting` / `down`) llega a la UI en el snapshot.

**Cliente** (`Aria2Client`):
- `POST http://127.0.0.1:<port>/jsonrpc` con `HttpClient`; cada llamada es independiente (sin conexión persistente ni reconexión).
- Métodos usados: `addUri`, `addTorrent`, `pause`, `unpause`, `pauseAll`, `unpauseAll`, `forceRemove`, `removeDownloadResult`, `purgeDownloadResult`, `tellStatus`, `tellActive`, `tellWaiting`, `tellStopped`, `getGlobalStat`, `changeGlobalOption`, `saveSession`, `shutdown`, `getVersion`, `system.multicall` (para agrupar las consultas del tick).
- Para los magnets se sigue el `followedBy` del GID de metadatos para mostrar una única fila al usuario.
- Pedir solo los campos necesarios (`keys`) en `tell*` para reducir carga.

---

## 8. Acceso remoto y seguridad (sin login)

Kestry **no implementa autenticación** por decisión de diseño. La protección se delega en la capa de red:

1. **Por defecto escucha solo en `127.0.0.1:8080`**. Para exponerlo hay que cambiar `Urls` explícitamente. En Docker escucha en `http://+:8080` dentro del contenedor y el compose publica solo `127.0.0.1:8080`.
2. Opciones recomendadas (documentadas en el README con ejemplos):
   - **Cloudflare Tunnel + Cloudflare Access (Zero Trust)**: `cloudflared` como servicio en el `docker-compose.yml` (perfil opcional), y una política de Access por email/OTP o proveedor de identidad. Kestry nunca abre puertos en el router. Se publica en un subdominio propio (sin subruta).
   - **Tailscale / WireGuard**: acceso solo desde la red privada.
3. Mitigaciones aunque no haya login:
   - aria2 solo en loopback y con `rpc-secret`; nunca expuesto.
   - Comprobación de la cabecera `Origin` en las peticiones mutadoras (anti-CSRF / anti DNS-rebinding): se acepta el propio host y los de `Kestry:AllowedOrigins`.
   - Carpeta de descargas y borrado de archivos limitados a `AllowedRoots`; normalización de rutas para impedir `..`.
   - `AllowShutdown` desactivable desde el servidor.
   - Validación y límite de tamaño de las peticiones (textarea y `.torrent`).

---

## 9. Diseño de la interfaz

### 9.1 Fuente y alcance
- **Dónde está**: el original vive en https://claude.ai/artifact/UvfFmAMTTYHUEUiTDM9uQC. Hay una copia exportada en `design/kestrel/` (versión: el campo `version` de `tokens.json`); se vuelve a exportar cuando cambie el original. Manda sobre este plan en lo visual:
  - `README.md`: principios, uso de color, tipografía, espaciado y reglas de estado.
  - `tokens.json`: **fuente única** de colores (temas claro y oscuro), tipografía, espaciado, radios y sombras.
  - `components/index.d.ts` y `components/<Comp>/README.md`: props, variantes y estados de cada componente.
  - `components/bundle.css` y `components/bundle.js`: implementación de referencia. Sirven para consultar medidas exactas (alturas, paddings, tamaños de icono) y los trazos SVG de los iconos. No se cargan en la app.
- **Preview de referencia**: las pantallas acordadas están en https://claude.ai/artifact/4uDV2ivwkbSYLJs9EQrZ6D (Descargas, Añadir descargas, Estado vacío, Apagado al terminar, Configuración).
- **Por qué se reimplementa**: Kestrel incluye componentes React (`window.Kestrel` + `bundle.css`), pero no se usan tal cual: se cargan como script global (sin paquete npm/ESM), sus textos vienen fijos en inglés (`StatusBadge`, `DownloadItem`) y faltan piezas que necesita el plan (DropdownMenu, Drawer). Se reimplementan en `components/ui/` con **Tailwind CSS + tokens de Kestrel**, HTML nativo como en el original y Radix solo donde se indica en el §9.4, replicando su anatomía, variantes y estados (hover, focus, disabled, error).
- **Nota de nombres**: no confundir con *Kestrel*, el servidor web de ASP.NET Core que usa el backend; en código y documentación se habla de "design system Kestrel" para distinguirlos.

### 9.2 Tokens, tema y tipografía
- **Tokens**: `styles/tokens.css` se escribe a mano desde `design/kestrel/tokens.json` (son pocos valores y cambian poco), con variables CSS de los mismos nombres (`--surface-000`, `--ink`, `--slate-600`, `--radius-md`…) expuestas a Tailwind con `@theme inline`:
  ```css
  @import "tailwindcss";
  :root { --surface-000: #faf7f2; /* … */ }
  .dark { --surface-000: #1a1d21; /* … */ }
  @theme inline { --color-surface-000: var(--surface-000); /* colores, fuentes, radios, sombras */ }
  ```
  El espaciado de Kestrel (base 4 px: `space-1` 4 … `space-12` 48) coincide con la escala por defecto de Tailwind (`p-1` … `p-12`), así que no se redefine. Nada de estilos ad hoc: solo clases de Tailwind que apunten a esos tokens; sin colores ni tamaños arbitrarios (`bg-[#...]`, `p-[13px]`).
- **Tema**: los valores `light` van en `:root` y los `dark` bajo la clase `.dark` en `<html>`, según la preferencia Claro/Oscuro/Sistema (`prefers-color-scheme`). Como los colores son variables, las clases no cambian entre temas y no hace falta la variante `dark:`.
- **Tipografía**: Fraunces (títulos; el estilo `display` como mucho una vez por pantalla), Manrope (toda la UI) y JetBrains Mono (URLs, rutas, info-hash). A diferencia de Kestrel, que las carga desde Google Fonts, se **autoalojan** con `@fontsource` dentro del build: Kestry corre en servidores domésticos que pueden no tener salida a Internet.

### 9.3 Reglas de Kestrel que aplican siempre
Resumen; el README de Kestrel es la referencia:
- Fondos tranquilos (`surface-000` / `surface-100`); `slate-600` es la acción primaria y `rufous-*` el acento. Nunca dos botones primario y acento con el mismo peso juntos.
- Separación con líneas (`line`), no con sombras; `shadow-soft` solo en lo que flota (diálogos, drawer, toasts, menús).
- `cere` (naranja de señal) solo para puntos o insignias de "nuevo"/atención, **nunca texto**, y como mucho uno visible a la vez.
- Textos con mayúscula solo al inicio ("Guardar cambios"), sin exclamaciones; decir qué pasó ("Descarga añadida"), no cómo se siente el sistema.
- Foco: anillo de 2 px en `focus` con 2 px de separación en todo elemento interactivo. Deshabilitado: 40 % de opacidad, sin hover. Error: borde `danger` + mensaje, nunca solo color.
- Radios: `radius-sm` en campos, `radius-md` en botones y tarjetas, `radius-lg` en diálogos y drawer, `radius-full` en insignias.

### 9.4 Componentes
| Concepto del plan | Componente Kestrel | Implementación |
|-------------------|--------------------|----------------|
| Botón | `Button` (primary · accent · secondary · quiet · danger; sm · md) | `<button>` + Tailwind |
| Botón de icono | `IconButton` (con `aria-label` obligatorio) | `<button>` + Tailwind |
| Icono | `Icon` (ver §9.6) | SVG en línea |
| Campo de texto / número | `TextField` (variante `mono` para URLs y rutas) | `<input>` + `<label>` |
| Área de texto | — (estilo de `TextField`) | `<textarea>` |
| Selector | `Select` | `<select>` nativo |
| Casilla | `Checkbox` | `<input type="checkbox">` |
| Interruptor | `Switch` | Radix `Switch` |
| Etiqueta de estado | `StatusBadge` | HTML |
| Progreso | `ProgressBar` | `<div role="progressbar">` (sin `aria-valuenow` si es indeterminado) |
| Fila de descarga | `DownloadItem` | HTML |
| Filtro | `NavItem` (con contador) | `<button aria-current>` |
| Barra de acciones | `Toolbar` | `<div role="toolbar">` |
| Totales | `SpeedStat` | HTML |
| Modal | `Dialog` | Radix `Dialog` |
| Notificación | `Toast` (info · success · error) | Lista en una región `aria-live` + temporizador |
| Estado vacío | `EmptyState` | HTML |
| Panel lateral | — `Drawer` | Radix `Dialog` anclado a la derecha |
| Menú de acciones | — `DropdownMenu` | Radix `DropdownMenu` |
| Selector segmentado | — `Segmented` | Grupo de `<input type="radio">` |
| Opciones plegables | — | `<details>` / `<summary>` |
| Zona de arrastre | — `FileDrop` | `<input type="file">` + eventos de drop |
| Aviso | — `Banner` | HTML (`rufous-100` para avisos de acción, `slate-100` para info) |

"—" = Kestrel no lo tiene: se diseña solo con sus tokens y las reglas del §9.3.

### 9.5 Estados de descarga
Kestrel define cinco estados compartidos por `StatusBadge` y `ProgressBar`; cada uno lleva siempre su palabra, nunca solo color:

| Estado aria2 | Estado Kestrel | Texto | Color |
|--------------|----------------|-------|-------|
| `active` | downloading | Descargando | slate |
| `waiting` | queued | En cola | neutro |
| `paused` | paused | Pausada | rufous |
| `complete` | completed | Completada | moss |
| `error` | failed | Error | danger |

Un magnet sin metadatos se muestra como *downloading* con `ProgressBar` indeterminada y el texto "Obteniendo metadatos…". Las descargas `removed` no se muestran.

### 9.6 Iconografía
- Se portan los 23 iconos de Kestrel (`Kestrel.iconNames`: download, pause, play, x, plus, check, retry, folder, search, alert, info, file, video, archive, music, image, trash, link, clock, inbox, sliders, chevronDown, gauge).
- Se añaden con el mismo estilo (trazo 1.75, cuadrícula de 24 px, extremos redondeados): `upload` (zona `.torrent`) y `magnet`.
- 20 px en UI, en `ink` o `ink-muted`; `slate-600` solo para el estado activo.

### 9.7 Layout y pantallas
- **Cabecera**: logotipo (de momento un hueco reservado de 36 px hasta tener el logo), nombre "Kestry", `Switch` de límite de velocidad, `Switch` de apagar al terminar y `IconButton` de ajustes.
- **Barra lateral** (240 px, `surface-100`): filtros `NavItem`, `SpeedStat` (↓, ↑, espacio libre) y, al final, el indicador de conexión.
- **Contenido** (`surface-000`): `Toolbar`, banner de apagado, resumen y lista de `DownloadItem`; `Toast` abajo a la derecha.
- **Vistas**: solo Descargas. Configuración es un `Drawer` de 480 px desde la derecha, no una página propia.
- **Estado vacío**: `EmptyState` + botón grande *"Pega tus enlaces aquí"* que abre el modal.
- **Indicador de conexión** (`StatusBadge` al final de la barra lateral; versión desde `/api/system`, estado desde el snapshot):
  - Conectado: *"aria2 {version} conectado"*, `moss`.
  - Instalando: *"Instalando aria2…"*, `slate` (solo Windows, primera ejecución).
  - Reconectando: *"Reconectando con aria2…"*, `rufous`.
  - Caído: *"aria2 no responde"*, `danger`.
- **Responsive**: Kestrel no define breakpoints; se usan los de Tailwind por defecto. Por debajo de `lg` (1024 px) la barra lateral pasa arriba y los filtros quedan en una fila con scroll horizontal. Por debajo de `sm` (640 px), las acciones de cada fila van en un `DropdownMenu` y el drawer ocupa todo el ancho.

### 9.8 Idioma y accesibilidad
- **Idioma**: español. Ningún texto visible sale del design system: todas las etiquetas, incluidas las de estado, están en `strings.ts`, listo para añadir otro idioma más adelante.
- **Accesibilidad**: foco, teclado y ARIA de diálogos, drawer y menús vienen de Radix; el resto usa elementos nativos. `aria-label` en todos los botones de icono. Los pares de color de Kestrel cumplen 4.5:1 en ambos temas; `rufous-500`, `sand` y `cere` nunca se usan para texto.

---

## 10. Configuración del servidor (`appsettings.json` / variables de entorno)

```jsonc
{
  "Urls": "http://127.0.0.1:8080",      // en Docker: http://+:8080 (ver §11)
  "Kestry": {
    "DataDir": "./data",                 // settings.json, aria2.session, logs
    "AllowedRoots": ["/downloads"],     // raíces permitidas para la carpeta de descargas
    "AllowShutdown": true,
    "AllowedOrigins": []                 // orígenes extra permitidos (dominio del túnel)
  },
  "Aria2": {
    "Path": null,                        // null = PATH → <DataDir>/aria2 → descarga (solo Windows)
    "Port": 6800,                        // RPC, solo loopback
    "BtPort": 6881,                      // BitTorrent y DHT (TCP/UDP)
    "ExtraArgs": []
  }
}
```
Todas las claves se pueden sobrescribir con variables `KESTRY__ALLOWEDROOTS__0=/downloads`, etc.

---

## 11. Despliegue

- **Docker** (recomendado):
  - Imagen multi-stage: `node` (build frontend) → `mcr.microsoft.com/dotnet/sdk` (publish) → `mcr.microsoft.com/dotnet/aspnet` + `aria2` instalado. Las etapas de build usan `--platform=$BUILDPLATFORM` y `dotnet publish -a $TARGETARCH` para no compilar bajo emulación.
  - Multi-arquitectura `linux/amd64` y `linux/arm64` (Raspberry Pi / NAS).
  - `ASPNETCORE_URLS=http://+:8080` en la imagen; el compose publica `127.0.0.1:8080:8080`.
  - Volúmenes: `/downloads` y `/data`. Corre con el usuario no root `app` de la imagen de .NET; se puede cambiar con `user:` en el compose para que coincida con el dueño de los volúmenes.
  - `docker-compose.yml` con servicio `kestry`, servicio opcional `cloudflared` (perfil `tunnel`) y el puerto BitTorrent `6881` TCP/UDP mapeado.
- **Binario self-contained**: `dotnet publish -c Release -r win-x64|linux-x64|linux-arm64 --self-contained -p:PublishSingleFile=true`. Se distribuye un zip con el ejecutable y la carpeta `wwwroot/` (los estáticos no van dentro del ejecutable). Ningún zip incluye aria2: en Windows lo descarga Kestry en el primer arranque y en Linux se instala con el gestor de paquetes (§7).
- **Servicio del sistema**: unidad `systemd` de ejemplo y soporte `UseWindowsService()` para correr como servicio de Windows.

---

## 12. Pruebas

| Tipo | Qué se prueba | Herramienta |
|------|---------------|-------------|
| Unitarias | `LinkParser` (tipos, duplicados, inválidos, separadores), `SettingsService` (validación, rutas fuera de `AllowedRoots`), lógica de `ShutdownService` (condiciones y cancelación, con reloj falso) | xUnit + `TimeProvider` falso |
| Unitarias | `Aria2Locator`: orden de búsqueda y rechazo de un zip con SHA-256 distinto (sin red, con un zip falso) | xUnit |
| Humo | Arranque de aria2 real, añadir una URL HTTP servida localmente y verla completada | xUnit (mismo proyecto; se salta si no hay `aria2c`; en CI se instala con `apt`) |
| CI | Build, tests y build de la imagen Docker en cada PR | GitHub Actions |

El comando real de apagado nunca se ejecuta en tests: `ShutdownService` depende de una interfaz `ISystemPower` que se sustituye por un doble.

---

## 13. Fases de desarrollo

### Fase 0 — Esqueleto (1–2 días)
- [ ] Solución .NET + proyecto Vite/React con Tailwind CSS; el build del frontend se copia a `wwwroot`.
- [ ] Escribir `styles/tokens.css` (variables + `@theme inline`) desde `tokens.json` de Kestrel y configurar el tema claro/oscuro.
- [ ] Autoalojar las fuentes (Fraunces, Manrope, JetBrains Mono) con `@fontsource`.
- [ ] Portar los iconos de Kestrel y añadir los nuevos (§9.6).
- [ ] Implementar en `components/ui/` los componentes de la tabla del §9.4.
- [ ] `Program.cs` sirviendo estáticos + `/health`.
- [ ] CI básica.

### Fase 1 — Núcleo aria2 (2–3 días)
- [ ] `Aria2Locator`: búsqueda del binario y descarga verificada en Windows.
- [ ] `Aria2ProcessManager`: lanzar, vigilar y parar aria2c; sesión persistente.
- [ ] `Aria2Client`: JSON-RPC sobre HTTP.
- [ ] `DownloadService` + endpoints REST de descargas (incluido el borrado de archivos dentro de `AllowedRoots`).
- [ ] Prueba de humo con aria2 real.

### Fase 2 — Panel en tiempo real (2–3 días)
- [ ] `DownloadMonitor` + endpoint SSE `/api/events`.
- [ ] `DownloadsList` (filas `DownloadItem`) con acciones por fila y toasts derivados de los snapshots.
- [ ] `Sidebar`: filtros `NavItem`, `SpeedStat` e indicador de conexión con aria2.

### Fase 3 — Importación (2 días)
- [ ] `LinkParser` con tests.
- [ ] `AddDownloadsModal` con textarea, resumen en vivo y subida de `.torrent`.
- [ ] Pegado global con `Ctrl+V`.

### Fase 4 — Configuración y apagado (2–3 días)
- [ ] `SettingsService` con aplicación en caliente a aria2.
- [ ] `SettingsDrawer` y conmutador rápido de velocidad.
- [ ] `ShutdownService` (Windows/Linux) con cuenta atrás y cancelación.

### Fase 5 — Remoto y seguridad (1 día)
- [ ] Validación de `Origin`, `AllowedRoots`, límites de tamaño de petición.
- [ ] Guía de Cloudflare Tunnel + Access y de Tailscale en el README; comprobar que el SSE llega sin retención a través del túnel.

### Fase 6 — Empaquetado y pulido (2 días)
- [ ] Dockerfile multi-arch + `docker-compose.yml` con perfil `cloudflared`.
- [ ] Binarios self-contained, unidad systemd, servicio Windows.
- [ ] Responsive móvil y tema oscuro.

**Estimación total: ~2,5–3 semanas** de trabajo de una persona.

---

## 14. Riesgos y mitigaciones

| Riesgo | Mitigación |
|--------|-----------|
| Exposición accidental a Internet sin protección | Escucha en loopback por defecto (también el puerto publicado en Docker), documentación clara del túnel/VPN |
| Apagado no disponible por permisos o en Docker | Detección en `/api/system`, opción deshabilitada con explicación |
| Magnets que nunca obtienen metadatos | Mostrar estado "Obteniendo metadatos" y permitir cancelar; DHT habilitado |
| aria2 no está instalado o no se puede descargar | Estado `down` con el motivo y las instrucciones en la UI; `Aria2:Path` para indicarlo a mano |
| La release de aria2 en GitHub cambia o desaparece | Versión y SHA-256 fijados en el código; se actualizan a mano en una nueva versión de Kestry |
| aria2 se cae o se corrompe la sesión | Reinicio automático; copia de seguridad del fichero de sesión antes de cada arranque |
| Muchas descargas → snapshot pesado | `keys` mínimos en aria2 y `system.multicall`; si hace falta, envío de diferencias (post v1) |
| Un proxy o túnel retiene el SSE | `Cache-Control: no-cache`; probarlo con Cloudflare Tunnel en la Fase 5 |
| Puertos BitTorrent bloqueados (NAT) | Puerto único configurable (`BtPort`), documentar su mapeo; mostrar nº de peers para diagnosticar |

---

## 15. Mejoras futuras (post v1)
- Reordenar la cola (`aria2.changePosition`).
- Selección múltiple con acciones masivas.
- Panel de detalle por descarga (archivos, peers, info-hash, error de aria2).
- Interfaz en inglés.
- Hook de apagado para Docker (`ShutdownCommand` → script o webhook del host).
- Aviso en la UI si Kestry detecta que escucha en una interfaz pública sin proxy delante.
- Soporte de `PathBase` (publicar en una subruta) y `ForwardedHeaders`.
- Enlaces `sftp://`.
- Programador de horarios de velocidad (p. ej. sin límite de noche).
- Categorías con carpetas de destino automáticas.
- Extensión de navegador / bookmarklet para enviar enlaces a Kestry.
- Integración con RSS de torrents.
- Webhooks / notificaciones push (Telegram, ntfy) al completar.
- Modo PWA instalable.
