# RodajeIA

App web para productoras/es de series generadas con IA: convierte guiones en bloques de tomas y
prompts listos para copiar en la IA de video.

## Cómo arrancar

Los comandos de instalación, configuración inicial, ejecución local, tests y linter están en la
sección **Stack** de [`AGENTS.md`](AGENTS.md). En resumen:

```bash
dotnet tool restore
dotnet restore
dotnet run --project src/RodajeIA.Web
dotnet test
dotnet format --verify-no-changes
```
