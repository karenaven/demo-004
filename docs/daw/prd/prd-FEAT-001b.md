# PRD FEAT-001b: Generación con Gemini validada y con reintentos

| Field | Value |
|-------|-------|
| Ticket | FEAT-001b |
| Tracker | none |
| Date | 2026-10-27 |
| PRD loops | 0 |
| Padre | `docs/daw/prd/prd-FEAT-001.md` (índice) |
| PRD base | `docs/daw/prd/PRD.md` (PRD-003 v3) |

## Context and Problem

Segundo sub-ticket de FEAT-001. Sobre las escenas "pendiente" que deja FEAT-001a, genera con Gemini
un bloque de tomas por clip, con los campos técnicos del vocabulario cerrado, valida cada respuesta,
reintenta y guarda el progreso por escena.

**Correcciones del repositorio anterior que aplican aquí (correcciones 1 a 3 del profesor):**

1. **La IA copia la hoja en la acción.** En una escena la acción empezaba con «Ana, 32 años. Mujer
   latina de piel trigueña…» y terminaba en «Rol: Protagonista espera bajo el toldo». La
   instrucción sola no alcanza: se valida la respuesta y se reintenta (FR-05, RN-11).
2. **Gemini devolvió el texto del diálogo en vez del id** (`c1-d1`) y se gastaron 2 de los 3
   intentos. El schema se arma por llamada con los ids como enum (FR-03).
3. **Se pierden las tildes del nombre** («Jose Daniel sonrie picaramente»). Se corrige sin IA y
   sin consumir intentos (FR-06).

## Goals

1. Al pulsar «Generar», cada escena "pendiente" termina "generada" con un bloque por clip, o en
   "error" tras agotar sus intentos, sin perder lo ya generado.
2. Una escena de 20 clips con diálogo no agota intentos por errores que el sistema puede prevenir
   (ids como enum) o corregir (tildes del nombre).
3. La acción guardada nunca contiene campos de la hoja (base de la consistencia del objetivo 1 de
   FEAT-001).

Sin variantes en este ticket, la **hoja efectiva** de toda escena es la hoja base.

## Functional Requirements

Entre paréntesis, el requerimiento equivalente del PRD base cuando existe.

**Generación con Gemini**

- FR-01: El sistema debe ofrecer en el episodio un botón «Generar» que genere todas sus escenas en
  estado "pendiente" (RF-06c).
- FR-02: El sistema debe generar con Gemini, para cada escena, un bloque por clip; cada bloque es
  una lista de tomas cuyos plano, óptica, iluminación, ángulo y movimiento salen de
  `config/vocabulario.json` mediante salida estructurada con enums (RF-06a, RN-02).
- FR-03: El sistema debe construir el schema de salida en cada llamada, con el campo `dialogos` de
  cada toma restringido por enum a los ids de las líneas de diálogo de esa escena; si la escena no
  tiene diálogo, el schema no debe admitir ningún id (corrección 2).
- FR-04: El sistema debe validar cada respuesta contra RN-08 (un bloque por clip, cada número de
  clip una vez), RN-09 (cada línea de diálogo del clip referenciada exactamente una vez, solo con
  ids de ese clip) y RN-10 (al menos una toma por bloque y acción no vacía), y tratar como fallida
  la respuesta que no los cumpla (RF-06b).
- FR-05: El sistema debe tratar como fallida una respuesta en la que la acción de alguna toma
  contenga (RN-11, corrección 1):
  - una etiqueta de campo de hoja: `Edad:`, `Descripción física:`, `Peinado:`, `Vestuario:`,
    `Heridas/marcas:`, `Personalidad:` o `Rol:`, sin distinguir mayúsculas ni tildes; o
  - el valor completo de la descripción física, la personalidad o el rol de un personaje presente
    en la escena, sin distinguir mayúsculas ni tildes.
  La edad, el peinado, el vestuario y las heridas/marcas no se comparan por valor.
- FR-06: Antes de validar y guardar, el sistema debe reemplazar en la acción de cada toma toda
  palabra o secuencia de palabras completa que empiece con mayúscula y que coincida con el nombre de
  un personaje con hoja de la serie salvo por las tildes, por el nombre exacto de la hoja. Esta
  corrección es determinística, no consume intentos y no toca ninguna otra palabra (corrección 3).
- FR-07: El sistema debe reintentar la generación de una escena ante error de Gemini, tiempo de
  espera agotado o respuesta fallida, según NFR-02 y NFR-03 (RNF-07).

**Progreso de la generación**

- FR-08: El sistema debe mantener por escena un estado de generación: "pendiente" al cargarse el
  guion, "generando" mientras se genera, "generada" cuando sus bloques quedan guardados y "error"
  según FR-10 (RF-10d).
- FR-09: El sistema debe guardar los bloques de cada escena en cuanto se generan, de modo que el
  fallo de otra escena no los borre (RF-10a).
- FR-10: El sistema debe marcar como "error" una escena cuya generación falla después de agotar
  los intentos de NFR-03 (RF-10b).
- FR-11: Al iniciar la aplicación, el sistema debe pasar a "error" toda escena que haya quedado en
  "generando" (RF-10e).

## Non-Functional Requirements

Tiempos medidos como p95 en el equipo local donde corre la app.

- NFR-01: Generación con Gemini de una escena de 20 clips, sin reintentos: < 30 s p95 (RNF-05;
  spike del 2026-10-04: 22,2 s p95).
- NFR-02: Tiempo máximo de espera por llamada a Gemini: 60 s (RNF-06).
- NFR-03: Reintentos por escena: 2 reintentos (3 intentos en total), con espera de 10 s antes del
  primero y de 30 s antes del segundo (RNF-07).
- NFR-04: Corrección de nombres (FR-06) y validación de respuestas (FR-04, FR-05): 0 llamadas
  adicionales a Gemini.

## Acceptance Criteria

*(EARS. Gemini se sustituye por un doble detrás de `IClienteGemini` en los tests.)*

**Generación con Gemini**

- AC-01 (FR-01, FR-08): WHEN se pulsa «Generar» en un episodio con la escena 1 "generada" y la
  escena 2 "pendiente" y Gemini responde bien, THE sistema SHALL hacer una sola llamada a Gemini,
  para la escena 2, dejarla "generada" y conservar los bloques de la escena 1.
- AC-02 (FR-02): WHEN se genera una escena de 3 clips y Gemini responde bien en el primer intento,
  THE sistema SHALL guardar 3 bloques cuyas tomas tienen plano, óptica e iluminación del
  vocabulario, y ángulo y movimiento vacíos o del vocabulario.
- AC-03 (FR-03): WHEN se genera una escena cuyas líneas de diálogo tienen los ids `c1-d1`, `c1-d2`
  y `c2-d1`, THE sistema SHALL enviar a Gemini un schema cuyo campo `dialogos` admite solo esos tres
  valores.
- AC-04 (FR-03): WHEN se genera una escena sin ninguna línea de diálogo, THE sistema SHALL enviar a
  Gemini un schema cuyo campo `dialogos` no admite ningún id.
- AC-05 (FR-04, FR-07): IF la respuesta de Gemini para una escena de 3 clips trae 2 bloques, o 3
  bloques para los clips 1, 1 y 3, THEN THE sistema SHALL tratarla como fallida y reintentar.
- AC-06 (FR-04): IF la respuesta de Gemini omite una línea de diálogo de un clip, la referencia en
  dos tomas o referencia un id de otro clip, THEN THE sistema SHALL tratarla como fallida y
  reintentar.
- AC-07 (FR-04): IF la respuesta de Gemini trae un bloque sin tomas o una toma con acción vacía,
  THEN THE sistema SHALL tratarla como fallida y reintentar.
- AC-08 (FR-05): IF la acción de una toma es «Ana, 32 años. Mujer latina de piel trigueña. Rol:
  Protagonista espera bajo el toldo.», THEN THE sistema SHALL tratar la respuesta como fallida y
  reintentar.
- AC-09 (FR-05): IF la acción de una toma contiene, sin ninguna etiqueta, el valor completo de la
  descripción física de Ana, presente en la escena, THEN THE sistema SHALL tratar la respuesta como
  fallida y reintentar.
- AC-10 (FR-05): WHEN la acción de una toma es «Ana se quita la chaqueta negra.» y el vestuario de
  la hoja de Ana es "chaqueta negra", THE sistema SHALL no tratar la respuesta como fallida por ese
  motivo.
- AC-11 (FR-06): WHEN Gemini devuelve la acción «Jose Daniel sonrie picaramente.» y la serie tiene
  la hoja "José Daniel", THE sistema SHALL guardar «José Daniel sonrie picaramente.» sin consumir
  un intento.
- AC-12 (FR-06): WHEN la serie tiene la hoja "Luz" y Gemini devuelve la acción «La luz cálida
  ilumina la mesa y ana sonríe.», THE sistema SHALL guardar la acción sin cambios.
- AC-13 (FR-07, FR-10): IF Gemini devuelve error en los 3 intentos de la escena 3, THEN THE sistema
  SHALL hacer exactamente 3 intentos para la escena 3 y dejarla en estado "error".
- AC-14 (FR-07): IF Gemini devuelve error en el primer intento de una escena y responde bien en el
  segundo, THEN THE sistema SHALL dejar la escena "generada" con sus bloques.

**Progreso de la generación**

- AC-15 (FR-09): IF las escenas 1 y 2 se generaron bien y la escena 3 falla en sus 3 intentos, THEN
  THE sistema SHALL conservar los bloques de las escenas 1 y 2.
- AC-16 (FR-08): WHILE la llamada a Gemini de una escena no respondió, WHEN se abre el episodio, THE
  sistema SHALL mostrar esa escena en estado "generando".
- AC-17 (FR-11): WHEN se inicia la aplicación con una escena guardada en estado "generando", THE
  sistema SHALL pasarla a estado "error".

## Out of Scope

- Armado y copia del prompt final, y su vista en el episodio (FEAT-001c).
- Reintentar a mano una escena en "error" (RF-10c); para regenerar se vuelve a cargar el guion.
- Regenerar una escena "generada" y cancelar una generación en curso.
- Revisión y edición de tomas (RF-07).
- Variantes de personaje (RF-09).
- Corregir en la acción otras tildes u ortografía que no sean el nombre de un personaje con hoja
  («sonrie» queda como vino), o nombres en minúscula («ana»).
- Comparar por valor la edad, el peinado, el vestuario y las heridas/marcas en la acción (FR-05).
- Cargar un guion nuevo mientras se generan las escenas del anterior.

## Risks and Mitigations

| Riesgo | Mitigación |
| --- | --- |
| La IA copia la hoja en la acción | La instrucción lo prohíbe y FR-05 lo rechaza y reintenta |
| La IA devuelve el texto del diálogo en vez del id | Schema por llamada con los ids como enum (FR-03) |
| La IA pierde las tildes del nombre | Corrección determinística sin consumir intentos (FR-06) |
| FR-05 rechaza demasiado y agota intentos | Solo etiquetas y los tres campos largos; vestuario y campos cortos fuera (AC-10) |
| `config/instruccion-generacion.txt` dice que un nombre mal escrito «se rechaza», pero FR-06 lo corrige | Se acepta: el texto disuade a la IA; el comportamiento lo fija FR-06 |
| Un nombre de hoja que es palabra común («Luz») se reemplaza | Solo se corrigen diferencias de tildes con mayúscula inicial; nunca se toca una minúscula (AC-12) |
| Error 503 de Gemini por alta demanda (1 de 10 en el spike) | Reintentos de NFR-03; la escena va a "error" sin afectar a las demás (AC-15) |
| La app se cierra a mitad de generación | Al iniciar, "generando" pasa a "error" (FR-11) |

## Dependencies

- **FEAT-001a**: episodios con guion, escenas en "pendiente", clips con ids de diálogo y personajes
  detectados por escena (FR-01, FR-02, FR-05).
- **Gemini** (`gemini-3.1-flash-lite`) con salida estructurada y enums en el schema, detrás de
  `IClienteGemini` (FR-02, FR-03, FR-07); API key en user-secrets (`Gemini:ApiKey`).
- **`config/vocabulario.json`** (FR-02, FR-03) y **`config/instruccion-generacion.txt`** (FR-02,
  FR-05). Hoy existen en disco sin commitear: quedan versionados a más tardar con este ticket.
- **PRD base** `docs/daw/prd/PRD.md`: Anexo C (RN-02, RN-08 a RN-10) y Anexo D (salida
  estructurada).
- **SQLite + EF Core** para estados y bloques (FR-08 a FR-11).

## Trazabilidad con FEAT-001

FR-01 a FR-11 = FR-18 a FR-28 de `prd-FEAT-001.md`; AC-01 a AC-17 = AC-31 a AC-47; NFR-01 a NFR-03
= NFR-03 a NFR-05; NFR-04 = parte de NFR-06. RN-11 se define en FR-05.
