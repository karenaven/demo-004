# ADR-001: Rechazo de líneas en NFD y bordes de palabra del detector de personajes

| Field | Value |
|-------|-------|
| Date | 2026-10-28 |
| Ticket | FEAT-001a |
| Status | Accepted |

## Context
El Block 4 de `docs/daw/specs/spec-FEAT-001a.md` compara en NFC los nombres de las hojas con el
guion. Usa la regex compilada `(?<![\p{L}\p{N}])Nombre(?![\p{L}\p{N}])`. Al revisar el bloque
aparecieron dos problemas:
- **Marcadores de diálogo en NFD.** Un marcador como `JOSÉ:` escrito en NFD (letra + tilde
  combinante) no coincide con el patrón de marcador. Por eso la línea se toma como acción sin
  ningún error. Esto rompe la regla «detección permisiva, validación estricta» y la regla de
  AGENTS.md: «nunca se normaliza ni se corrige».
- **Bordes de palabra.** Con `[\p{L}\p{N}]`, una tilde combinante (`\p{M}`) que sigue al nombre
  cuenta como borde de palabra. Así, `José` se detecta dentro de `Josée`.

Además, el detector arma una regex por cada hoja y por cada carga. `RegexOptions.Compiled` cuesta
más en compilarla que lo que ahorra en un solo uso.

## Options considered

### Option 1: Normalizar el guion a NFC antes de parsearlo
- **Pros:** acepta guiones pegados desde editores que escriben en NFD.
- **Cons:** guarda un texto distinto del que pegó la productora. AGENTS.md prohíbe normalizar.

### Option 2: Rechazar cada línea que no esté en NFC, con su número de línea (elegida)
- **Pros:** no normaliza nada. El error indica la línea exacta y cómo arreglarla.
- **Cons:** sin otra medida, la máquina de estados no avanza y aparecen errores en cascada.
  Por ejemplo, un guion completo en NFD solo da «El guion no tiene ninguna escena».

## Decision
- **Líneas en NFD:** se elige la opción 2. Para evitar la cascada, `GuionParser.ProcesarNoNormalizada`
  procesa internamente la forma NFC de la línea, solo para que la estructura avance. Después
  descarta los errores de esa línea y deja un único error: «Texto con tildes en formato no estándar
  (NFD); vuelve a pegarlo desde otro editor». El guion queda inválido, así que ningún valor
  normalizado llega a guardarse.
- **Bordes de palabra:** el detector usa `[\p{L}\p{M}\p{N}]`.
- **Compilación de la regex:** se usa sin `Compiled`, con `CultureInvariant`. Se mantiene la mitigación
  M-06 del threat model (`Regex.Escape` y nombre de 60 caracteres como máximo).

## Consequences
- Cambia la validación del Block 3: un guion en NFD ahora es inválido.
- Archivos: `src/RodajeIA.Web/Guiones/GuionParser.cs` y `src/RodajeIA.Web/Guiones/DetectorPersonajes.cs`,
  con sus tests en `tests/RodajeIA.Tests/Guiones/`.
- Si una línea en NFD tiene además otro error, ese otro error aparece recién cuando se pega de
  nuevo el texto corregido.
- El spec no se modifica en CODE. Este ADR documenta la desviación.
