# ADR-003: Caracteres compartidos para marcadores y nombres de hoja, y equivalencia de apóstrofos

| Field | Value |
|-------|-------|
| Date | 2026-11-11 |
| Ticket | FIX-001 |
| Status | Accepted |

## Context
El parser solo reconocía como marcador de diálogo las mayúsculas del español, mientras que el alta
de hojas aceptaba cualquier carácter (RCA `docs/daw/specs/rca-FIX-001.md`). El resultado era que una
hoja "Ana-María" se aceptaba, pero su diálogo `ANA-MARÍA:` caía como acción. El PRD ya fija qué
caracteres valen (Anexo A, RF-01i) y que `'` y `’` son equivalentes al comparar (RF-05a). Este ADR
decide cómo se implementa eso y amplía el ADR-002.

## Options considered

### Option 1: Una regex en el parser y otra en el servicio
- **Pros:** cambio mínimo en cada archivo.
- **Cons:** repite la causa raíz: dos reglas que pueden volver a desalinearse.

### Option 2: Fuente única en `Guiones/NombrePersonaje` (elegida)
- **Pros:** el parser, el detector y `ServicioPersonajes` usan el mismo conjunto de caracteres
  (marcador `\p{Lu}[\p{Lu}0-9 '’.\-]*`, hoja `^\p{L}[\p{L}0-9 '’.\-]*$`, ambas con
  `NonBacktracking`) y la misma clave de comparación.
- **Cons:** un archivo más; `Servicios` depende de `Guiones`, algo que la arquitectura ya permite.

### Para las letras sin mayúscula (`ß`, `ª`, `º`, `\p{Lo}`)
- **A (elegida):** se aceptan en la hoja y se documenta que su diálogo no se reconoce como marcador.
- **B:** aceptar letras sin mayúscula en el marcador. Exigía cambiar el Anexo A.
- **C:** rechazar la hoja si su versión en mayúsculas no sirve de marcador. Contradice RF-01i.

## Decision
Opción 2 con la variante A, elegidas por la usuaria el 2026-11-11.
- `ClaveDeMarcador` (NFC y `’`→`'`) se usa **solo para comparar**: el texto del guion y de las hojas
  nunca se normaliza (coherente con ADR-001 y ADR-002).
- La clave de duplicados del ADR-002 también pliega los apóstrofos: "O'Brien" y "O’Brien" son el
  mismo nombre, porque compartirían marcador.
- Las hojas creadas antes de esta regla no se revalidan.

## Consequences
- Archivos: `Guiones/NombrePersonaje.cs` (nuevo), `Guiones/GuionParser.cs`,
  `Guiones/DetectorPersonajes.cs`, `Servicios/ServicioPersonajes.cs` y sus tests (detalle en
  `docs/daw/specs/fix-FIX-001.md`).
- Limitación aceptada: una hoja con letras sin mayúscula en es-ES se puede crear, pero el personaje
  solo se detecta por su mención en la acción, nunca por su diálogo.
- Una hoja guardada antes de esta regla con caracteres que ya no se aceptan sigue existiendo, pero su
  marcador puede no reconocerse.
