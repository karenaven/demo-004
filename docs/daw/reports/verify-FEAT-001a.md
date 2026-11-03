# Verificación — FEAT-001a: Guion sin IA — series, hojas, episodios, carga y validación

## Ronda 1 — 2026-10-29

- **Tier:** FEATURE
- **PRD:** `docs/daw/prd/prd-FEAT-001a.md`
- **Spec:** `docs/daw/specs/spec-FEAT-001a.md`
- **Rama:** `feat/FEAT-001a-guion-sin-ia` (HEAD `d7b7aad`)
- **Verificación cruzada:** agente `daw-module-verifier`, que no escribió el código
- **Veredicto:** **PASSED** — 0 FAIL, 7 WARNING

### Resultado por regla

| Regla | Resultado | Evidencia |
|---|---|---|
| F-VER-01 — AC con test | ✅ PASS | 30/30 AC con test en verde que comprueba comportamiento (datos releídos de la BD o DOM renderizado). 28 ✅ y 2 ⚠️ (AC-05, AC-28; ver WARNINGs). |
| F-VER-02 — Tareas de la spec | ✅ PASS | Bloques 1–6 implementados: archivos, lógica, validación de entrada y manejo de errores según la spec. |
| F-VER-03 — Cobertura ≥ 80 % | ✅ PASS | Código nuevo de `src/RodajeIA.Web` (sin `Datos/Migraciones/`): **líneas 92,1 %**, **ramas 84,5 %**, **funciones 92,9 %**. Detalle abajo. |
| F-VER-04 — Sad paths | ✅ PASS | Parser (22 errores + NFD + vacío/null), detector, `Resultado<T>`, los 4 servicios (vacíos, máximos, inexistentes, repetidos, carrera, fallo de BD, rollback) y formularios UI. |
| F-VER-05 — Lint / type checker | ✅ PASS | `dotnet format --verify-no-changes` exit 0; `dotnet build --no-incremental` 0 errores, 0 warnings. |
| F-VER-06 — Tests de la spec | ✅ PASS | 86/86 tests listados en la spec existen con su nombre exacto y pasan. Suite completa: 139/139. |
| W-VER-01 — Código muerto / usings | ✅ | Sin usings sin uso ni miembros sin referencias. `_Imports.razor` conserva usings de plantilla (sin impacto). |
| W-VER-02 — Lógica de negocio < 90 % | ✅ | `Guiones/` 97,8 % líneas / 93,8 % ramas; `Servicios/` 99,1 % / 96,9 %. |
| W-VER-03 — Tests frágiles | ⚠️ | Ver WARNINGs 4–5. |

### Cobertura

Medida con `dotnet-coverage` 18.12.0 (Cobertura) sobre `dotnet test` (139/139), ejecutada por la usuaria.

| Área | Líneas | Ramas | Funciones |
|---|---|---|---|
| `Guiones/` | 97,8 % (491/502) | 93,8 % (195/208) | 100 % (50/50) |
| `Servicios/` | 99,1 % (318/321) | 96,9 % (62/64) | 100 % (40/40) |
| `Datos/` (sin migraciones) | 100 % (135/135) | 100 % (24/24) | 100 % (26/26) |
| `Components/` | 77,1 % (202/262) | 58,2 % (57/98) | 80,0 % (28/35) |
| `Program.cs` | 0 % (0/24) | 0 % (0/6) | 0 % (0/4) |
| **Total** | **92,1 % (1146/1244)** | **84,5 % (338/400)** | **92,9 % (144/155)** |

Por página: `CargarGuion.razor` 53/58 líneas, 25/34 ramas; `DetalleSerie.razor` 116/151, 21/38;
`Series.razor` 33/46, 11/18. `Error.razor` y `MainLayout.razor` (plantilla) sin cubrir.

### Final verification de la spec

- ✅ Ningún componente de `Components/` referencia `RodajeDbContext`, `IDbContextFactory` ni `Datos`.
- ✅ `Guiones/` no referencia `Servicios/`, `Datos/`, `Generacion/` ni EF Core (también por reflexión en
  `ArquitecturaTests.Guiones_NoReferenciaGeneracionServiciosNiDatos`).
- ✅ `dotnet build`, `dotnet test` y `dotnet format --verify-no-changes` en verde.
- ✅ Recorrido manual de la demo completado por la usuaria (2026-10-29): crear serie → hojas Ana y José
  Daniel → episodio 1 → pegar la escena de ejemplo del Anexo A → un clic → «Guion guardado: 1 escenas en
  estado pendiente».

### Evidencia TDD

- Bloque 6: los 9 tests requeridos fallaron antes de implementar (con páginas stub), con aserciones
  verificadas por la revisión de spec del bloque.
- Bloques 1–5: revisados por bloque en sesiones anteriores de CODE, cuyo protocolo exige la evidencia para
  aprobar; los reportes no se persistieron en disco y no se pueden reconstruir desde la suite en verde.

### WARNINGs (no bloquean)

1. **Cobertura de UI baja en ramas.** `Components/` 58,2 % de ramas; `Program.cs` sin cubrir (arranque, no
   testeado por unit tests). El total cumple F-VER-03.
2. **AC-05** se verifica solo a nivel de servicio (`ListarPersonajes_DosHojas_DevuelveLasDos`); ningún
   bUnit renderiza la lista de dos hojas. La spec no lo exige.
3. **AC-28** «guardar la línea como diálogo» de un personaje sin hoja se cubre por composición; ningún test
   persiste ese diálogo.
4. **Sad paths menores sin test:** `ListarAsync` de personajes/episodios con serie inexistente; guards de
   `ResultadoParseo.Valido(null)` / `Invalido([])`; bUnit de nombre vacío en `Series.razor` (cubierto en
   servicio).
5. **Tests frágiles (W-VER-03):** `Rendimiento_*` dependen del reloj (exigidos por NFR-01, margen amplio);
   ids fijos `999`/`(1, 1)` en tests de servicios y `DetalleSerieTests:70` (cada test usa su propia SQLite
   en memoria, sin estado compartido); BOM literal invisible en `GuionParserTests.cs:91`.
6. **Fuera de los AC:** `MarcadorDialogo` (`GuionParser.cs:40`) no admite `-`, `'` ni `Ç` — `JOSÉ-LUIS: "x"`
   se clasifica como acción. Registrado como FIX pendiente junto con el BOM literal.
7. **Deuda de robustez:** los `ListarAsync` de `Servicios/` no capturan excepciones de BD y estas llegan a
   la UI (detectado en la revisión del bloque 6). Registrado para un ticket posterior.

### Observaciones

- Ningún checkbox de «Required tests» de la spec está marcado; los tests existen y pasan.
- Desviaciones respaldadas por ADR: rechazo de NFD y `\p{M}` en bordes del detector (ADR-001); nombres de
  hoja únicos en NFC (ADR-002); regex `NonBacktracking` en lugar de compiladas (más fuerte frente a M-06).
