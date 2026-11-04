# RCA FIX-001: Marcadores de diálogo con - ' Ç se tratan como acción

| Campo | Valor |
|-------|-------|
| Ticket | FIX-001 |
| Tier | FIX |
| Fecha | 2026-11-04 |
| Origen | Hallazgo W-1 del bloque 4/6 de CODE de FEAT-001a (2026-10-28), diferido a un FIX por decisión de la usuaria |
| PRD relacionado | `docs/daw/prd/PRD.md` (Anexo A, RF-01e, RF-05a); `prd-FEAT-001a.md` (FR-03, FR-15) como antecedente |

## Síntoma

Una línea de diálogo cuyo marcador lleva `-`, `'` o `Ç` (`ANA-MARÍA: "Hola."`, `O'BRIEN: "Hola."`,
`FRANÇOIS: "Bonjour."`) no se reconoce como diálogo: el guion se acepta sin error y la línea queda
como acción del clip.

## Causa raíz

La regex `MarcadorDialogo` de `src/RodajeIA.Web/Guiones/GuionParser.cs:39`:

```
^[A-ZÁÉÍÓÚÑÜ][A-ZÁÉÍÓÚÑÜ0-9 ]*\s*[(:]
```

solo admite las mayúsculas del español, dígitos y espacio. En cambio, `ServicioPersonajes` acepta
cualquier carácter en el nombre de una hoja (solo valida que no esté vacío, el largo máximo y NFC).
Las dos reglas no están alineadas, y el Anexo A del PRD no define qué caracteres admite el nombre de
un marcador.

## Cadena de eventos

1. Se crea la hoja "Ana-María" y `ServicioPersonajes` la acepta.
2. El guion trae `ANA-MARÍA: "Hola."`.
3. `MarcadorDialogo.IsMatch` falla por el `-`, así que la línea va a `clip.Accion`
   (`GuionParser.cs:198-205`) sin generar error.
4. `DetectorPersonajes` busca el patrón de la hoja ("Ana-María", sensible a mayúsculas) en la acción
   y no lo encuentra en "ANA-MARÍA"; el marcador tampoco está entre los diálogos. Resultado: el
   personaje no se detecta en el clip.

## Consecuencias

- La hoja del personaje no llega a Gemini ni al prompt de ese clip (rompe RF-05a y el objetivo 1).
- El diálogo deja de insertarse literal: va como texto de acción, que la IA puede reescribir
  (rompe RN-04).
- Se salta la validación: `ANA-MARÍA: Hola` sin comillas no da error, aunque el Anexo A lo exige.

## Defecto secundario

`tests/RodajeIA.Tests/Guiones/GuionParserTests.cs:91` contiene un BOM (U+FEFF) literal e invisible
dentro de un string. Debe escribirse como el escape `﻿` para que sea legible y no se pierda al
editar o formatear el archivo.

## Componentes afectados

- `Guiones/GuionParser` (regex `MarcadorDialogo`).
- `Guiones/DetectorPersonajes` (equivalencia `'` / `’` al comparar marcador con hoja).
- `Servicios/ServicioPersonajes` y el formulario de hoja (rechazo de caracteres no permitidos).
- Tests de `tests/RodajeIA.Tests/Guiones/` y `Servicios/`.

## Hueco en el PRD y decisiones

Sí había hueco. Decisiones de la usuaria (2026-11-04), incorporadas en `PRD.md`:

1. El nombre del marcador admite letras mayúsculas Unicode, dígitos, espacio, `-`, `'`, `’` y `.`,
   y empieza con letra (Anexo A).
2. El nombre de una hoja nueva solo admite letras, dígitos, espacio, `-`, `'`, `’` y `.`, y empieza
   con letra; si no, se rechaza indicando los caracteres permitidos. Las hojas existentes no se
   tocan (RF-01i, AC-01l, AC-01m).
3. Al comparar marcador con hoja, `'` y `’` son equivalentes; el texto no se modifica (RF-05a,
   AC-05k). Es una equivalencia de comparación, no una normalización (coherente con ADR-002).
4. Criterios de regresión: AC-03v, AC-05h, AC-05i, AC-05j.

## Punto abierto para PLAN

La regla nueva de nombres amplía lo decidido en ADR-002 (nombres únicos y en NFC); evaluar en PLAN si
corresponde un ADR nuevo.
