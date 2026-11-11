# Verificación — FIX-001: Marcadores de diálogo con - ' Ç se tratan como acción

## Ronda 1 — 2026-11-11

- **Tier:** FIX
- **PRD:** `docs/daw/prd/PRD.md` (RF-01i, RF-04c, RF-05a, Anexo A)
- **Fix-plan:** `docs/daw/specs/fix-FIX-001.md`
- **RCA:** `docs/daw/specs/rca-FIX-001.md`
- **Rama:** `fix/FIX-001-marcadores-dialogo` (HEAD `fdc5779`)
- **Verificación cruzada:** agente `daw-module-verifier`, que no escribió el código
- **Veredicto:** **PASSED** — 0 FAIL, 4 WARNING

### Resultado por regla

| Regla | Resultado | Evidencia |
|---|---|---|
| F-VER-01 | ✅ | AC-01l y AC-01m → `ServicioPersonajes.cs:61-65` + `NombrePersonaje.EsNombreDeHojaValido` → `CrearPersonaje_NombreConCaracteresNoPermitidos_SeRechaza` (5 casos) y `CrearPersonaje_NombresConCaracteresPermitidos_SeGuardan` (6 casos). AC-03v → `GuionParser.cs:39-40` → `Parsear_MarcadorConGuionSinComillas_SeRechaza`. AC-05h/i/j → parser + `DetectorPersonajes.cs:50-52,88` → tests `Parsear_*` y `Detectar_*` de guion, apóstrofo con acotación y cedilla. AC-05k → `NombrePersonaje.ClaveDeMarcador` → `Detectar_ApostrofoTipograficoEnMarcador_DetectaHojaConApostrofoRecto` (detecta y conserva `’`). |
| F-VER-02 | ✅ | Los 6 pasos del fix-plan implementados: `NombrePersonaje.cs` (nuevo), `MarcadorDialogo` con el fragmento compartido, detector con `ClaveDeMarcador`, validación de caracteres solo tras NFC y largo (un único error) y `ClaveDeNombre` con `’`→`'`, BOM como escape (ningún U+FEFF literal en `src/` ni `tests/`), ADR-003. |
| F-VER-06 | ✅ | 19/19 tests del fix-plan existen y pasan (ver W-3 por una diferencia de forma). |
| Regresión | ✅ | Tests nuevos corridos contra el código de `fdc5779^`: fallan 18 (7 parser, 5 detector, 6 servicio) por las causas del RCA; con el fix pasan todos. |
| Suite completa | ✅ | `dotnet test`: 168/168, 0 fallidos, 0 omitidos. |
| F-VER-03 | ✅ | `dotnet-coverage` 18.12.0: `NombrePersonaje.cs` 100 % líneas y métodos; líneas cambiadas de `GuionParser`, `DetectorPersonajes` y `ServicioPersonajes` 100 % líneas, ramas y funciones. Archivos completos: `GuionParser` 97,7 % líneas / 95,8 % ramas, `DetectorPersonajes` 100 % / 95,5 %, `ServicioPersonajes` 99,0 % / 100 %. |
| F-VER-04 | ✅ | Sad paths de `CrearAsync` (5 nombres inválidos, vacío, largo, NFD, duplicado `'`/`’`) y del marcador (sin comillas, `OK. (pausa)`, `-ANA`/`1ANA`/`'ANA`). Ver W-2. |
| F-VER-05 | ✅ | `dotnet format --verify-no-changes`: exit 0; build sin warnings. |
| W-VER-01 | ✅ | Sin código muerto ni usings sin uso. |
| W-VER-02 | ✅ | Lógica de negocio tocada ≥ 90 %. |
| W-VER-03 | ⚠️ | Ver W-1. |
| Threat model | ✅ | M-01 a M-06 aplicadas (`threat-FIX-001.md`). |

### Warnings (no bloquean)

1. **W-1 (W-VER-03):** `Rendimiento_LineaPatologicaCaracteresNuevos_MenorA1Segundo` depende del tiempo
   (`Stopwatch` < 1 s), como los dos tests de rendimiento previos. Midió 8 ms: hay mucho margen,
   pero podría fallar en una CI muy cargada.
2. **W-2 (F-VER-04, valorado como WARN):** `NombrePersonaje` no tiene tests directos. Los
   `ArgumentNullException.ThrowIfNull` (`NombrePersonaje.cs:33,44`) no se prueban con `null`, y
   `EsNombreDeHojaValido` solo se prueba a través de `CrearAsync`. Las entradas inválidas sí se
   cubren de forma indirecta. Recomendación: `tests/RodajeIA.Tests/Guiones/NombrePersonajeTests.cs`.
3. **W-3 (forma de F-VER-06):** `CrearPersonaje_NombreQueNoEmpiezaConLetra_SeRechaza` no existe con
   ese nombre; sus casos (`-Ana`, `1Ana`, `'Ana`) están dentro del Theory
   `CrearPersonaje_NombreConCaracteresNoPermitidos_SeRechaza`.
4. **W-4 (alcance de AC-05k):** el test comprueba que se conserva el `’` en `LineaDialogoParseada`,
   no en la base de datos. `ServicioGuiones.cs:152,155` (no tocado por el fix) copia el valor sin
   cambios; falta una prueba de punta a punta.

### Notas

- El proyecto de tests no trae herramienta de cobertura (ni coverlet ni
  `Microsoft.Testing.Extensions.CodeCoverage`); la medición se hizo con `dotnet-coverage` instalado
  fuera del repo.
