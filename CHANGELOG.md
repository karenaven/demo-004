# Changelog

Todos los cambios relevantes de este proyecto se documentan en este archivo.

El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/).

## [Unreleased]

### Added

- [FEAT-001a] Esqueleto de la solución Blazor Web App (.NET 10) con `Resultado<T>` y modelo de datos EF Core sobre SQLite con migración inicial.
- [FEAT-001a] Alta y listado de series, hojas de personaje y episodios (número único por serie).
- [FEAT-001a] Carga del guion de un episodio como texto plano, con el valor actualizado en cada `input` para que un solo clic en «Validar y guardar» procese lo pegado.
- [FEAT-001a] Parser determinístico y validación del guion contra la convención de formato: un guion inválido se rechaza completo con la lista de errores (línea y motivo) y uno válido se guarda dividido en escenas y clips en estado «pendiente».
- [FEAT-001a] Detección determinística de personajes por nombre exacto, con rechazo de líneas en NFD.
- [FEAT-001a] Al recargar un guion válido se sobrescribe el anterior y se eliminan sus escenas, clips y bloques.
