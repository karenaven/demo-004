# Fix-plan FIX-001: Marcadores de diálogo con - ' Ç se tratan como acción

| Field | Value |
|-------|-------|
| Ticket | FIX-001 |
| Tier | FIX |
| RCA | docs/daw/specs/rca-FIX-001.md |
| PRD | docs/daw/prd/PRD.md (RF-01i, RF-04c, RF-05a, Anexo A) |
| Threat model | docs/daw/security/threat-FIX-001.md |
| Date | 2026-11-11 |
| Spec loops | 0 |

## Problem

Una línea de diálogo cuyo marcador lleva `-`, `'` o `Ç` (`ANA-MARÍA: "Hola."`, `O'BRIEN: "Hola."`,
`FRANÇOIS: "Bonjour."`) no se reconoce como diálogo: el guion se acepta sin error, la línea queda
como acción del clip y el personaje no se detecta. Además, `ANA-MARÍA: Hola` (sin comillas) no da
error. En paralelo, `ServicioPersonajes` acepta cualquier carácter en el nombre de una hoja, y el
test `GuionParserTests.cs:91` contiene un BOM (U+FEFF) literal e invisible.

## Root cause

La regex `MarcadorDialogo` (`src/RodajeIA.Web/Guiones/GuionParser.cs:39-40`,
`^[A-ZÁÉÍÓÚÑÜ][A-ZÁÉÍÓÚÑÜ0-9 ]*\s*[(:]`) solo admite las mayúsculas del español, dígitos y espacio,
mientras que el alta de hojas no restringe caracteres: las dos reglas no estaban alineadas ni
definidas en un único lugar (ver RCA).

## Coverage: PRD → steps

| Requirement | Covered by |
|---|---|
| RF-01i (AC-01l, AC-01m) | Pasos 1, 4 |
| RF-04c / Anexo A — caracteres del marcador (AC-03v, AC-05h, AC-05i, AC-05j) | Pasos 1, 2 |
| RF-05a — equivalencia `'` / `’` (AC-05h, AC-05i, AC-05j, AC-05k) | Pasos 1, 3 |
| Defecto secundario (BOM literal) | Paso 5 |
| Punto abierto del RCA (ADR) | Paso 6 |

## Solution — steps

1. **`src/RodajeIA.Web/Guiones/NombrePersonaje.cs` (nuevo)** — clase estática, fuente única de la
   regla de caracteres (sin dependencias de `Servicios/`, `Datos/` ni `Components/`):
   - `internal const string FragmentoMarcador = @"\p{Lu}[\p{Lu}0-9 '’.\-]*"`: nombre de un marcador
     de diálogo (empieza con mayúscula; mayúsculas de cualquier idioma, dígitos, espacio, `-`, `'`,
     `’`, `.`).
   - `public static bool EsNombreDeHojaValido(string nombre)`: regex estática
     `^\p{L}[\p{L}0-9 '’.\-]*$` con `RegexOptions.CultureInvariant | RegexOptions.NonBacktracking`
     (M-01).
   - `public const string CaracteresPermitidos`: texto para el motivo de error, p. ej. «letras,
     dígitos, espacio, guion (-), apóstrofo (' o ’) y punto, empezando con una letra».
   - `public static string ClaveDeMarcador(string texto)`: NFC y `’`→`'`. **Solo para comparar**;
     nunca se asigna a una entidad ni a `LineaDialogoParseada` (M-03).
2. **`src/RodajeIA.Web/Guiones/GuionParser.cs:39-40`** — `MarcadorDialogo` pasa a
   `new("^" + NombrePersonaje.FragmentoMarcador + @"\s*[(:]", Opciones)` (sigue
   `CultureInvariant | NonBacktracking`). `ProcesarDialogo` no cambia: el conjunto sigue excluyendo
   `(` y `:`, así que el primer `(` o `:` cierra el nombre. El `personaje` guardado es el texto tal
   como está en el guion.
3. **`src/RodajeIA.Web/Guiones/DetectorPersonajes.cs:48` y `:83`** — el set de marcadores del clip
   se arma con `NombrePersonaje.ClaveDeMarcador(d.Personaje)` y `Hoja.Marcador` con
   `ClaveDeMarcador(nfc.ToUpper(Espanol))`. El patrón de la acción (`Patron`) y lo que se devuelve
   (`h.Nombre`) no cambian. Actualizar el comentario de la clase (equivalencia `'`/`’` de RF-05a).
4. **`src/RodajeIA.Web/Servicios/ServicioPersonajes.cs`**:
   - `CrearAsync` (después del chequeo NFC, línea 54-57): si el nombre no está vacío, cabe en
     `MaximoNombre`, está en NFC y `!NombrePersonaje.EsNombreDeHojaValido(nombre)`, agrega
     `new Error($"El nombre solo admite {NombrePersonaje.CaracteresPermitidos}", Campo: "Nombre")`.
     Un nombre en NFD sigue dando un único error (no rompe
     `ServicioPersonajesTests.cs:249-262`). Un nombre vacío o demasiado largo da solo su error actual.
   - `ClaveDeNombre` (línea 108): además de NFC y mayúsculas es-ES, aplica
     `NombrePersonaje.ClaveDeMarcador`, para que "O'Brien" y "O’Brien" sean duplicados (M-02).
   - Las hojas existentes no se tocan: no hay migración ni revalidación de datos guardados.
5. **`tests/RodajeIA.Tests/Guiones/GuionParserTests.cs:91`** — reemplazar el U+FEFF literal del
   string por el escape `"﻿"`. Sin cambio de comportamiento.
6. **`docs/adr/adr-003-caracteres-de-nombres-y-equivalencia-de-apostrofos.md` (nuevo, en PLAN vía
   `/daw-create-adr`)** — amplía ADR-002: conjunto de caracteres compartido en `NombrePersonaje`,
   equivalencia `'`/`’` solo de comparación (coherente con ADR-001/002: el texto no se normaliza),
   duplicados con apóstrofos plegados, y la limitación aceptada por la usuaria (opción A,
   2026-11-11): letras sin mayúscula en es-ES (`ß`, `ª`, `º`, letras `\p{Lo}`) se aceptan en el nombre
   de la hoja pero su diálogo no puede reconocerse como marcador.

## Dependencies between steps

El paso 1 va primero (los pasos 2, 3 y 4 usan `NombrePersonaje`). Los pasos 2, 3 y 4 son
independientes entre sí. El paso 5 es independiente. El paso 6 se escribe en PLAN, antes del código.

## Error handling

- Nombre de hoja con caracteres no permitidos → `Resultado.Fallo` con `Campo: "Nombre"` y el motivo
  con `CaracteresPermitidos`; la UI (`DetalleSerie.razor`) ya muestra los errores del campo Nombre.
- Marcador con caracteres nuevos y sin comillas → el parser reporta `MotivoDialogoSinComillas` en
  esa línea y rechaza el guion completo (AC-03v); nunca corrige el texto.
- Línea de acción en mayúsculas con `.` antes de `:` o `(` (p. ej. `OK. (pausa)`) → ahora se trata
  como marcador y, sin comillas, da error (detección permisiva del Anexo A).
- No se agregan excepciones nuevas: todo error es de validación.

## Tests

- [ ] **Regression test (parser)** — `Parsear_MarcadorConGuion_EsDialogo`: `ANA-MARÍA: "Hola."` queda
  en `Dialogos` (personaje `ANA-MARÍA`) y no en `LineasAccion`. Falla ANTES del fix, pasa DESPUÉS.
  — AC-05h
- [ ] **Regression test (parser)** — `Parsear_MarcadorConGuionSinComillas_SeRechaza`:
  `ANA-MARÍA: Hola` → error en esa línea con motivo «El diálogo debe ir entre comillas». Falla
  ANTES, pasa DESPUÉS. — AC-03v
- [ ] `Parsear_MarcadorConApostrofoYAcotacion_EsDialogo`: `O'BRIEN (serio): "Hola."` → diálogo con
  acotación `serio`. — AC-05i
- [ ] `Parsear_MarcadorConCedilla_EsDialogo`: `FRANÇOIS: "Bonjour."` → diálogo. — AC-05j
- [ ] `Parsear_MarcadorConPuntoYApostrofoTipografico_EsDialogo`: `ST. JAMES: "Hola."` y
  `O’BRIEN: "Hola."` → diálogo, texto del marcador sin modificar.
- [ ] `Parsear_AccionMayusculasConPuntoAntesDeParentesis_SeTrataComoDialogo`: `OK. (pausa)` → error
  de diálogo en esa línea (regresión documentada, M-05).
- [ ] `Parsear_MarcadorQueEmpiezaConDigitoOGuion_EsAccion`: `-ANA: "Hola."` y `1ANA: "Hola."` quedan
  como acción (el marcador empieza con letra).
- [ ] `Rendimiento_LineaPatologicaCaracteresNuevos_MenorA1Segundo`: línea de 200 000 caracteres
  `A-'’.` repetidos, sin `:` ni `(`, en un clip → se procesa en menos de 1 s (M-01). El test existente
  `GuionParserTests.cs:770-800` sigue en verde.
- [ ] **Regression test (detector)** — `Detectar_MarcadorConGuion_DetectaHoja`: hoja "Ana-María",
  clip con `ANA-MARÍA: "Hola."` y acción sin la mención → Ana-María en la lista del clip. Falla
  ANTES, pasa DESPUÉS. — AC-05h
- [ ] `Detectar_MarcadorConApostrofo_DetectaHoja` (hoja "O'Brien", `O'BRIEN (serio): "Hola."`) —
  AC-05i
- [ ] `Detectar_MarcadorConCedilla_DetectaHoja` (hoja "François", `FRANÇOIS: "Bonjour."`) — AC-05j
- [ ] `Detectar_ApostrofoTipograficoEnMarcador_DetectaHojaConApostrofoRecto`: hoja "O'Brien",
  `O’BRIEN: "Hola."` → O'Brien detectado y la `LineaDialogoParseada` conserva `O’BRIEN` y su línea
  original con `’` (M-03). — AC-05k
- [ ] `Detectar_ApostrofoEnAccion_SigueSiendoExacto`: hoja "O'Brien" y acción "O’Brien entra" → no
  detectado por la acción (la equivalencia es solo para el marcador, RF-05a).
- [ ] `CrearPersonaje_NombreConCaracteresNoPermitidos_SeRechaza`: "Ana (niña)" → no se guarda, error
  único con `Campo: "Nombre"` que lista los caracteres permitidos. — AC-01l
- [ ] `CrearPersonaje_NombreQueNoEmpiezaConLetra_SeRechaza`: "-Ana", "1Ana", "'Ana" → error de
  caracteres. — AC-01l
- [ ] `CrearPersonaje_NombresConCaracteresPermitidos_SeGuardan` (Theory): "Ana-María", "O'Brien",
  "François", "St. James" → guardadas tal como se escribieron. — AC-01m
- [ ] `CrearPersonaje_ApostrofoRectoYTipografico_SonRepetidos`: con "O'Brien" guardada, crear
  "O’Brien" → `MotivoNombreRepetido` (M-02).
- [ ] `CrearPersonaje_NombreNoNFC_SeRechazaSinNormalizar` (existente, `ServicioPersonajesTests.cs:
  249-262`) sigue pasando con un único error.
- [ ] `GuionParserTests.cs:91` con `"﻿"`: el test del BOM sigue en verde y el archivo no tiene
  caracteres invisibles literales.

## Regression risk

**Bajo.** Lo que podría romperse:
- Guiones con una línea de acción en mayúsculas que tenga `-`, `'`, `’`, `.` o letras no españolas
  antes de `:` o `(` pasan a tratarse como diálogo y, sin comillas, se rechazan (comportamiento
  exigido por el Anexo A; cubierto por test).
- Nombres de hoja que antes se aceptaban (con paréntesis, `/`, emojis…) ya no pueden crearse; las
  hojas ya guardadas siguen funcionando.
- No cambia ninguna firma pública; `DetectorPersonajes.Detectar` solo se llama desde
  `ServicioGuiones.cs:87`, cuyo mapeo por `h.Nombre` no cambia.

## Rollback plan *(mandatory)*

- Steps: trivial — revertir el commit del fix (`git revert`). No hay migraciones, cambios de esquema
  ni datos transformados que deshacer: las hojas y guiones guardados no se modifican.
- Indicators: guiones válidos según el Anexo A rechazados en masa, personajes que dejan de
  detectarse en clips donde antes aparecían, o el test de rendimiento del parser por encima de 1 s.
