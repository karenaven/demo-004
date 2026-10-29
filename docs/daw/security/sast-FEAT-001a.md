# SAST — FEAT-001a: Guion sin IA — series, hojas, episodios, carga y validación

- **Fecha:** 2026-10-29
- **Fase:** CODE (cierre)
- **Alcance:** `src/RodajeIA.Web/` (Guiones/, Servicios/, Datos/ excepto Migraciones/, Components/, Program.cs,
  appsettings*.json) y dependencias NuGet de `RodajeIA.Web` y `RodajeIA.Tests`.
- **Resultado:** **PASSED** — 0 Critical, 0 High, 0 Medium, 1 Low.

## Resultados por regla

| ID | Categoría | Resultado | Evidencia |
|---|---|---|---|
| F-SAST-01 | Secretos hardcodeados | ✅ Limpio | Sin patrones de API key/password/token en `src/`. `appsettings.json` solo tiene `Data Source=rodajeia.db` (ruta local, no secreto). La API key de Gemini va por user-secrets. `*.db` en `.gitignore`; no hay `.env`, `.db`, `.pfx` ni `.key` versionados. |
| F-SAST-02 | Inyección SQL | ✅ Limpio | Solo LINQ de EF Core; sin `FromSql*`, `ExecuteSql*` ni `SqlQuery` fuera de `Migraciones/`. |
| F-SAST-03 | Inyección de comandos | ✅ Limpio | Sin `Process.Start`. |
| F-SAST-04 | Deserialización insegura | ✅ Limpio | Sin `BinaryFormatter` ni `TypeNameHandling`. |
| F-SAST-05 | Path traversal | ✅ Limpio | Sin acceso a archivos con entrada del usuario (`File.*`, `Path.Combine`, `Directory.*`). |
| F-SAST-06 | XSS | ✅ Limpio | Sin `MarkupString`, `innerHTML` ni `JSRuntime` en `Components/`; el texto del usuario se renderiza con `@expresión`, codificado por Blazor (M-02). |
| F-SAST-07 | SSRF | ✅ Limpio | Sin `HttpClient`/`WebRequest` en este ticket. |
| F-SAST-08 | Criptografía rota | ✅ Limpio | Sin MD5/SHA1/DES/Rijndael ni `Random` en usos de seguridad. |
| F-SAST-09 | Modo debug en producción | ✅ Limpio | `DetailedErrors` solo en Development (`Program.cs`); `UseExceptionHandler` + HSTS fuera de Development (M-05). |
| F-SAST-10 | Log de datos sensibles | ✅ Limpio | Los logs de `Servicios/` registran solo ids y conteos (episodio, escenas, clips, número de errores); nunca el contenido del guion ni campos de la hoja. |
| F-SAST-11 | Subida sin restricciones | ✅ N/A | Sin `InputFile`/`IFormFile`; el guion entra como texto con `maxlength` 200 000 y validación en servicio. |
| F-SAST-12 | CSRF | ✅ Limpio | `app.UseAntiforgery()` activo; la UI es Interactive Server (eventos sobre el circuito SignalR). |
| F-SAST-13 / F-SAST-16 | CVEs en dependencias | ✅ Limpio | `dotnet list package --vulnerable --include-transitive`: sin paquetes vulnerables en ambos proyectos. |
| F-SAST-14 | Validación de entrada incompleta | ✅ Limpio | Validación autoritativa en `Servicios/` (longitudes, rangos, NFD, pertenencia episodio↔serie); `maxlength` en la UI solo como ayuda. Tamaño de mensaje del hub acotado a 1 MB. |
| F-SAST-15 | Errores que filtran internos | ✅ Limpio | Los servicios devuelven `Error` con mensaje propio y registran la excepción solo en el log; la UI no muestra `ex.Message` ni trazas. |
| F-SAST-17 | Funciones inseguras | ✅ Limpio | Sin `eval`/`dynamic`/reflexión sobre entrada. |

## Hallazgos

### L-01 — `AllowedHosts: "*"` (🟢 Low, WARNING, no bloquea)

- **Archivo:** `src/RodajeIA.Web/appsettings.json`
- **Detalle:** plantilla por defecto de ASP.NET Core; acepta cualquier cabecera `Host`. Sin impacto en
  uso local; conviene fijar el host real al desplegar.

## Fuera del alcance de SAST (registrado)

- Las excepciones de los `ListarAsync` de `Servicios/` llegan a la UI y cortan el circuito. No filtran
  información (F-SAST-15 limpio porque `DetailedErrors` está desactivado fuera de Development); es
  robustez, no seguridad. Registrado como deuda para un ticket posterior.

## Supresiones

Ninguna.
