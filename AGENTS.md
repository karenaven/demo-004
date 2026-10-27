# AGENTS.md — RodajeIA

## Idioma

Idioma de trabajo: **español**. Todos los artefactos (PRDs, specs, ADRs, reportes, mensajes de
commit) se escriben en español.

---

## Qué es este proyecto

App web para productoras/es de series generadas con IA: convierte guiones en bloques de tomas
(plano, óptica, iluminación, más ángulo y movimiento como opcionales) y prompts listos para copiar en
la IA de video, manteniendo consistentes a los personajes entre escenas y episodios.

**PRD de referencia:** `docs/daw/prd/PRD.md`

---

## Stack

| Campo | Valor |
|-------|-------|
| Lenguaje | C# |
| Runtime | .NET 10 |
| Framework | Blazor Web App (Interactive Server) |
| Base de datos | SQLite + EF Core |
| LLM | Gemini (Google) |
| Test runner | xUnit v3 |
| Linter / formatter | `dotnet format` (reglas en `.editorconfig`) |
| Gestor de paquetes | NuGet |

Solución: `RodajeIA.slnx` · App: `src/RodajeIA.Web` · Tests: `tests/RodajeIA.Tests`

```bash
# Instalar dependencias
dotnet tool restore
dotnet restore

# Configuración inicial (una vez)
dotnet user-secrets set "Gemini:ApiKey" "<tu-api-key>" --project src/RodajeIA.Web

# Levantar en local
dotnet ef database update --project src/RodajeIA.Web
dotnet run --project src/RodajeIA.Web

# Tests
dotnet test

# Linter
dotnet format --verify-no-changes
```

---

## Convenciones de arquitectura

- **Estructura de carpetas:** `src/RodajeIA.Web/` organizado por área: `Guiones/` (parser
  determinístico y detección de personajes), `Generacion/` (vocabulario, schema, cliente de Gemini,
  validación de respuestas, hoja efectiva, plantilla y armado del prompt, cola de generación),
  `Servicios/` (operaciones que usa la UI), `Datos/` (DbContext, entidades y `Migraciones/`),
  `Components/` (UI Blazor, con `Pages/` y `Layout/`). Los tests espejan esa estructura en
  `tests/RodajeIA.Tests/`.
- **Separación de capas:** los componentes Blazor no usan el `DbContext` ni llaman a Gemini; siempre
  pasan por un servicio de `Servicios/`.
- **Acceso a la IA:** Gemini queda detrás de la interfaz `IClienteGemini`, para poder sustituirlo
  por un doble en los tests.
- **Configuración como archivos:** `config/vocabulario.json`, `config/estilo.txt`,
  `config/instruccion-generacion.txt` y `config/molde-prompt.json`, versionados en el repo.
- **Manejo de errores:** los servicios devuelven `Resultado<T>` (valor o lista de errores); los
  errores del guion llevan número de línea y motivo (`ErrorGuion`).
- **Naming:** código, carpetas y tipos en español y PascalCase (`GuionParser`, `ArmadorPrompt`);
  interfaces con prefijo `I`.
- **Dependencias:** un paquete NuGet nuevo usa la última versión estable (nunca preview/beta). Los
  paquetes existentes no se actualizan sin preguntar.

---

## Qué NO hacer en este proyecto

- No usar IA para validar ni dividir el guion (escenas, campos de escena, clips,
  acción/diálogo/texto en pantalla) ni para detectar personajes (RF-03, RF-04, RF-05): son pasos
  determinísticos, testeables sin llamar al modelo. Un guion inválido se rechaza completo con línea
  y motivo; nunca se normaliza, corrige ni guarda en parte.
- No hardcodear ni inventar valores técnicos (RN-02): plano, óptica, iluminación, ángulo y
  movimiento (los dos últimos opcionales) salen solo del vocabulario JSON versionado en el repo, y
  el schema de salida estructurada de Gemini se genera desde ese archivo con cada campo como enum.
  No existen campos de cámara ni profundidad de campo: la cámara es fija y va en el estilo global.
  El único texto libre de la IA es la acción de cada toma.
- No dejar que la IA escriba lo que debe ser idéntico entre escenas (RN-03, RN-04): hojas de
  personaje (efectivas), diálogo con su acotación y texto en pantalla se insertan literales por
  plantilla. La IA solo los recibe como contexto y referencia las líneas de diálogo por id.

---

<!-- BEGIN DAW (managed by DAW — do not edit by hand) -->
# DAW — Dilux Agentic Workflow

This repo uses **DAW**: an agent-driven development pipeline with the phases
`CLASSIFY → DEFINE → PLAN → CODE → VERIFY → RELEASE`.

Before answering, read `.daw/orchestrator.md` and run its Boot Sequence. It is a strict state
machine: it decides what you are allowed to do based on the phase recorded in `.daw-state.json`.

The project's own context — stack, architecture, domain — is elsewhere in this file. It lives here,
in `AGENTS.md`, and not in any one tool's file, on purpose: it is tool-agnostic and comes along
unchanged when the pipeline is ported to another agent.
<!-- END DAW -->
