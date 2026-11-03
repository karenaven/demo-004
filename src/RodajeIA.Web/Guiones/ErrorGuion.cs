namespace RodajeIA.Web.Guiones;

/// <summary>
/// Error de validación del guion: la línea en que se detectó (desde 1) y el motivo legible.
/// </summary>
/// <param name="Linea">Número de línea del guion, desde 1. Las líneas en blanco cuentan.</param>
/// <param name="Motivo">Descripción del problema, pensada para mostrarse al usuario.</param>
public sealed record ErrorGuion(int Linea, string Motivo);
