# Threat model FIX-001: Marcadores de diálogo con - ' Ç se tratan como acción

| Field | Value |
|-------|-------|
| Ticket | FIX-001 |
| Fix-plan | docs/daw/specs/fix-FIX-001.md |
| RCA | docs/daw/specs/rca-FIX-001.md |
| PRD | docs/daw/prd/PRD.md (RF-01i, RF-05a, Anexo A) |
| Date | 2026-11-11 |
| Método | STRIDE por componente modificado |

## Contexto

App web local y monousuario: el contexto y el riesgo aceptado AR-01 (sin autenticación) de
`threat-FEAT-001a.md` siguen vigentes y no cambian. Este FIX no agrega endpoints, dependencias,
entidades ni migraciones: amplía el conjunto de caracteres del marcador de diálogo, agrega una
validación de caracteres al nombre de una hoja nueva y una equivalencia `'`/`’` solo para comparar.

## Componentes analizados

| # | Componente | Cambio |
|---|---|---|
| C1 | `Guiones/NombrePersonaje` (nuevo) | Fuente única de los caracteres permitidos: fragmento del marcador `\p{Lu}[\p{Lu}0-9 '’.\-]*`, regex del nombre de hoja `^\p{L}[\p{L}0-9 '’.\-]*$` y `ClaveDeMarcador` (NFC + `’`→`'`, solo para comparar) |
| C2 | `Guiones/GuionParser` (`MarcadorDialogo`) | El marcador usa el fragmento de C1 |
| C3 | `Guiones/DetectorPersonajes` | Compara marcador y hoja con `ClaveDeMarcador`; el patrón de la acción no cambia |
| C4 | `Servicios/ServicioPersonajes.CrearAsync` | Rechaza nombres fuera de la regla (después del chequeo NFC); `ClaveDeNombre` pliega `’`→`'` en el chequeo de duplicados |

## Trust boundaries

| # | Frontera | Cruce en este FIX |
|---|---|---|
| TB-1 | Navegador → servidor Blazor (SignalR en `localhost`) | El nombre de la hoja (formulario de `DetalleSerie.razor`) y el texto del guion (`CargarGuion.razor`) llegan sin confianza a C4 y a C2 |
| TB-2 | Servidor → SQLite | El nombre de la hoja y las líneas de diálogo se guardan tal como llegaron (sin plegar apóstrofos); sin cambios en el acceso |

No hay frontera con servicios externos: Gemini no participa en este FIX.

## Clasificación de datos (F-TM-05)

| Dato | Clasificación | Justificación |
|---|---|---|
| Nombre de hoja de personaje, líneas de diálogo del guion | **Interno — contenido creativo**, no PII | Personajes de ficción |
| Credenciales | **Ninguna** | El FIX no toca la API key ni agrega login |

Sin PII ni credenciales: F-TM-07 no aplica.

## STRIDE por componente

### C1 — `NombrePersonaje`

| STRIDE | Análisis | Riesgo | Mitigación |
|---|---|---|---|
| S | N/A (código puro, sin identidad). | — | — |
| T | `ClaveDeMarcador` podría usarse por error para guardar texto y alterar el guion. | 🟢 R-03 | M-03 |
| R | N/A. | — | — |
| I | N/A: sin E/S. | — | — |
| D | Las regex nuevas evalúan texto del usuario (hasta 200 000 caracteres por guion). | 🟡 R-01 | M-01 |
| E | N/A. | — | — |

### C2 — `GuionParser`

| STRIDE | Análisis | Riesgo | Mitigación |
|---|---|---|---|
| S | Un diálogo de un personaje sin hoja con nombre parecido al de una hoja (`O’BRIEN` / `O'BRIEN`). | 🟢 R-02 | M-02 |
| T | El conjunto más amplio clasifica como diálogo líneas que antes eran acción (`OK. (pausa)`); el guion se rechaza completo, nunca se corrige. | 🟢 R-05 | M-05 |
| R | N/A (sin cambios en logging: M-04 de FEAT-001a sigue). | — | — |
| I | N/A. | — | — |
| D | ReDoS en `MarcadorDialogo` con una línea patológica. | 🟡 R-01 | M-01 |
| E | N/A. | — | — |

### C3 — `DetectorPersonajes`

| STRIDE | Análisis | Riesgo | Mitigación |
|---|---|---|---|
| S | Dos hojas que solo difieren en `'`/`’` reclamarían el mismo diálogo. | 🟢 R-02 | M-02 |
| T | La equivalencia `'`/`’` podría filtrarse al texto guardado y romper RN-04. | 🟢 R-03 | M-03 |
| R | N/A. | — | — |
| I | N/A. | — | — |
| D | El patrón de la acción ahora incluye `'`, `’`, `.` y `-` del nombre de la hoja. | 🟢 R-01 | M-01 (`Regex.Escape` y nombre ≤ 60 ya existentes) |
| E | N/A. | — | — |

### C4 — `ServicioPersonajes.CrearAsync`

| STRIDE | Análisis | Riesgo | Mitigación |
|---|---|---|---|
| S | Nombres con letras de otro alfabeto visualmente iguales (`А` cirílica / `A` latina) crean hojas que parecen duplicadas. | 🟢 R-06 | M-06 |
| T | Un nombre con `'` llega a la base. | 🟢 R-04 | M-04 |
| R | Un rechazo por caracteres no se registra en el log. | 🟢 — | Es un error de validación visible para la usuaria en el campo Nombre, como los demás de `CrearAsync`; no requiere log |
| I | Un nombre con `'`, `<` u otros caracteres se muestra en la UI. | 🟢 R-04 | M-04 |
| D | La regex del nombre de hoja sobre un nombre largo. | 🟢 R-01 | M-01 (el largo ≤ 60 se valida antes) |
| E | N/A. | — | — |

### Dependencias (W-TM-01)

No se agregan ni actualizan paquetes NuGet. M-07 de FEAT-001a sigue vigente en el SAST.

## Riesgos

| ID | Riesgo | STRIDE | Prob. | Impacto | Tratamiento |
|---|---|---|---|---|---|
| R-01 | ReDoS en las regex nuevas del marcador o del nombre | D | Baja | Medio | M-01 |
| R-02 | Diálogo asignado a dos hojas que solo difieren en `'`/`’` | S/T | Baja | Bajo | M-02 |
| R-03 | La equivalencia de apóstrofos modifica el texto guardado (RN-04) | T | Baja | Medio | M-03 |
| R-04 | Inyección (SQL/XSS) por los caracteres nuevos permitidos en el nombre | T/I | Baja | Bajo | M-04 |
| R-05 | Guiones que antes se aceptaban ahora se rechazan | T | Media | Bajo | M-05 |
| R-06 | Hojas con homógrafos de otro alfabeto | S | Baja | Bajo | M-06 |

Riesgos: C:0 H:0 M:1 L:5.

## Mitigaciones a incorporar en el fix-plan

| ID | Mitigación |
|---|---|
| M-01 | Regex del marcador y del nombre de hoja con `RegexOptions.CultureInvariant \| RegexOptions.NonBacktracking`, sin cuantificadores anidados. Mantener el test de rendimiento `GuionParserTests.cs:770-800` y agregar una línea patológica de 200 000 caracteres con los caracteres nuevos (`A-'’.` repetidos sin `:` ni `(`), que debe procesarse en menos de 1 segundo |
| M-02 | `ClaveDeNombre` pliega `’`→`'` además de NFC y mayúsculas es-ES: "O'Brien" y "O’Brien" son duplicados en la misma serie. Test en `ServicioPersonajesTests` |
| M-03 | `ClaveDeMarcador` se usa solo en comparaciones (sets/diccionarios del detector y clave de duplicados); nunca se asigna a una entidad ni a `LineaDialogoParseada`. Test AC-05k: la línea guardada conserva `’` |
| M-04 | Sin cambios en acceso a datos ni UI: solo LINQ (M-03 de FEAT-001a) y texto con `@`, sin `MarkupString` (M-02 de FEAT-001a). La regla nueva además excluye `<`, `>`, `"`, `&` del nombre |
| M-05 | Comportamiento exigido por el Anexo A (detección permisiva, rechazo completo con línea y motivo). Test de regresión con `OK. (pausa)` documentado en el fix-plan |
| M-06 | Se mantiene el chequeo de duplicados por clave; un homógrafo no reclama diálogos ajenos (el detector compara exacto en NFC), en el peor caso no se detecta, falla de forma segura y visible en la lista de personajes del clip |

No hay riesgos aceptados nuevos. AR-01 de `threat-FEAT-001a.md` no cambia.
