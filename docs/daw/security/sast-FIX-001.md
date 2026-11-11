# SAST — FIX-001: Marcadores de diálogo con - ' Ç se tratan como acción

- **Fecha:** 2026-11-11
- **Fase:** CODE (cierre)
- **Alcance:** archivos cambiados por el fix: `src/RodajeIA.Web/Guiones/NombrePersonaje.cs` (nuevo),
  `Guiones/GuionParser.cs`, `Guiones/DetectorPersonajes.cs`, `Servicios/ServicioPersonajes.cs`, los
  tests de `tests/RodajeIA.Tests/Guiones/` y `Servicios/`, y las dependencias NuGet de `RodajeIA.Web` y
  `RodajeIA.Tests`.
- **Threat model:** `docs/daw/security/threat-FIX-001.md`
- **Resultado:** **PASSED** — 0 Critical, 0 High, 0 Medium, 1 Low.

## Resultados por regla

| ID | Categoría | Resultado | Evidencia |
|---|---|---|---|
| F-SAST-01 | Secretos hardcodeados | ✅ Limpio | Sin patrones de API key/password/token/connection string en los archivos cambiados. |
| F-SAST-02 | Inyección SQL | ✅ Limpio | `ServicioPersonajes` sigue usando solo LINQ de EF Core; el nombre nuevo con `'` se guarda parametrizado (M-04). |
| F-SAST-03 | Inyección de comandos | ✅ Limpio | Sin `Process.Start`. |
| F-SAST-04 | Deserialización insegura | ✅ Limpio | Sin deserialización en los archivos cambiados. |
| F-SAST-05 | Path traversal | ✅ Limpio | Sin acceso a archivos. |
| F-SAST-06 | XSS | ✅ Limpio | Sin `MarkupString` en `src/`; la UI no cambia y muestra el nombre con `@` (M-02 de FEAT-001a). |
| F-SAST-07 | SSRF | ✅ N/A | Sin llamadas de red. |
| F-SAST-08 | Criptografía rota | ✅ N/A | Sin criptografía. |
| F-SAST-09 | Modo debug en producción | ✅ Limpio | Sin cambios de configuración. |
| F-SAST-10 | Log de datos sensibles | ✅ Limpio | El único log tocado (`ServicioPersonajes.cs:110`, preexistente) registra solo `SerieId`; el rechazo por caracteres no se registra. |
| F-SAST-11 | Subida sin restricciones | ✅ N/A | Sin subida de archivos. |
| F-SAST-12 | CSRF | ✅ Limpio | Sin cambios en el pipeline (`UseAntiforgery` activo). |
| F-SAST-13 / F-SAST-16 | CVEs en dependencias | ✅ Limpio | No se agregan paquetes; `dotnet list package --vulnerable --include-transitive`: sin paquetes vulnerables en ambos proyectos. |
| F-SAST-14 | Validación de entrada incompleta | ✅ Limpio | El nombre de la hoja se valida en el servicio con lista de permitidos (`EsNombreDeHojaValido`, RF-01i) además de largo y NFC; el marcador del guion se valida en el parser con regex `NonBacktracking` (M-01). |
| F-SAST-15 | Errores que filtran internos | ✅ Limpio | El rechazo nuevo devuelve un `Error` con mensaje propio en `Campo: "Nombre"`. |
| F-SAST-17 | Funciones inseguras | ✅ Limpio | Sin `eval`/`dynamic`/reflexión. |

## Hallazgos

### L-01 — Regex del detector sin `NonBacktracking` (🟢 Low, WARNING, no bloquea)

- **Archivo:** `src/RodajeIA.Web/Guiones/DetectorPersonajes.cs:85` (preexistente, no cambiada por el fix).
- **Detalle:** el patrón de la acción usa lookbehind/lookahead, que `NonBacktracking` no admite. El
  nombre ahora puede traer `'`, `’`, `.` y `-`, pero entra con `Regex.Escape` y con largo ≤ 60, y no
  tiene cuantificadores: la búsqueda es lineal en el texto. Cubierto por M-01 del threat model y por
  los tests de rendimiento del parser.

## Supresiones

Ninguna.
