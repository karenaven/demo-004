# Threat model FEAT-001a: Guion sin IA — series, hojas, episodios, carga y validación

| Field | Value |
|-------|-------|
| Ticket | FEAT-001a |
| Spec | docs/daw/specs/spec-FEAT-001a.md |
| PRD | docs/daw/prd/prd-FEAT-001a.md |
| Date | 2026-10-27 |
| Método | STRIDE por componente |

## Contexto

App web local y monousuario (PRD base, «Fuera de alcance»: sin login, roles ni separación de datos).
FEAT-001a no llama a ningún servicio externo: no hay Gemini ni API key en uso en este ticket.

## Componentes analizados

| # | Componente (spec) | Qué hace |
|---|---|---|
| C1 | Páginas Blazor `Series.razor`, `DetalleSerie.razor`, `CargarGuion.razor` (Block 6) sobre el circuito SignalR de Interactive Server (Block 1) | Reciben texto del navegador y lo muestran |
| C2 | Servicios `ServicioSeries`, `ServicioPersonajes`, `ServicioEpisodios`, `ServicioGuiones` (Block 5) | Validan entrada y persisten |
| C3 | `GuionParser` y `DetectorPersonajes` (Blocks 3 y 4) | Procesan hasta 200 000 caracteres de texto del usuario con regex |
| C4 | `RodajeDbContext` + archivo SQLite `rodajeia.db` (Block 2) | Almacenamiento local |
| C5 | Dependencias NuGet nuevas (Block 1) | EF Core Sqlite/Design, xUnit v3, bUnit, `dotnet-ef` |

## Trust boundaries

| # | Frontera | Cruce |
|---|---|---|
| TB-1 | Navegador → servidor Blazor (SignalR/WebSocket sobre HTTP en `localhost`) | Todo el texto que escribe o pega la productora (guion, hojas, nombres) |
| TB-2 | Servidor → archivo SQLite en disco | Lectura/escritura de todas las entidades |
| TB-3 | Registro NuGet (nuget.org) → build | Código de terceros que corre en build, tests y runtime |

## Clasificación de datos (F-TM-05)

| Dato | Clasificación | Justificación |
|---|---|---|
| Guiones, hojas de personaje (nombre, edad, descripción física, vestuario…), series, episodios | **Interno — contenido creativo**, no PII | Describen personajes de ficción, no personas reales |
| Credenciales | **Ninguna en este ticket** | No hay login; la API key de Gemini entra en FEAT-001b (user-secrets) |

No hay PII ni credenciales → F-TM-07 (cifrado de PII/credenciales) no aplica. En tránsito el tráfico
no sale de `localhost`; en reposo el archivo queda bajo los permisos del usuario del sistema.

## STRIDE por componente

### C1 — Páginas Blazor / circuito SignalR

| STRIDE | Análisis | Riesgo | Mitigación |
|---|---|---|---|
| S | Sin autenticación: cualquiera que alcance el puerto actúa como «la productora». | 🟡 R-01 | Kestrel escucha solo en `localhost` (M-01); sin login = riesgo aceptado (AR-01) |
| T | El usuario manipula el texto o los `serieId`/`episodioId` enviados por el circuito. | 🟡 R-02 | El servicio revalida todo: episodio pertenece a la serie (Block 5, error 8), longitudes y obligatorios |
| R | Sin registro de quién cargó qué guion. | 🟢 R-03 | Monousuario. Log `Information` por carga de guion: episodio, resultado y cantidad de errores, sin el contenido (M-04) |
| I | XSS: el guion o una hoja con `<script>` se renderiza. Errores detallados del servidor expuestos al navegador. | 🟡 R-04 | Blazor codifica la salida por defecto; **prohibido `MarkupString`** con contenido del usuario (M-02). Fuera de Development, `UseExceptionHandler` y `DetailedErrors` del circuito desactivado (M-05); el error 11 del Block 5 muestra un mensaje genérico y loguea el detalle |
| D | `oninput` envía el guion completo en cada pulsación; un pegado enorme o mensajes de más de 1 MB. | 🟢 R-05 | `MaximumReceiveMessageSize` = 1 MB corta el exceso; `maxlength` 200 000 en el textarea y límite autoritativo en `ServicioGuiones` |
| E | No hay roles ni privilegios que escalar. | — | N/A |

### C2 — Servicios

| STRIDE | Análisis | Riesgo | Mitigación |
|---|---|---|---|
| S | Sin identidad (ver AR-01). | — | AR-01 |
| T | Inyección SQL a través de nombres u hojas. | 🟢 R-06 | Solo LINQ de EF Core (parametrizado); **sin `FromSqlRaw`/`ExecuteSqlRaw` con texto del usuario** (M-03) |
| T | Carga parcial de guion que deja datos inconsistentes. | 🟢 R-07 | Validación completa antes de la transacción; transacción única con rollback (Block 5, FR-13, error 11) |
| R | Ver R-03. | — | M-04 |
| I | Mensajes de error que filtran detalles de EF/SQLite. | 🟢 R-08 | Error 11 genérico hacia la UI; detalle solo en `ILogger` (M-05) |
| D | Doble clic que dispara dos cargas en paralelo. | 🟢 R-09 | El botón se deshabilita mientras se procesa (Block 6); contexto por operación |
| E | N/A. | — | — |

### C3 — `GuionParser` y `DetectorPersonajes`

| STRIDE | Análisis | Riesgo | Mitigación |
|---|---|---|---|
| S | N/A (código puro). | — | — |
| T | El texto podría hacer que el parser «corrija» o acepte un guion mal formado. | 🟢 R-10 | Detección permisiva y validación estricta; un guion inválido no produce `GuionParseado` (Block 3) |
| R | N/A. | — | — |
| I | N/A: no hay E/S. | — | — |
| D | **ReDoS**: la regex del encabezado `(.+)\s+[—-]\s+(.+)` hace backtracking polinómico sobre una línea larga con muchos separadores (hasta 200 000 caracteres). Un nombre de hoja muy largo en la regex del detector. | 🟡 R-11 | Separar LUGAR y MOMENTO con búsqueda lineal (`IndexOf` del primer separador y `LastIndexOf` del último), no con un grupo codicioso (M-06); las demás regex sin cuantificadores anidados; `Regex.Escape` + longitud de nombre ≤ 60 (Block 5). Test de rendimiento con una línea patológica de 200 000 caracteres (M-06) |
| E | N/A. | — | — |

### C4 — SQLite (`rodajeia.db`)

| STRIDE | Análisis | Riesgo | Mitigación |
|---|---|---|---|
| S | N/A. | — | — |
| T | Otro proceso o usuario del equipo modifica el archivo. | 🟢 R-12 | Permisos del sistema operativo del único usuario; `*.db*` en `.gitignore` para no versionar datos (Block 1) |
| R | N/A (monousuario). | — | — |
| I | El archivo se sube al repositorio por error. | 🟢 R-13 | `.gitignore` con `*.db`, `*.db-shm`, `*.db-wal` (Block 1) |
| D | Archivo corrupto o bloqueado. | 🟢 R-14 | Contexto por operación (sin conexiones largas); rollback documentado en el spec |
| E | N/A. | — | — |

### C5 — Dependencias NuGet (W-TM-01)

| STRIDE | Análisis | Riesgo | Mitigación |
|---|---|---|---|
| T | Paquete malicioso o typosquatting. | 🟡 R-15 | Solo los paquetes nombrados en el spec, desde nuget.org, de los propietarios oficiales (Microsoft, xunit, bUnit); versiones exactas fijadas, sin preview (M-07) |
| I/E | Vulnerabilidad conocida en una dependencia. | 🟡 R-16 | `dotnet list package --vulnerable --include-transitive` sin hallazgos como parte del SAST de CODE (M-07) |
| S/R/D | N/A. | — | — |

## Riesgos

| ID | Riesgo | STRIDE | Prob. | Impacto | Tratamiento |
|---|---|---|---|---|---|
| R-01 | Acceso sin autenticación si el puerto queda expuesto en la red | S | Baja | Medio | M-01 + **AR-01** |
| R-02 | Ids manipulados en el circuito | T | Baja | Bajo | Revalidación en servicio |
| R-04 | XSS por contenido del guion u hojas | I | Baja | Medio | M-02, M-05 |
| R-11 | ReDoS en el parser | D | Media | Medio | M-06 |
| R-15/16 | Dependencias comprometidas o vulnerables | T/I | Baja | Medio | M-07 |
| R-03, 05–10, 12–14 | Ver tablas | R/D/T/I | Baja | Bajo | Mitigados como se indica |

Riesgos: C:0 H:0 M:5 L:9.

## Mitigaciones a incorporar en el spec

| ID | Mitigación | Bloque |
|---|---|---|
| M-01 | `launchSettings.json` solo con `http://localhost:*`/`https://localhost:*`; no se configura `0.0.0.0` ni `*` | Block 1 |
| M-02 | Prohibido `MarkupString`/`(MarkupString)` con contenido del usuario; los componentes muestran texto solo con `@` | Block 6 |
| M-03 | Prohibido `FromSqlRaw`/`ExecuteSqlRaw`; solo LINQ | Block 5 |
| M-04 | `ILogger` `Information` por carga de guion (episodio, ok/errores, cantidad) sin el contenido del guion | Block 5 |
| M-05 | Fuera de Development: `UseExceptionHandler` + `UseHsts` de la plantilla y `DetailedErrors` del circuito desactivado | Block 1 |
| M-06 | LUGAR/MOMENTO con `IndexOf`/`LastIndexOf`; test `Rendimiento_LineaPatologica200000_MenorA1Segundo` | Block 3 |
| M-07 | Versiones exactas de paquetes oficiales; `dotnet list package --vulnerable --include-transitive` en el SAST | Block 1 / CODE |

## Riesgo aceptado

| ID | AR-01 — Sin autenticación ni autorización |
|---|---|
| Riesgo | Cualquier persona que alcance el puerto de la app puede leer y modificar todos los datos |
| Quién lo acepta | Karen (dueña del producto), al aprobar el PRD base y FEAT-001a, que declaran la v1 monousuario y local sin login |
| Justificación | La v1 corre solo en el equipo de la productora y M-01 limita la escucha a `localhost`; no hay PII ni credenciales en los datos de este ticket |
| Condiciones de revisión | Se revisa (y deja de aceptarse) si la app se despliega en un servidor o red compartida, si se agrega multiusuario, o si se empiezan a guardar datos personales o credenciales en la base |
