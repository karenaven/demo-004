namespace RodajeIA.Web.Servicios;

/// <summary>Serie tal como la ve la UI.</summary>
public sealed record SerieResumen(int Id, string Nombre);

/// <summary>Hoja de personaje tal como la ve la UI.</summary>
public sealed record PersonajeResumen(
    int Id,
    int SerieId,
    string Nombre,
    string Edad,
    string DescripcionFisica,
    string Peinado,
    string Vestuario,
    string? HeridasMarcas,
    string Personalidad,
    string Rol);

/// <summary>Episodio tal como lo ve la UI.</summary>
public sealed record EpisodioResumen(int Id, int SerieId, int Numero, string Titulo);

/// <summary>Datos del formulario de hoja de personaje. <see cref="HeridasMarcas"/> es opcional.</summary>
public sealed record DatosPersonaje(
    string Nombre,
    string Edad,
    string DescripcionFisica,
    string Peinado,
    string Vestuario,
    string? HeridasMarcas,
    string Personalidad,
    string Rol);

/// <summary>Resumen de un guion guardado: cantidad de escenas y de clips.</summary>
public sealed record ResumenCarga(int Escenas, int Clips);
