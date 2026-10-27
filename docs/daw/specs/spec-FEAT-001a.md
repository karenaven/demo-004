# Spec FEAT-001a: Guion sin IA — series, hojas, episodios, carga y validación

| Field | Value |
|-------|-------|
| Ticket | FEAT-001a |
| PRD | docs/daw/prd/prd-FEAT-001a.md |
| Tier | FEATURE |
| Date | 2026-10-27 |
| Spec loops | 2 |
| Threat model | docs/daw/security/threat-FEAT-001a.md (M-01..M-07 incorporadas) |

## Summary

Se crea la solución .NET 10 (Blazor Web App, Interactive Server) con SQLite + EF Core y un proyecto
de tests xUnit v3. El núcleo es un **parser determinístico de una sola pasada** (`GuionParser`) que
valida el guion contra el Anexo A del PRD base acumulando **todos** los errores con línea y motivo,
y que solo produce un guion parseado si no hay ninguno; luego `DetectorPersonajes` marca, sin IA,
los personajes con hoja presentes por clip y por escena. `ServicioGuiones` orquesta parseo,
detección y guardado en una transacción (sobrescribiendo el guion anterior) y deja las escenas en
"pendiente". La UI (tres páginas) solo habla con servicios de `Servicios/`. El campo del guion usa
`oninput` (corrección 4 del repositorio anterior).

## Coverage: PRD → blocks

| Requirement | Covered by |
|---|---|
| FR-01 | Block 2 (entidad `Serie`), Block 5 (`ServicioSeries.CrearAsync`), Block 6 (`Series.razor`) |
| FR-02 | Block 5 (`ServicioSeries.ListarAsync`), Block 6 (`Series.razor`, `CargarGuion.razor`) |
| FR-03 | Block 2 (entidad `Personaje`), Block 5 (`ServicioPersonajes.CrearAsync`), Block 6 (`DetalleSerie.razor`) |
| FR-04 | Block 5 (`ServicioPersonajes.ListarAsync`), Block 6 (`DetalleSerie.razor`) |
| FR-05 | Block 2 (entidad `Episodio`, índice único), Block 5 (`ServicioEpisodios.CrearAsync`), Block 6 (`DetalleSerie.razor`) |
| FR-06 | Block 5 (`ServicioEpisodios.ListarAsync`), Block 6 (`DetalleSerie.razor`, `CargarGuion.razor`) |
| FR-07 | Block 5 (`ServicioGuiones.CargarAsync`), Block 6 (`CargarGuion.razor`) |
| FR-08 | Block 1 (límite de mensaje SignalR), Block 6 (`@bind:event="oninput"`) |
| FR-09 | Block 2 (cascadas), Block 5 (sobrescritura en transacción) |
| FR-10 | Block 3 (`GuionParser`: estructura) |
| FR-11 | Block 3 (`GuionParser`: campos y líneas de clip) |
| FR-12 | Block 3 (`ErrorGuion` con línea y motivo), Block 5 (traducción a `Resultado<T>`), Block 6 (lista de errores) |
| FR-13 | Block 3 (sin guion parseado si hay errores), Block 5 (no se abre transacción) |
| FR-14 | Block 3 (`GuionParseado`) |
| FR-15 | Block 4 (`DetectorPersonajes`, por clip), Block 5 (persistencia `ClipPersonaje`) |
| FR-16 | Block 4 (`DetectorPersonajes`, por escena), Block 5 (persistencia `EscenaPersonaje`) |
| FR-17 | Block 2 (`EstadoGeneracion`, default `Pendiente`), Block 5 (escenas nuevas en `Pendiente`) |
| NFR-01 | Strategy: parser de una pasada O(líneas) con regex compiladas estáticas, detector con una regex compilada por personaje y escena; test de rendimiento del Block 3 (100 corridas de 10 × 20, p95 < 1 s) |
| NFR-02 | Strategy: `GuionParser` y `DetectorPersonajes` son clases sin dependencias inyectadas ni E/S, en `Guiones/`; el proyecto no tiene cliente de Gemini en este ticket y `Guiones/` no referencia `Generacion/` (verificado por el test de arquitectura del Block 4) |

## Dependencies between blocks

- Block 1 → base de todo (solución, proyectos, `Resultado<T>`).
- Block 2 depende de 1.
- Block 3 depende de 1 (no usa la base de datos).
- Block 4 depende de 3 (consume `GuionParseado`).
- Block 5 depende de 2, 3 y 4.
- Block 6 depende de 5.

Orden de ejecución: 1 → 2 → 3 → 4 → 5 → 6 (2 y 3 podrían ir en paralelo; se ejecutan en serie).

## Decisiones de diseño

- **Capas.** `Components/` → `Servicios/` → (`Guiones/`, `Datos/`). `Guiones/` no depende de
  `Servicios/` ni de `Datos/`: el parser devuelve su propio `ResultadoParseo` con `ErrorGuion`, y
  `ServicioGuiones` lo traduce a `Resultado<T>`.
- **DbContext.** En Interactive Server un contexto scoped vive todo el circuito; los servicios usan
  `IDbContextFactory<RodajeDbContext>` y crean un contexto por operación (`await using`).
- **Lectura del texto (no es normalización).** Al leer: se quita el BOM inicial, se parte por `\n`
  y se quita el `\r` final, y cada línea se compara sin los espacios del inicio y del final. Las
  líneas en blanco se ignoran pero cuentan para el número de línea. Los valores guardados son el
  texto que sigue al marcador, sin espacios en los extremos; nada se corrige.
- **Detección permisiva, validación estricta de marcadores** (hallazgo del arch-auditor): toda línea
  que *parece* un marcador se clasifica como tal y luego se valida su forma; si no cumple, es error
  en esa línea. Nunca cae como acción.
- **Interpretaciones del Anexo A acordadas con la usuaria (PLAN, 2026-10-27):**
  1. Se aceptan comillas rectas `"…"` y tipográficas `“…”` (la de apertura y la de cierre deben ser
     del mismo tipo). Se guarda el texto sin comillas y, aparte, la línea original tal cual.
  2. En el encabezado, LUGAR va del primer separador al último y MOMENTO es lo que sigue al último
     (`ESCENA 2 — EXT. CALLE - SAN JOSÉ — NOCHE` → LUGAR `CALLE - SAN JOSÉ`, MOMENTO `NOCHE`).
     Separadores válidos: `—` o `-`, mezclables, con espacios alrededor. El guion medio `–` no es
     separador.
  3. Son error en esa línea: texto después de la comilla de cierre (`ANA: "Hola" y se va.`),
     comillas vacías (`ANA: ""`) y acotación vacía o sin cerrar (`ANA (): "x"`,
     `ANA (sonriendo: "x"`).
- **Ids de diálogo.** El parser asigna `c{clip}-d{n}` (n desde 1 dentro de cada clip, en orden de
  aparición). Son únicos dentro de la escena (FEAT-001b los usa como enum por escena).

## Block 1 — Esqueleto de la solución y base común

**Files**
- `RodajeIA.slnx` (new) — solución con los dos proyectos.
- `src/RodajeIA.Web/RodajeIA.Web.csproj` (new) — desde la plantilla `blazor` (`--interactivity
  Server --empty false`, sin auth), `net10.0`, `Nullable` y `ImplicitUsings` habilitados.
- `src/RodajeIA.Web/Program.cs` (new) — host de la plantilla; `AddInteractiveServerComponents()
  .AddHubOptions(o => o.MaximumReceiveMessageSize = 1 * 1024 * 1024)`; `DetailedErrors` del
  circuito solo en Development; `UseExceptionHandler` + `UseHsts` fuera de Development, como en la
  plantilla (M-05).
- `src/RodajeIA.Web/Properties/launchSettings.json` (new, de la plantilla) — perfiles solo con
  `http://localhost:*` / `https://localhost:*`; ningún `0.0.0.0` ni `*` (M-01).
- `src/RodajeIA.Web/Servicios/Resultado.cs` (new) — `Resultado<T>` y `Error`.
- `src/RodajeIA.Web/Components/Pages/Counter.razor`, `Weather.razor` (borrados tras generar la
  plantilla) — páginas de ejemplo que no pertenecen al producto.
- `tests/RodajeIA.Tests/RodajeIA.Tests.csproj` (new) — xUnit v3, referencia al proyecto web.
- `tests/RodajeIA.Tests/Servicios/ResultadoTests.cs` (new).
- `.editorconfig` (new) — reglas de `dotnet format` (indentación 4, `var` cuando el tipo es
  evidente, `file_scoped` namespaces, `IDE0005` como warning).
- `.config/dotnet-tools.json` (new) — herramienta `dotnet-ef` fijada.
- `.gitignore` (modified) — agrega `bin/`, `obj/`, `*.db`, `*.db-shm`, `*.db-wal`, `__pycache__/`
  fuera del bloque gestionado por DAW; `git rm --cached` del `.pyc` versionado por error en
  `cae9561`.
- `README.md` (modified) — sección «Cómo arrancar» que remite a los comandos de `AGENTS.md`.

**Logic**
- `public sealed record Error(string Motivo, string? Campo = null, int? Linea = null);`
- `public sealed class Resultado<T>` con `T? Valor`, `IReadOnlyList<Error> Errores`, `bool EsExito
  => Errores.Count == 0`, fábricas `Exito(T valor)` y `Fallo(IEnumerable<Error> errores)`.
  `Fallo` con lista vacía lanza `ArgumentException` (un fallo sin motivo no existe).
- Paquetes (última versión estable al implementar, nunca preview; se registra la versión en el
  reporte del bloque): `xunit.v3`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`,
  `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design`, `bunit`
  (verificar compatibilidad con xUnit v3 y .NET 10 antes de agregarlo), herramienta `dotnet-ef`.
  Solo estos paquetes, desde nuget.org y de sus propietarios oficiales, con versión exacta en el
  `.csproj`; el SAST de CODE incluye `dotnet list package --vulnerable --include-transitive` sin
  hallazgos (M-07).
- El límite de 1 MB de SignalR existe porque `oninput` envía el texto completo del guion en cada
  evento y el valor por defecto (32 KB) cortaría el circuito con guiones grandes (FR-08). 1 MB
  cubre el máximo de guion aceptado (200 000 caracteres, Block 5) en UTF-8.

**Error handling**
- `Resultado<T>.Fallo` con lista de errores vacía → `ArgumentException`.
- Acceso a `Valor` de un fallo → devuelve `default`; los consumidores consultan `EsExito` primero
  (documentado en el XML doc).

**Required tests**
- [ ] `Exito_DevuelveValorYSinErrores` — `EsExito` true, `Valor` igual al dado.
- [ ] `Fallo_ConErrores_ConservaLineaCampoYMotivo` — un `Error` con línea y otro con campo.
- [ ] `Fallo_SinErrores_LanzaArgumentException` — error documentado 1.
- [ ] `Fallo_ValorEsDefault` — error documentado 2.
- [ ] `dotnet build` y `dotnet format --verify-no-changes` sin errores (verificación del esqueleto).

**Completion criterion**
`dotnet build RodajeIA.slnx` y `dotnet format --verify-no-changes` terminan sin errores, `dotnet
test` ejecuta los 4 tests de `ResultadoTests` en verde, y `git ls-files` ya no lista el `.pyc`.

## Block 2 — Modelo de datos y migración inicial

**Files**
- `src/RodajeIA.Web/Datos/RodajeDbContext.cs` (new) — `DbSet`s, configuración fluida.
- `src/RodajeIA.Web/Datos/Serie.cs`, `Personaje.cs`, `Episodio.cs`, `Escena.cs`, `Clip.cs`,
  `LineaDialogo.cs` (new) — entidades.
- `src/RodajeIA.Web/Datos/EstadoGeneracion.cs`, `IntExt.cs` (new) — enums.
- `src/RodajeIA.Web/Datos/Migraciones/*_Inicial.cs` (new) — generada con `dotnet ef migrations add
  Inicial --output-dir Datos/Migraciones`.
- `src/RodajeIA.Web/Program.cs` (modified) —
  `AddDbContextFactory<RodajeDbContext>(o => o.UseSqlite(cs))` con la cadena
  `ConnectionStrings:RodajeIA`.
- `src/RodajeIA.Web/appsettings.json` (modified) — `"ConnectionStrings": { "RodajeIA": "Data
  Source=rodajeia.db" }`.
- `tests/RodajeIA.Tests/Datos/BaseDeDatosEnMemoria.cs` (new) — fixture: `SqliteConnection`
  `DataSource=:memory:` abierta + `IDbContextFactory` + `EnsureCreated`.
- `tests/RodajeIA.Tests/Datos/RodajeDbContextTests.cs` (new).

**Data model**

| Entidad | Campo | Tipo | Restricciones |
|---|---|---|---|
| `Serie` | `Id` | int | PK |
| | `Nombre` | string | requerido, máx. 100 |
| `Personaje` | `Id` | int | PK |
| | `SerieId` | int | FK → `Serie`, requerido, `ON DELETE CASCADE`; índice |
| | `Nombre` | string | requerido, máx. 60 |
| | `Edad` | string | requerido, máx. 40 |
| | `DescripcionFisica` | string | requerido, máx. 2000 (incluye voz) |
| | `Peinado` | string | requerido, máx. 300 |
| | `Vestuario` | string | requerido, máx. 500 |
| | `HeridasMarcas` | string? | nullable, máx. 500 |
| | `Personalidad` | string | requerido, máx. 1000 |
| | `Rol` | string | requerido, máx. 200 |
| `Episodio` | `Id` | int | PK |
| | `SerieId` | int | FK → `Serie`, requerido, cascade |
| | `Numero` | int | requerido, ≥ 1; **índice único (`SerieId`, `Numero`)** |
| | `Titulo` | string | requerido, máx. 150 |
| | `Guion` | string? | nullable (TEXT), null hasta la primera carga |
| `Escena` | `Id` | int | PK |
| | `EpisodioId` | int | FK → `Episodio`, requerido, cascade; **único (`EpisodioId`, `Numero`)** |
| | `Numero` | int | requerido |
| | `IntExt` | `IntExt` (`Int`, `Ext`) | requerido, guardado como string |
| | `Lugar` | string | requerido, máx. 200 |
| | `Momento` | string | requerido, máx. 100 |
| | `Locacion`, `Iluminacion`, `PuestaEnEscena`, `Audio` | string | requeridos (TEXT) |
| | `Estado` | `EstadoGeneracion` (`Pendiente`, `Generando`, `Generada`, `Error`) | requerido, string, default `Pendiente` |
| `Clip` | `Id` | int | PK |
| | `EscenaId` | int | FK → `Escena`, requerido, cascade; **único (`EscenaId`, `Numero`)** |
| | `Numero` | int | requerido |
| | `Accion` | string | requerido (TEXT, puede ser `""` si el clip solo tiene diálogo o texto en pantalla); líneas unidas con `\n` |
| | `TextoEnPantalla` | string? | nullable, texto sin comillas |
| | `TextoEnPantallaOriginal` | string? | nullable, con sus comillas originales (lo usa FEAT-001c) |
| `LineaDialogo` | `Id` | int | PK |
| | `ClipId` | int | FK → `Clip`, requerido, cascade; **único (`ClipId`, `Codigo`)** — como `Codigo` incluye el número de clip y los clips son únicos por escena, el código es único por escena |
| | `Codigo` | string | requerido, máx. 20, formato `c{clip}-d{n}` |
| | `Orden` | int | requerido, orden dentro del clip |
| | `Personaje` | string | requerido, máx. 100 — **texto, no FK** (un `MESERO` sin hoja también tiene diálogo) |
| | `Acotacion` | string? | nullable, máx. 200 |
| | `Texto` | string | requerido, sin comillas |
| | `Original` | string | requerido, la línea tal como está en el guion (lo usa FEAT-001c) |
| `EscenaPersonaje` | (`EscenaId`, `PersonajeId`) | int, int | PK compuesta; FK cascade a ambos |
| `ClipPersonaje` | (`ClipId`, `PersonajeId`) | int, int | PK compuesta; FK cascade a ambos |

Cascadas: `Serie → Personaje`, `Serie → Episodio → Escena → Clip → LineaDialogo`, y las tablas de
unión. FEAT-001b colgará `Bloque`/`Toma` de `Clip` con cascada, de modo que la sobrescritura de
FR-09 también los borre.

**Logic**
Configuración fluida en `OnModelCreating` (longitudes, índices, `HasConversion<string>()` para los
enums, `HasDefaultValue(EstadoGeneracion.Pendiente)`, N:M con `UsingEntity` para las tablas de
unión). Microsoft.Data.Sqlite activa `foreign_keys=ON`, requerido para las cascadas.

**Error handling**
- Insertar un `Episodio` con (`SerieId`, `Numero`) repetido → `DbUpdateException` por el índice
  único (los servicios lo previenen antes; el índice es la última barrera).
- Insertar una `LineaDialogo` con (`ClipId`, `Codigo`) repetido → `DbUpdateException`.

**Required tests**
- [ ] `EliminarEpisodio_BorraEscenasClipsYLineasEnCascada` — base de FR-09.
- [ ] `EpisodioConNumeroRepetidoEnLaSerie_LanzaDbUpdateException` — error documentado 1.
- [ ] `EpisodioConMismoNumeroEnOtraSerie_SeGuarda` — el índice es por serie.
- [ ] `LineaDialogoConCodigoRepetidoEnElClip_LanzaDbUpdateException` — error documentado 2.
- [ ] `EscenaNueva_TieneEstadoPendientePorDefecto` — FR-17.
- [ ] `PersonajeSinHeridas_SeGuardaConHeridasNull` — campo opcional.

**Completion criterion**
La migración `Inicial` existe en `Datos/Migraciones/`, `dotnet ef database update` crea
`rodajeia.db` sin errores y los 6 tests de `RodajeDbContextTests` pasan.

## Block 3 — Parser y validación del guion

**Files**
- `src/RodajeIA.Web/Guiones/GuionParser.cs` (new) — `ResultadoParseo Parsear(string texto)`.
- `src/RodajeIA.Web/Guiones/GuionParseado.cs` (new) — records `GuionParseado`, `EscenaParseada`,
  `ClipParseado`, `LineaDialogoParseada`.
- `src/RodajeIA.Web/Guiones/ErrorGuion.cs` (new) — `record ErrorGuion(int Linea, string Motivo)`.
- `src/RodajeIA.Web/Guiones/ResultadoParseo.cs` (new) — `GuionParseado? Guion`,
  `IReadOnlyList<ErrorGuion> Errores`; `Guion` es null si y solo si hay errores.
- `tests/RodajeIA.Tests/Guiones/GuionParserTests.cs` (new).
- `tests/RodajeIA.Tests/Guiones/GuionesDePrueba.cs` (new) — la escena de ejemplo del Anexo A y
  generador de un guion de 10 × 20.

**Logic**

Records (inmutables):
- `EscenaParseada(int Numero, int LineaEncabezado, IntExt IntExt, string Lugar, string Momento,
  string Locacion, string Iluminacion, string PuestaEnEscena, string Audio,
  IReadOnlyList<ClipParseado> Clips)`.
- `ClipParseado(int Numero, int Linea, IReadOnlyList<string> LineasAccion,
  IReadOnlyList<LineaDialogoParseada> Dialogos, string? TextoEnPantalla, string?
  TextoEnPantallaOriginal)`.
- `LineaDialogoParseada(string Codigo, int Orden, string Personaje, string? Acotacion, string
  Texto, string Original)`.

Clasificación de cada línea no vacía, **en este orden** (detección permisiva → validación
estricta):

| # | La línea se clasifica como… | si (detección) | y es válida si (validación) | Si no es válida |
|---|---|---|---|---|
| 1 | Encabezado de escena | empieza con `ESCENA` (sin distinguir mayúsculas) seguido de espacio, dígito o fin | prefijo `^ESCENA ([1-9]\d*)\s+[—-]\s+(INT\.\|EXT\.)\s+` por regex; el resto se parte **sin regex**, con `LastIndexOf` del último separador (`—` o `-` rodeado de espacios): LUGAR antes, MOMENTO después (M-06, sin backtracking); LUGAR y MOMENTO no vacíos | error «Encabezado inválido: se espera `ESCENA N — INT./EXT. LUGAR — MOMENTO`» |
| 2 | `CLIP` | empieza con `CLIP` (sin distinguir mayúsculas) seguido de espacio, dígito o fin | `^CLIP ([1-9]\d*)$` | error «Marcador de clip inválido: se espera `CLIP N`» |
| 3 | Texto en pantalla | empieza con `TEXTO EN PANTALLA` (sin distinguir mayúsculas) | `^TEXTO EN PANTALLA:\s*(.+)$` y el valor es un texto entre comillas válido (ver abajo) | error «El texto en pantalla debe ir entre comillas» |
| 4 | Campo de escena | `^(LOCACIÓN\|ILUMINACIÓN\|PUESTA EN ESCENA\|AUDIO):` exacto | valor no vacío; reglas de orden abajo | error según la regla |
| 5 | Marcador en mayúsculas (diálogo) | `^[A-ZÁÉÍÓÚÑÜ][A-ZÁÉÍÓÚÑÜ0-9 ]*\s*[(:]` | `^NOMBRE(\s*\((.*)\))?\s*:\s*(.+)$`, acotación no vacía si hay paréntesis, paréntesis cerrado, texto entre comillas válido | error con el motivo exacto (acotación vacía, paréntesis sin cerrar, texto sin comillas, comillas vacías, texto tras la comilla) |
| 6 | Acción | cualquier otra línea | debe estar dentro de un clip | error de ubicación |

Texto entre comillas válido: empieza con `"` y termina con `"`, o empieza con `“` y termina con
`”`; tiene al menos 2 caracteres; el interior sin espacios en los extremos no está vacío. El valor
guardado es el interior; `Original` es la línea completa sin espacios en los extremos.

Máquina de estados (`AntesDeEscena` → `EnCampos` → `EnClip`), acumulando errores sin detenerse:

- **Sin ningún encabezado** (detectado por la regla 1) en todo el texto, o texto vacío → un único
  error en la línea 1: «El guion no tiene ninguna escena (`ESCENA N — …`)». No se reportan otros.
- **Texto antes del primer encabezado** → un error en la primera de esas líneas: «Texto antes de
  la primera escena».
- **Numeración**: escenas desde 1, cada encabezado debe ser el anterior + 1; si no, error en ese
  encabezado («Se esperaba ESCENA k») y el contador sigue desde el número leído (con `ESCENA 1, 3,
  4` hay un solo error, en la 3). Igual para `CLIP` dentro de cada escena.
- **Límites**: el encabezado número 11 del guion da «Se superó el límite de 10 escenas» en su línea
  (una sola vez); el `CLIP` número 21 de una escena da «Se superó el límite de 20 clips por
  escena» en su línea (una vez por escena).
- **Encabezado inválido**: abre igual una escena (para seguir validando su contenido), cuenta como
  anterior + 1 para la numeración, y no genera errores de «falta campo» ni «sin clips».
- **Campos de escena** (orden `LOCACIÓN`, `ILUMINACIÓN`, `PUESTA EN ESCENA`, `AUDIO`), en estado
  `EnCampos`:
  - repetido → error en la línea repetida («Campo repetido»);
  - sin valor → error en esa línea («Campo sin valor»); cuenta como presente;
  - fuera de orden: si falta alguno de los campos que lo preceden → error en esa línea («Campo
    fuera de orden: va después de X»); cuenta como presente (con `ILUMINACIÓN` antes de
    `LOCACIÓN`, un único error, en `ILUMINACIÓN`).
  - En estado `EnClip`, un campo de escena da «El campo X va antes del primer CLIP» en esa línea y
    no cuenta como presente.
- **Línea no reconocida entre el encabezado y el primer `CLIP`** (marcador en mayúsculas o acción)
  → error en esa línea: «Línea fuera de lugar: entre el encabezado y el primer CLIP solo van los
  campos de escena».
- **Al cerrar cada escena válida**: por cada campo ausente, error en la línea del encabezado
  («Falta el campo X»); sin ningún `CLIP`, error en la línea del encabezado («La escena no tiene
  ningún CLIP»).
- **Clips**: `TEXTO EN PANTALLA` repetido en el clip → error en la segunda. Al cerrar un clip sin
  acción, sin diálogo y sin texto en pantalla → error en la línea del `CLIP` («El clip no tiene
  contenido»). Un clip con solo texto en pantalla tiene contenido.
- **Ids de diálogo**: `c{numeroClip}-d{orden}` con orden desde 1.
- El resultado tiene `Guion` solo si `Errores` está vacía; los errores se devuelven ordenados por
  línea.

**Input validation**
- Tipo: `string` (null se trata como vacío). La longitud máxima (200 000 caracteres) la impone
  `ServicioGuiones` (Block 5) antes de llamar al parser.
- Formato: Anexo A del PRD base con las interpretaciones de «Decisiones de diseño».

**Error handling** (cada uno produce un `ErrorGuion(línea, motivo)`; nunca excepción)
1. Guion vacío o sin encabezado → línea 1.
2. Texto antes de la primera escena.
3. Encabezado inválido (INT/EXT mal escrito, `I/E`, separadores inválidos, LUGAR o MOMENTO vacío).
4. Numeración de escena salteada o repetida.
5. Numeración de clip salteada o repetida.
6. Marcador de clip inválido (`CLIP1`, `Clip 2`, `CLIP 01`).
7. Más de 10 escenas.
8. Más de 20 clips en una escena.
9. Campo de escena ausente (línea del encabezado).
10. Campo de escena repetido.
11. Campo de escena sin valor.
12. Campo de escena fuera de orden.
13. Campo de escena después del primer `CLIP`.
14. Línea fuera de lugar entre el encabezado y el primer `CLIP`.
15. Escena sin ningún `CLIP`.
16. Clip sin contenido.
17. Diálogo sin comillas.
18. Texto después de la comilla de cierre.
19. Comillas vacías.
20. Acotación vacía o paréntesis sin cerrar.
21. Texto en pantalla sin comillas.
22. Más de un texto en pantalla en el clip.

**Required tests**
- [ ] `EscenaDeEjemplo_ObtieneEncabezadoCamposYClips` — AC-23.
- [ ] `EscenaDeEjemplo_ConGuionComunYLineasEnBlanco_IgualCampoPorCampo` — AC-24.
- [ ] `TextoEnPantalla_SeGuardaAparteDeAccionYDialogo` — AC-25.
- [ ] `ComillasTipograficas_SeAceptanYConservanOriginal` — decisión 1.
- [ ] `LugarConGuion_SeTomaEntrePrimerYUltimoSeparador` — decisión 2.
- [ ] `IdsDeDialogo_SonCDeClipGuionDDeOrden` — `c1-d1`, `c1-d2`.
- [ ] `MismoGuionDosVeces_ResultadosIguales` — determinismo (FR-14).
- [ ] `FaltaAudioEnEscena1YSinClipEnEscena2_DosErroresEnLosEncabezados` — AC-13; errores 9 y 15.
- [ ] `GuionVacio_ErrorEnLinea1` y `GuionSinEscena_ErrorEnLinea1` — AC-14; error 1.
- [ ] `TextoAntesDeLaPrimeraEscena_ErrorEnEsaLinea` — AC-18; error 2.
- [ ] `EncabezadoIE_ErrorEnElEncabezado` — AC-18; error 3.
- [ ] `EncabezadoIntSinPuntoNoPrimero_ErrorEnElEncabezado` — error 3 (hallazgo del auditor).
- [ ] `Escena1SeguidaDeEscena3_UnSoloErrorEnEscena3` — AC-17; error 4.
- [ ] `DosClip1_ErrorEnElSegundo` — AC-17; error 5.
- [ ] `ClipMalEscrito_ErrorEnEsaLineaYNoEsAccion` — `CLIP1`, `Clip 2`, `CLIP 01`; error 6.
- [ ] `OnceEscenas_ErrorEnElEncabezadoDeLaEscena11` — AC-15; error 7.
- [ ] `VeintiunClips_ErrorEnElClip21` — AC-16; error 8.
- [ ] `LocacionRepetida_ErrorEnLaSegunda` — AC-20; error 10.
- [ ] `AudioSinTexto_ErrorEnEsaLinea` — AC-20; error 11.
- [ ] `IluminacionAntesDeLocacion_UnSoloErrorEnIluminacion` — AC-20; error 12.
- [ ] `AudioDespuesDeClip1_ErrorEnEseAudio` — error 13.
- [ ] `TextoEntreAudioYClip1_ErrorEnEsaLinea` — AC-21; error 14.
- [ ] `Clip2SinContenido_ErrorEnClip2` — AC-21; error 16.
- [ ] `ClipSoloConTextoEnPantalla_EsValido` — límite de error 16.
- [ ] `DialogoSinComillas_ErrorEnEsaLinea` — AC-19; error 17.
- [ ] `TextoDespuesDeLaComilla_ErrorEnEsaLinea` — error 18.
- [ ] `ComillasVacias_ErrorEnEsaLinea` — error 19.
- [ ] `AcotacionVaciaOAbierta_ErrorEnEsaLinea` — error 20.
- [ ] `TextoEnPantallaSinComillas_ErrorEnEsaLinea` — AC-19; error 21.
- [ ] `DosTextosEnPantalla_ErrorEnElSegundo` — AC-21; error 22.
- [ ] `GuionInvalido_NoDevuelveGuionParseado` — FR-13.
- [ ] `Rendimiento_GuionDe10x20_P95MenorA1Segundo` — NFR-01 (100 corridas).
- [ ] `Rendimiento_LineaPatologica200000_MenorA1Segundo` — M-06: encabezado de 200 000
  caracteres con miles de ` - `.

**Completion criterion**
Los 34 tests de `GuionParserTests` pasan; cada uno de los 22 errores documentados tiene al menos
un test que lo produce con el número de línea exacto.

## Block 4 — Detección de personajes

**Files**
- `src/RodajeIA.Web/Guiones/DetectorPersonajes.cs` (new) — `PersonajesDetectados
  Detectar(GuionParseado guion, IReadOnlyList<string> nombresDeHojas)`.
- `src/RodajeIA.Web/Guiones/PersonajesDetectados.cs` (new) — por (escena, clip) los nombres
  presentes, y por escena la unión.
- `tests/RodajeIA.Tests/Guiones/DetectorPersonajesTests.cs` (new).
- `tests/RodajeIA.Tests/ArquitecturaTests.cs` (new) — `Guiones/` no referencia `Generacion`,
  `Servicios` ni `Datos`.

**Logic**
- Para cada nombre de hoja: regex compilada `(?<![\p{L}\p{N}])` + `Regex.Escape(nombre)` +
  `(?![\p{L}\p{N}])`, **sin** `IgnoreCase`. Antes de comparar, el nombre y el texto se llevan a
  forma Unicode NFC (solo para comparar; el texto guardado no cambia).
- Personaje presente en un clip si el nombre aparece en alguna de sus `LineasAccion`, o si algún
  diálogo tiene `Personaje == nombre.ToUpper(CultureInfo("es-ES"))` (comparado en NFC, con el
  marcador ya separado de la acotación). **No** se busca en el texto del diálogo, la acotación ni
  el texto en pantalla.
- Personaje presente en una escena = presentes en alguno de sus clips ∪ nombres que aparecen en
  `PuestaEnEscena` con la misma regex.
- Nombres sin hoja (`MESERO`) no se detectan.

**Error handling**
- Lista de hojas vacía → resultado sin personajes (no es error).
- Nombre de hoja con caracteres especiales de regex (`.`, `(`) → se escapan; no lanza.

**Required tests**
- [ ] `AnaEnLaAccion_PresenteEnClipYEscena` — AC-26.
- [ ] `DialogoAna_PresenteEnClipYEscena` — AC-26.
- [ ] `Anabel_NoDetectaAna` — AC-27.
- [ ] `JoseDanielSinTilde_NoDetectaJoseDaniel` — AC-27.
- [ ] `MeseroSinHoja_NoSeDetecta` — AC-28.
- [ ] `AnaSoloEnPuestaEnEscena_PresenteEnEscenaYEnNingunClip` — AC-29.
- [ ] `NombreDentroDelTextoDeDialogo_NoSeDetecta` — `ANA: "Saludos a José Daniel"`.
- [ ] `JoseDanielEnFormaNFD_SeDetecta` — NFC.
- [ ] `SinHojas_ResultadoVacio` — error documentado 1.
- [ ] `NombreConCaracteresDeRegex_NoLanza` — error documentado 2.
- [ ] `Guiones_NoReferenciaGeneracionServiciosNiDatos` — NFR-02 (test de arquitectura por
  reflexión sobre los tipos del espacio de nombres `RodajeIA.Web.Guiones`).

**Completion criterion**
Los 11 tests pasan, incluido el de arquitectura.

## Block 5 — Servicios

**Files**
- `src/RodajeIA.Web/Servicios/ServicioSeries.cs` (new) — `CrearAsync(string nombre)` →
  `Resultado<SerieResumen>`; `ListarAsync()` → `IReadOnlyList<SerieResumen>` ordenadas por nombre.
- `src/RodajeIA.Web/Servicios/ServicioPersonajes.cs` (new) — `CrearAsync(int serieId,
  DatosPersonaje datos)` → `Resultado<PersonajeResumen>`; `ListarAsync(int serieId)`.
- `src/RodajeIA.Web/Servicios/ServicioEpisodios.cs` (new) — `CrearAsync(int serieId, int numero,
  string titulo)` → `Resultado<EpisodioResumen>`; `ListarAsync(int serieId)` ordenados por número.
- `src/RodajeIA.Web/Servicios/ServicioGuiones.cs` (new) — `CargarAsync(int? serieId, int?
  episodioId, string texto)` → `Resultado<ResumenCarga>` (`ResumenCarga(int Escenas, int Clips)`).
- `src/RodajeIA.Web/Servicios/Modelos.cs` (new) — records `SerieResumen`, `PersonajeResumen`,
  `EpisodioResumen`, `DatosPersonaje`, `ResumenCarga` (la UI no recibe entidades de EF).
- `src/RodajeIA.Web/Program.cs` (modified) — registro scoped de los 4 servicios.
- `tests/RodajeIA.Tests/Servicios/ServicioSeriesTests.cs`, `ServicioPersonajesTests.cs`,
  `ServicioEpisodiosTests.cs`, `ServicioGuionesTests.cs` (new) — con `BaseDeDatosEnMemoria`.

**Logic**
- Todas las operaciones crean su contexto con `IDbContextFactory` (`await using`).
- `ServicioGuiones.CargarAsync`:
  1. Valida entrada (abajo); si falla, devuelve `Fallo` sin tocar la base.
  2. `GuionParser.Parsear(texto)`; si hay errores, devuelve `Fallo` con cada `ErrorGuion`
     traducido a `Error(Motivo, Linea: …)` en el mismo orden, **sin abrir transacción** (FR-13).
  3. Carga los nombres de las hojas de la serie y ejecuta `DetectorPersonajes`.
  4. En **una transacción**: borra las escenas del episodio (cascada a clips, líneas y tablas de
     unión), guarda `Episodio.Guion = texto`, inserta escenas (estado `Pendiente`), clips, líneas
     de diálogo, `ClipPersonaje` y `EscenaPersonaje`; `SaveChanges`; commit. Si algo falla, rollback
     y el episodio queda como estaba.
- Solo consultas LINQ de EF Core; prohibido `FromSqlRaw`/`ExecuteSqlRaw` (M-03).
- `ServicioGuiones` registra con `ILogger` en nivel `Information` cada carga: episodio, resultado
  (guardado / rechazado) y cantidad de errores o escenas, **sin** el contenido del guion (M-04).
- `ServicioPersonajes.CrearAsync` recorta espacios en los extremos de cada campo antes de validar y
  guardar (los valores no se reescriben de otra forma).

**Input validation**

| Operación | Campo | Regla |
|---|---|---|
| Crear serie | `nombre` | string, no vacío tras recortar, máx. 100 |
| Crear personaje | `serieId` | serie existente |
| | `Nombre`, `Edad`, `DescripcionFisica`, `Peinado`, `Vestuario`, `Personalidad`, `Rol` | requeridos (no vacíos tras recortar); máx. 60 / 40 / 2000 / 300 / 500 / 1000 / 200 |
| | `HeridasMarcas` | opcional; vacío se guarda como null; máx. 500 |
| Crear episodio | `serieId` | serie existente |
| | `numero` | entero ≥ 1 y ≤ 9999; único en la serie |
| | `titulo` | no vacío tras recortar, máx. 150 |
| Cargar guion | `serieId`, `episodioId` | ambos presentes; el episodio existe y pertenece a la serie |
| | `texto` | string no null; máx. 200 000 caracteres; el resto lo valida el parser |

**Error handling** (todos devuelven `Resultado.Fallo`; nunca excepción hacia la UI)
1. Nombre de serie vacío o > 100 → `Error("El nombre es obligatorio" | "…máximo 100", Campo:
   "Nombre")`.
2. Serie inexistente al crear personaje o episodio → `Error("La serie no existe")`.
3. Campo obligatorio de personaje vacío → un `Error("Falta {campo}", Campo)` por cada uno (p. ej.
   «Falta el peinado»).
4. Campo de personaje que supera su máximo → `Error(…, Campo)`.
5. Número de episodio repetido en la serie → `Error("El número 1 ya existe en esta serie", Campo:
   "Numero")`.
6. Número de episodio < 1 o título vacío → `Error(…, Campo)`.
7. Carga sin serie o sin episodio → `Error("Elige una serie y un episodio")`.
8. Episodio que no pertenece a la serie o inexistente → `Error("El episodio no pertenece a la
   serie elegida")`.
9. Guion > 200 000 caracteres → `Error("El guion supera los 200 000 caracteres", Campo: "Guion")`.
10. Guion inválido → errores del parser con su línea.
11. Fallo de base de datos durante la transacción → rollback y `Error("No se pudo guardar el
    guion; no se modificó nada")`; se registra con `ILogger`.

**Required tests**
- [ ] `CrearSeries_ListarDevuelveLasDos` — AC-01 (servicio).
- [ ] `CrearSerie_NombreVacio_Falla` — error 1.
- [ ] `CrearPersonaje_TodosLosCampos_SeGuardaConEsosValores` — AC-02.
- [ ] `CrearPersonaje_SinHeridas_SeGuarda` — AC-03.
- [ ] `CrearPersonaje_PeinadoVacio_NoSeGuardaYFaltaElPeinado` — AC-04; error 3.
- [ ] `CrearPersonaje_CampoDemasiadoLargo_Falla` — error 4.
- [ ] `CrearPersonaje_SerieInexistente_Falla` — error 2.
- [ ] `ListarPersonajes_DosHojas_DevuelveLasDos` — AC-05.
- [ ] `CrearEpisodios_ListarDevuelveLosDos` — AC-06 (servicio).
- [ ] `CrearEpisodio_NumeroRepetido_NoSeGuardaEIndicaQueExiste` — AC-07; error 5.
- [ ] `CrearEpisodio_NumeroCeroOTituloVacio_Falla` — error 6.
- [ ] `CargarGuion_Valido_QuedaAsociadoAlEpisodio` — AC-08.
- [ ] `CargarGuion_SinSerieOEpisodio_NoGuardaNada` — AC-09 (servicio); error 7.
- [ ] `CargarGuion_EpisodioDeOtraSerie_Falla` — error 8.
- [ ] `CargarGuion_Mayor200000_Falla` — error 9.
- [ ] `CargarGuion_ValidoSobreUnoDe3Escenas_QuedanSolo2` — AC-11.
- [ ] `CargarGuion_InvalidoSobreUnoValido_ConservaElAnterior` — AC-12; error 10.
- [ ] `CargarGuion_InvalidoEnEpisodioSinGuion_NoGuardaNada` — AC-22; error 10.
- [ ] `CargarGuion_ErroresTraducidosConLineaYEnOrden` — FR-12.
- [ ] `CargarGuion_Valido_EscenasEnPendienteYSinClienteGemini` — AC-30 (no hay `IClienteGemini`
  registrado ni llamado).
- [ ] `CargarGuion_GuardaPersonajesPorClipYPorEscena` — FR-15, FR-16 persistidos.
- [ ] `CargarGuion_FalloAlGuardar_RollbackYEpisodioSinCambios` — error 11 (contexto que lanza en
  `SaveChanges`).

**Completion criterion**
Los 22 tests de servicios pasan contra SQLite en memoria y los servicios están registrados en
`Program.cs`.

## Block 6 — Interfaz Blazor

**Files**
- `src/RodajeIA.Web/Components/Pages/Series.razor` (new) — `/series`: listado y formulario «Nueva
  serie».
- `src/RodajeIA.Web/Components/Pages/DetalleSerie.razor` (new) — `/series/{SerieId:int}`:
  personajes (lista + formulario con los 8 campos) y episodios (lista + formulario número/título).
- `src/RodajeIA.Web/Components/Pages/CargarGuion.razor` (new) — `/guiones/cargar`: selector de
  serie, selector de episodio (filtrado por serie), `<textarea @bind="texto"
  @bind:event="oninput">`, botón «Validar y guardar» y lista de errores «Línea N: motivo».
- `src/RodajeIA.Web/Components/Pages/Home.razor` (modified) — accesos a Series y Cargar guion.
- `src/RodajeIA.Web/Components/Layout/NavMenu.razor` (modified) — enlaces a las tres páginas;
  quita Counter y Weather.
- `tests/RodajeIA.Tests/Components/Pages/SeriesTests.cs`, `DetalleSerieTests.cs`,
  `CargarGuionTests.cs` (new) — bUnit con servicios reales sobre `BaseDeDatosEnMemoria`.

**Logic**
- Los componentes solo inyectan servicios de `Servicios/` (nunca `RodajeDbContext`).
- El contenido del usuario se muestra solo con `@expresión` (codificado por Blazor); prohibido
  `MarkupString` con texto del usuario (M-02).
- Los formularios muestran los `Error` del `Resultado` junto al campo (`Campo`) o arriba si no hay
  campo; en éxito limpian el formulario y recargan la lista.
- `CargarGuion`: el botón está deshabilitado mientras no haya serie y episodio elegidos; al
  cambiar de serie se vacía el episodio elegido. Un clic ejecuta `ServicioGuiones.CargarAsync`; en
  éxito muestra «Guion guardado: N escenas en estado pendiente»; en fallo, la lista de errores con
  su línea. Mientras se procesa, el botón se deshabilita para evitar doble envío.

**Input validation**
- Textarea: `maxlength="200000"`; la regla real la aplica `ServicioGuiones` (Block 5).
- Inputs de serie, personaje y episodio con `maxlength` iguales a los del Block 5; el número de
  episodio es `type="number" min="1"`. La validación autoritativa es la del servicio.

**Error handling**
1. Serie inexistente en `/series/{id}` → mensaje «La serie no existe» y enlace a `/series`.
2. Errores de validación del servicio → se muestran (campo o general), no se navega.
3. Guion inválido → lista «Línea N: motivo» en orden; el texto pegado se conserva para corregirlo.
4. Clic sin serie o episodio → imposible por el botón deshabilitado; si llega, se muestra el error
   7 del servicio.

**Required tests**
- [ ] `Series_CrearDos_ListadoMuestraLasDos` — AC-01.
- [ ] `CargarGuion_SelectorOfreceLasSeriesCreadas` — AC-01.
- [ ] `DetalleSerie_CrearDosEpisodios_ListaMuestraLosDos` — AC-06.
- [ ] `CargarGuion_SelectorDeEpisodioOfreceLosDeLaSerie` — AC-06.
- [ ] `CargarGuion_SinSerieOEpisodio_BotonDeshabilitado` — AC-09; error 4.
- [ ] `CargarGuion_PegarYUnSoloClic_GuardaElGuion` — AC-10: `Input` en el textarea y un único
  `Click`; el servicio recibe el texto y se muestra «Guion guardado».
- [ ] `DetalleSerie_SerieInexistente_MuestraMensaje` — error 1.
- [ ] `DetalleSerie_PersonajeSinPeinado_MuestraFaltaElPeinado` — error 2.
- [ ] `CargarGuion_Invalido_MuestraErroresConLineaYConservaTexto` — error 3.

**Completion criterion**
Los 9 tests de bUnit pasan, `dotnet run` levanta la app y el recorrido crear serie → crear
personaje → crear episodio → pegar el guion de ejemplo → un clic en «Validar y guardar» muestra
«Guion guardado: 1 escenas en estado pendiente».

## Rollback

- Cambios de esquema: solo la migración `Inicial` (base nueva, sin datos previos).
- Revertir: `git revert` de los commits de CODE de este ticket; en una base local ya creada,
  `dotnet ef database update 0` o borrar `rodajeia.db`.
- Indicadores: la migración falla al aplicarse, o la app no arranca tras `dotnet ef database
  update`.

## Final verification

- `dotnet build`, `dotnet test` y `dotnet format --verify-no-changes` en verde.
- Los 30 AC de `prd-FEAT-001a.md` tienen al menos un test en verde (tabla de cobertura).
- Ningún componente de `Components/` inyecta `RodajeDbContext`; `Guiones/` no referencia
  `Servicios/`, `Datos/` ni `Generacion/`.
- Recorrido manual de demo de la sección del Block 6 completado con la escena de ejemplo del Anexo A.
