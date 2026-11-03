# Parent PRD: Del guion al prompt final con personajes consistentes

| Metric | Value |
|--------|-------|
| Ticket | FEAT-001 |
| Date | 2026-10-27 |
| Status | Split |

## Sub-tickets

| Sub-ticket | Title | PRD | Dependencies | Status |
|---|---|---|---|---|
| FEAT-001a | Guion sin IA — series, hojas, episodios, carga y validación | prd-FEAT-001a.md | none | active |
| FEAT-001b | Generación con Gemini validada y con reintentos | prd-FEAT-001b.md | depends on a | pending |
| FEAT-001c | Prompt final por plantilla y vista del episodio | prd-FEAT-001c.md | depends on b | pending |

## Suggested implementation order

a → b → c

## Original context

Primera feature core de Rodaje IA: el recorrido mínimo completo del criterio de demo del PRD base
(`docs/daw/prd/PRD.md`): crear serie → crear personaje → crear episodio → pegar guion → pulsar
«Generar» → ver y copiar el prompt final, con cada personaje descrito de forma idéntica en todas las
escenas.

El PRD unificado (32 FR, 6 NFR, 55 AC) pasó `daw-validate-prd` y fue aprobado por la usuaria el
2026-10-27; por tamaño se dividió en tres sub-tickets. Incorpora cuatro correcciones del profesor
sobre el repositorio anterior:

| # | Falla observada | Requerimiento | Sub-ticket |
|---|---|---|---|
| 1 | La IA copia la hoja en la acción («Ana, 32 años… Rol: Protagonista…») | RN-11: rechazar y reintentar si la acción trae una etiqueta de hoja o el valor completo de descripción física, personalidad o rol | FEAT-001b (FR-05) |
| 2 | Gemini devolvió el texto del diálogo en vez del id `c1-d1` y gastó 2 de 3 intentos | Schema por llamada con los ids de diálogo de la escena como enum | FEAT-001b (FR-03) |
| 3 | Tildes perdidas en el nombre («Jose Daniel») | Corrección determinística del nombre con mayúscula inicial, sin consumir intentos | FEAT-001b (FR-06) |
| 4 | El primer clic en «Validar y guardar» se perdía (bind en `change`) | Campo del guion con `oninput` | FEAT-001a (FR-08) |
