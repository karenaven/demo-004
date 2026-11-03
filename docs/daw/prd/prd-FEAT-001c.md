# PRD FEAT-001c: Prompt final por plantilla y vista del episodio

| Field | Value |
|-------|-------|
| Ticket | FEAT-001c |
| Tracker | none |
| Date | 2026-10-27 |
| PRD loops | 0 |
| Padre | `docs/daw/prd/prd-FEAT-001.md` (índice) |
| PRD base | `docs/daw/prd/PRD.md` (PRD-003 v3) |

## Context and Problem

Tercer sub-ticket de FEAT-001. Con las escenas generadas por FEAT-001b, arma por plantilla
determinística el prompt final de cada escena, listo para pegar en Google Flow, y lo muestra en el
episodio con un botón para copiarlo.

Es el sub-ticket que cumple el **objetivo central** del producto: la descripción de cada personaje
es idéntica, palabra por palabra, en todos los prompts donde aparece, porque la hoja, el diálogo y
el texto en pantalla se insertan literales y la IA nunca los escribe (RN-03, RN-04).

## Goals

1. La descripción de cada personaje con hoja es idéntica palabra por palabra en todos los prompts
   donde aparece (AC-02 es el criterio central).
2. La productora copia con un clic el prompt de cada escena generada, sin editarlo a mano.

Sin variantes en este ticket, la **hoja efectiva** de toda escena es la hoja base.

## Functional Requirements

Entre paréntesis, el requerimiento equivalente del PRD base cuando existe.

**Prompt final**

- FR-01: El sistema debe armar por plantilla (`config/molde-prompt.json`, Anexo D del PRD base) un
  prompt final por escena con las secciones estilo, locación e iluminación base, personajes
  (omitida si no hay ninguno con hoja), puesta en escena, audio y los bloques numerados en el orden
  de los clips (RF-08a, RN-01).
- FR-02: El sistema debe insertar de forma literal en el prompt la hoja de cada personaje presente,
  cada línea de diálogo con su marcador y acotación, y el texto en pantalla, sin pasar por la IA
  (RN-03, RN-04).
- FR-03: El sistema debe permitir copiar el prompt final completo de una escena con un solo botón
  (RF-08b).
- FR-04: El sistema debe mostrar dentro de cada episodio sus escenas en orden, cada una con su estado
  de generación y, si está generada, su prompt final (RF-11e).

## Non-Functional Requirements

- NFR-01: Armado del prompt final de una escena: < 1 s p95 sobre 100 ejecuciones en el equipo local
  (RNF-04).
- NFR-02: Armado del prompt: 0 llamadas a Gemini.

## Acceptance Criteria

*(EARS. "La escena de ejemplo" es la escena válida del Anexo A del PRD base, con las hojas "Ana" y
"José Daniel".)*

**Prompt final**

- AC-01 (FR-01): WHEN se arma el prompt final de la escena de ejemplo con sus bloques generados, THE
  sistema SHALL incluir estilo, locación, iluminación base, las hojas de Ana y José Daniel, puesta
  en escena, audio y "Bloque 1:" y "Bloque 2:" en ese orden, sin ningún campo vacío.
- AC-02 (FR-01, FR-02): WHEN se arman los prompts de dos escenas distintas en las que está presente
  el mismo personaje, THE sistema SHALL incluir su descripción idéntica palabra por palabra en ambos
  (criterio central, objetivo 1).
- AC-03 (FR-01): WHEN se arma el prompt final de una escena donde ni sus clips ni su `PUESTA EN
  ESCENA:` mencionan a ningún personaje con hoja, THE sistema SHALL omitir la sección de personajes
  e incluir el resto de las secciones y todos los bloques.
- AC-04 (FR-02): WHEN se arma el prompt del clip 1 de la escena de ejemplo, THE sistema SHALL
  incluir las líneas `ANA: "Mentiroso."` y `JOSÉ DANIEL (sonriendo): "Bueno... pasó parecido."`
  idénticas al guion.
- AC-05 (FR-02): WHEN se arma el prompt de un clip con `TEXTO EN PANTALLA: "Hermano, ¿puedes salir
  conmigo?"`, THE sistema SHALL incluir ese texto literal en su bloque y no incluir "Sin subtítulos
  ni texto en pantalla.".
- AC-06 (FR-02): WHEN se arma el prompt de una escena con un personaje presente cuya hoja no tiene
  heridas/marcas, THE sistema SHALL no incluir "Heridas/marcas" en su descripción.
- AC-07 (FR-03): WHEN se pulsa el botón de copiar de un prompt final armado, THE sistema SHALL
  dejar en el portapapeles el prompt completo, idéntico al que se muestra.
- AC-08 (FR-04): WHEN se abre un episodio con 3 escenas, la 1 y la 2 generadas y la 3 en "error",
  THE sistema SHALL mostrar las escenas 1, 2 y 3 en ese orden, las dos primeras con su prompt final
  y la 3 con su estado "error".

## Out of Scope

- Integración directa con Google Flow: el prompt se copia y pega a mano.
- Revisión y edición de tomas antes de armar el prompt (RF-07).
- Variantes de personaje (RF-09): la hoja insertada es siempre la hoja base.
- Pantalla para editar la plantilla, el estilo o el vocabulario: se editan los archivos versionados.
- Vista tipo storyboard.

## Risks and Mitigations

| Riesgo | Mitigación |
| --- | --- |
| La redacción de `config/molde-prompt.json` es un borrador | Se ajusta comparando con el prompt manual actual; la estructura de secciones la fija FR-01 |
| Se edita una hoja ya usada en escenas generadas y el prompt cambia | Fuera de este ticket (no hay edición de hojas); en v1 el prompt toma la hoja vigente |
| El portapapeles del navegador falla o no tiene permiso | Se valida con AC-07 en el navegador real |

## Dependencies

- **FEAT-001b**: escenas "generadas" con sus bloques y tomas guardados (FR-01, FR-04).
- **FEAT-001a**: guion dividido, hojas de personaje y personajes detectados por escena (FR-01,
  FR-02).
- **`config/molde-prompt.json`** y **`config/estilo.txt`** (FR-01, FR-02); sin commitear hoy,
  quedan versionados a más tardar con este ticket.
- **PRD base** `docs/daw/prd/PRD.md`: Anexo C (RN-01, RN-03, RN-04) y Anexo D (estructura del
  prompt).
- **Portapapeles del navegador** vía JS interop de Blazor (FR-03).

## Trazabilidad con FEAT-001

FR-01 a FR-04 = FR-29 a FR-32 de `prd-FEAT-001.md`; AC-01 a AC-08 = AC-48 a AC-55; NFR-01 = NFR-02;
NFR-02 = parte de NFR-06.
