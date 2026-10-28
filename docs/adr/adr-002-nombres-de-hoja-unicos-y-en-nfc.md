# ADR-002: Nombres de hoja de personaje únicos por serie y en NFC, validados en el servicio

| Field | Value |
|-------|-------|
| Date | 2026-10-28 |
| Ticket | FEAT-001a |
| Status | Accepted |

## Context
El Block 5 de `docs/daw/specs/spec-FEAT-001a.md` valida el nombre de la hoja solo por largo (≤ 60)
y obligatoriedad, y el Block 2 no tiene índice único `(SerieId, Nombre)`. Pero el detector de
personajes (Block 4) reconoce el diálogo por el marcador `Nombre.ToUpper(es-ES)` y compara en NFC
(ADR-001): dos hojas «Ana» y «ANA», o «José» en NFC y en NFD, producen el mismo marcador y un diálogo
`ANA:` quedaría asignado a ambos personajes.

## Options considered

### Option 1: Seguir el spec literal (sin regla de unicidad)
- **Pros:** sin desviación; el servicio de guiones ya resiste duplicados exactos quedándose con la
  hoja de menor Id.
- **Cons:** la productora puede crear hojas que el detector no distingue, sin ningún aviso.

### Option 2: Índice único `(SerieId, Nombre)` en una migración nueva
- **Pros:** garantía en la base, también ante dos altas simultáneas.
- **Cons:** compara en binario (no detecta «Ana»/«ANA» ni NFC/NFD); cambia el modelo del Block 2.

### Option 3: Validación en el servicio, sin distinguir mayúsculas, y rechazo de NFD (elegida)
- **Pros:** cubre exactamente los casos que el detector confunde; mensaje en el campo `Nombre`.
- **Cons:** sin respaldo en la base: dos altas simultáneas (dos pestañas) podrían duplicar un nombre.

## Decision
Opción 3, elegida por la usuaria el 2026-10-28. `ServicioPersonajes.CrearAsync` rechaza, con
`Campo: "Nombre"`: un nombre que no esté en NFC (sin normalizarlo, como ADR-001), y un nombre que,
llevado a NFC y a mayúsculas es-ES (la misma clave que el marcador del detector), coincida con el de
otra hoja de la misma serie. La carrera entre altas simultáneas
es un riesgo aceptado en una app local y monousuaria (AR-01).

## Consequences
- Archivos: `src/RodajeIA.Web/Servicios/ServicioPersonajes.cs` y sus tests.
- Si aun así existen duplicados exactos o que solo difieren en NFC/NFD, `ServicioGuiones` usa la
  hoja de menor Id (determinista). Si difieren solo en mayúsculas (p. ej. creados por una carrera),
  un diálogo `ANA:` se asigna a ambas hojas.
- El índice único puede agregarse en un ticket posterior si la app deja de ser monousuaria.
