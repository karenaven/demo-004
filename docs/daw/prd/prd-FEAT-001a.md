# PRD FEAT-001a: Guion sin IA — series, hojas, episodios, carga y validación

| Field | Value |
|-------|-------|
| Ticket | FEAT-001a |
| Tracker | none |
| Date | 2026-10-27 |
| PRD loops | 0 |
| Padre | `docs/daw/prd/prd-FEAT-001.md` (índice) |
| PRD base | `docs/daw/prd/PRD.md` (PRD-003 v3) |

## Context and Problem

Primer sub-ticket de FEAT-001 («Del guion al prompt final con personajes consistentes»). Construye
la parte **determinística, sin IA**, del recorrido de demo: crear serie, hojas de personaje y
episodio, pegar el guion, validarlo contra la convención de formato (Anexo A del PRD base) y
dividirlo en escenas y clips con sus personajes detectados.

El guion inválido vuelve al guionista con la lista de errores (línea y motivo); el válido queda
guardado con sus escenas en estado "pendiente", listas para la generación de FEAT-001b.

**Corrección del repositorio anterior que aplica aquí (corrección 4 del profesor):** si se pegaba
el guion y se pulsaba directo «Validar y guardar», el primer clic se perdía porque el valor del
campo se actualizaba en `change`. Aquí se actualiza en cada `input` (FR-08).

## Goals

1. La productora carga un guion y, sin ninguna llamada a la IA, obtiene o bien el guion guardado
   dividido en escenas y clips con sus personajes detectados, o bien la lista completa de errores
   con línea y motivo.
2. El parseo es determinístico y testeable sin Gemini (base de la consistencia de FEAT-001b y
   FEAT-001c).

Sin variantes en este ticket, la **hoja efectiva** de toda escena es la hoja base.

## Functional Requirements

Entre paréntesis, el requerimiento equivalente del PRD base cuando existe.

**Series, personajes y episodios**

- FR-01: El sistema debe permitir crear una serie con un nombre (RF-01a).
- FR-02: El sistema debe listar las series existentes (RF-01b).
- FR-03: El sistema debe permitir crear una hoja de personaje dentro de una serie con los campos
  nombre, edad, descripción física (incluye voz), peinado, vestuario, heridas/marcas, personalidad
  y rol; todos son obligatorios salvo heridas/marcas (RF-01e).
- FR-04: El sistema debe listar las hojas de personaje de una serie (RF-01f).
- FR-05: El sistema debe permitir crear un episodio dentro de una serie con número y título; el
  número debe ser único dentro de la serie (RF-11a).
- FR-06: El sistema debe listar los episodios de una serie (RF-11b).

**Carga y validación del guion**

- FR-07: El sistema debe permitir cargar el guion de un episodio como texto plano, asociado a una
  serie y un episodio existentes elegidos antes de la carga (RF-02a).
- FR-08: El campo de texto del guion debe actualizar su valor en cada pulsación o pegado
  (`oninput`), de modo que un único clic en «Validar y guardar» procese el texto pegado
  (corrección 4).
- FR-09: Si el episodio ya tiene guion, el sistema debe sobrescribirlo con el nuevo guion válido y
  eliminar de la base de datos las escenas, clips y bloques del guion anterior (RF-02b).
- FR-10: El sistema debe validar la estructura del guion según el Anexo A del PRD base:
  encabezados `ESCENA N — INT./EXT. LUGAR — MOMENTO`, numeración de escenas y clips desde 1 sin
  saltos ni repeticiones, al menos un `CLIP` por escena, ausencia de texto antes del primer
  encabezado y límites de 10 escenas por guion y 20 clips por escena (RF-03a).
- FR-11: El sistema debe validar el contenido de cada escena y clip según el Anexo A del PRD base:
  los cuatro campos de escena presentes, únicos, con valor, en orden y antes del primer `CLIP`;
  ninguna otra línea entre el encabezado y el primer `CLIP`; cada clip con contenido; diálogo y
  texto en pantalla entre comillas; como máximo un `TEXTO EN PANTALLA` por clip (RF-03a).
- FR-12: Si el guion es inválido, el sistema debe mostrar la lista completa de errores, cada uno con
  su número de línea según la regla de línea reportada del Anexo A y su motivo (RF-03b).
- FR-13: Si el guion es inválido, el sistema debe rechazarlo completo sin guardar el guion, ni
  escenas, ni clips (RF-03c).

**División y detección de personajes (sin IA)**

- FR-14: El sistema debe dividir de forma determinística el guion en escenas, cada escena en clips y
  cada clip en acción, líneas de diálogo (personaje, acotación opcional y texto) y texto en
  pantalla, y extraer el encabezado y los cuatro campos de cada escena (RF-04a–d).
- FR-15: El sistema debe identificar sin IA los personajes con hoja presentes en cada clip: el
  nombre de su hoja aparece como palabra completa en la acción escrito exactamente como en la hoja,
  o como marcador de diálogo en mayúsculas (RF-05a).
- FR-16: El sistema debe identificar sin IA los personajes presentes en cada escena: los presentes
  en alguno de sus clips más los nombrados en `PUESTA EN ESCENA:` con la misma regla (RF-05b).

**Generación con Gemini**

- FR-17: El sistema debe dejar en estado "pendiente", sin llamar a Gemini, las escenas de un guion
  recién cargado (RF-06d).

## Non-Functional Requirements

- NFR-01: Validación, división y detección de personajes de un guion de tamaño máximo (10 escenas ×
  20 clips): < 1 s p95 sobre 100 ejecuciones en el equipo local (RNF-03).
- NFR-02: Validación, división y detección de personajes: 0 llamadas a Gemini.

## Acceptance Criteria

*(EARS. "La escena de ejemplo" es la escena válida del Anexo A del PRD base, con las hojas "Ana" y
"José Daniel".)*

**Series, personajes y episodios**

- AC-01 (FR-01, FR-02): WHEN se crean las series "Serie A" y "Serie B" y se abre el listado de
  series, THE sistema SHALL mostrar las dos y ofrecerlas para elegir al cargar un guion.
- AC-02 (FR-03): WHEN se crea una hoja de personaje en una serie existente con todos los campos,
  THE sistema SHALL guardarla en esa serie con exactamente esos valores.
- AC-03 (FR-03): WHEN se crea una hoja de personaje con todos los campos salvo heridas/marcas, THE
  sistema SHALL guardarla.
- AC-04 (FR-03): IF se intenta crear una hoja de personaje con el peinado vacío, THEN THE sistema
  SHALL no guardarla e indicar que falta el peinado.
- AC-05 (FR-04): WHEN se abre la lista de personajes de una serie con dos hojas, THE sistema SHALL
  mostrar las dos.
- AC-06 (FR-05, FR-06): WHEN se crean en una serie el episodio 1 "Piloto" y el episodio 2 "La cena"
  y se abre la lista de episodios, THE sistema SHALL mostrar los dos y ofrecerlos para elegir al
  cargar un guion.
- AC-07 (FR-05): IF se intenta crear en una serie un episodio con un número que ya existe en ella,
  THEN THE sistema SHALL no guardarlo e indicar que el número ya existe.

**Carga y validación del guion**

- AC-08 (FR-07): WHEN se eligen una serie y un episodio existentes, se pega un guion válido y se
  confirma la carga, THE sistema SHALL guardar el guion asociado a ese episodio.
- AC-09 (FR-07): IF no se eligió serie o episodio, THEN THE sistema SHALL no permitir confirmar la
  carga y no guardar nada.
- AC-10 (FR-08): WHEN se pega un guion válido en el campo de texto y, sin ninguna otra interacción,
  se hace un único clic en «Validar y guardar», THE sistema SHALL guardar ese guion con ese único
  clic.
- AC-11 (FR-09): WHEN se carga un guion válido de 2 escenas en un episodio que tiene un guion de 3
  escenas generadas, THE sistema SHALL dejar el episodio con el guion nuevo y solo sus 2 escenas, y
  sin escenas, clips ni bloques del guion anterior en la base de datos.
- AC-12 (FR-09, FR-13): IF se intenta cargar un guion inválido en un episodio con un guion válido
  guardado, THEN THE sistema SHALL conservar el guion anterior con sus escenas, clips y bloques.
- AC-13 (FR-10, FR-11, FR-12): IF a la escena 1 le falta la línea `AUDIO:` y la escena 2 no tiene
  ningún `CLIP N`, THEN THE sistema SHALL mostrar los dos errores con su motivo, el primero en la
  línea del encabezado `ESCENA 1` y el segundo en la línea del encabezado `ESCENA 2`.
- AC-14 (FR-10, FR-12): IF el guion está vacío o no contiene ningún encabezado `ESCENA`, THEN THE
  sistema SHALL rechazarlo con un error en la línea 1.
- AC-15 (FR-10, FR-12): IF el guion tiene 11 escenas, THEN THE sistema SHALL rechazarlo con un
  error que indique el límite de 10 escenas en la línea del encabezado de la escena 11.
- AC-16 (FR-10): IF una escena tiene 21 clips, THEN THE sistema SHALL rechazar el guion con un
  error que indique el límite de 20 clips en la línea del `CLIP 21`.
- AC-17 (FR-10): IF el guion tiene `ESCENA 1` seguida de `ESCENA 3`, o una escena con dos líneas
  `CLIP 1`, THEN THE sistema SHALL rechazarlo con un error en la línea de `ESCENA 3` o del segundo
  `CLIP 1`, respectivamente.
- AC-18 (FR-10): IF el guion tiene una línea de texto antes del primer `ESCENA`, o un encabezado
  `ESCENA 1 — I/E RESTAURANTE — NOCHE`, THEN THE sistema SHALL rechazarlo con un error en la línea
  de ese texto o de ese encabezado, respectivamente.
- AC-19 (FR-11): IF un clip contiene `ANA: Mentiroso.` o `TEXTO EN PANTALLA: Hola` (sin comillas),
  THEN THE sistema SHALL rechazar el guion con un error en la línea de esa línea.
- AC-20 (FR-11): IF una escena tiene una segunda línea `LOCACIÓN:`, o `ILUMINACIÓN:` antes de
  `LOCACIÓN:`, o `AUDIO:` sin texto, THEN THE sistema SHALL rechazar el guion con un error en la
  línea de la segunda `LOCACIÓN:`, de `ILUMINACIÓN:` o de ese `AUDIO:`, respectivamente.
- AC-21 (FR-11): IF una escena tiene una línea de texto entre `AUDIO:` y `CLIP 1`, un `CLIP 2` sin
  contenido o un clip con dos `TEXTO EN PANTALLA:`, THEN THE sistema SHALL rechazar el guion con un
  error en la línea de ese texto, de `CLIP 2` o del segundo `TEXTO EN PANTALLA:`, respectivamente.
- AC-22 (FR-13): IF se intenta cargar un guion inválido en un episodio sin guion, THEN THE sistema
  SHALL dejar el episodio sin guion y sin ninguna escena, clip ni bloque guardado.

**División y detección de personajes**

- AC-23 (FR-14): WHEN se procesa la escena de ejemplo, THE sistema SHALL obtener la escena 1, INT,
  lugar "RESTAURANTE", momento "NOCHE", los cuatro campos iguales al texto que sigue a cada
  marcador, 2 clips y, en el clip 1, las dos líneas sin marcador como acción y los diálogos de ANA
  sin acotación con texto "Mentiroso." y de JOSÉ DANIEL con acotación "sonriendo" y texto
  "Bueno... pasó parecido.".
- AC-24 (FR-14): WHEN se procesa la escena de ejemplo escrita con `-` en lugar de `—` en el
  encabezado y con líneas en blanco entre campos y entre clips, THE sistema SHALL aceptarla y
  obtener un resultado igual campo por campo al de la escena de ejemplo original.
- AC-25 (FR-14): WHEN se procesa un clip con `TEXTO EN PANTALLA: "Hermano, ¿puedes salir
  conmigo?"`, THE sistema SHALL guardar "Hermano, ¿puedes salir conmigo?" como texto en pantalla
  del clip y no incluir esa línea en la acción ni en el diálogo.
- AC-26 (FR-15, FR-16): WHEN la serie tiene la hoja "Ana" y un clip menciona "Ana" en la acción o
  contiene la línea `ANA: "Claro."`, THE sistema SHALL incluir a Ana en los personajes de ese clip
  y de su escena.
- AC-27 (FR-15): IF la acción de un clip menciona "Anabel" o "Jose Daniel" (sin tilde) y el clip
  no tiene diálogo de `ANA:` ni de `JOSÉ DANIEL:`, THEN THE sistema SHALL no incluir a Ana ni a José
  Daniel en los personajes de ese clip.
- AC-28 (FR-15, FR-16): WHEN un clip contiene `MESERO: "¿Algo más?"` y la serie no tiene hoja
  "Mesero", THE sistema SHALL aceptar el guion, guardar la línea como diálogo del clip y no incluir
  a "Mesero" en los personajes del clip ni de la escena.
- AC-29 (FR-16): WHEN la `PUESTA EN ESCENA:` de una escena menciona "Ana" y ninguno de sus clips la
  menciona ni tiene diálogo de `ANA:`, THE sistema SHALL incluir a Ana en los personajes de la
  escena y en los de ningún clip.

**Generación con Gemini**

- AC-30 (FR-17): WHEN se carga un guion válido de 2 escenas, THE sistema SHALL dejar las 2 escenas
  en estado "pendiente" sin hacer ninguna llamada a Gemini.

## Out of Scope

- Generación con Gemini, reintentos y estados más allá de "pendiente" (FEAT-001b).
- Armado y copia del prompt final, y vista del prompt en el episodio (FEAT-001c).
- Editar o eliminar series, hojas de personaje y episodios (RF-01c, RF-01d, RF-01g, RF-01h,
  RF-11c, RF-11d).
- Variantes de personaje por escena (RF-09).
- Normalización automática de guiones mal formateados: se rechazan completos.
- Volver a detectar personajes de un guion ya cargado al crear una hoja nueva; se vuelve a cargar
  el guion (FR-09).
- Multiusuario y login (v1 monousuario y local).

## Risks and Mitigations

| Riesgo | Mitigación |
| --- | --- |
| Guion mal formateado por el guionista | FR-12 devuelve todos los errores con línea y motivo |
| Ambigüedad en el Anexo A del PRD base sobre un caso no listado | Se resuelve en PLAN con la usuaria antes de codificar; ningún caso se normaliza en silencio |
| Pérdida del primer clic al pegar el guion | Binding en `oninput` (FR-08) cubierto por AC-10 |
| Sobrescribir un guion borra trabajo generado | Solo ocurre con un guion nuevo válido (AC-11, AC-12) |

## Dependencies

- **PRD base** `docs/daw/prd/PRD.md`: Anexo A (formato, errores y línea reportada; FR-10 a FR-14)
  y Anexo B (modelo de datos; FR-03, FR-05).
- **SQLite + EF Core** para persistir series, hojas, episodios, guiones, escenas y clips (FR-01 a
  FR-09).
- **Blazor Web App (Interactive Server)** para la UI y el binding `oninput` (FR-08).

## Trazabilidad con FEAT-001

Los IDs de este PRD coinciden con los de `prd-FEAT-001.md` (aprobado el 2026-10-27): FR-01 a FR-17
y AC-01 a AC-30. NFR-01 = NFR-01 de FEAT-001; NFR-02 = parte de NFR-06 de FEAT-001.
