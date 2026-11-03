namespace RodajeIA.Web.Guiones;

/// <summary>
/// Interior o exterior según el encabezado de escena (<c>INT.</c> / <c>EXT.</c>). Es propio de
/// <c>Guiones/</c> para que el parser no dependa de <c>Datos/</c>; el servicio lo traduce al enum
/// de la entidad.
/// </summary>
public enum IntExt
{
    Int,
    Ext,
}

/// <summary>Guion válido dividido en escenas, en orden.</summary>
public sealed record GuionParseado(IReadOnlyList<EscenaParseada> Escenas);

/// <summary>Escena con su encabezado, sus cuatro campos y sus clips.</summary>
/// <param name="Numero">Número de la escena (desde 1).</param>
/// <param name="LineaEncabezado">Línea del guion en que está el encabezado.</param>
/// <param name="IntExt">Interior o exterior.</param>
/// <param name="Lugar">Texto entre el primer y el último separador del encabezado.</param>
/// <param name="Momento">Texto que sigue al último separador del encabezado.</param>
/// <param name="Locacion">Texto que sigue a <c>LOCACIÓN:</c>.</param>
/// <param name="Iluminacion">Texto que sigue a <c>ILUMINACIÓN:</c>.</param>
/// <param name="PuestaEnEscena">Texto que sigue a <c>PUESTA EN ESCENA:</c>.</param>
/// <param name="Audio">Texto que sigue a <c>AUDIO:</c>.</param>
/// <param name="Clips">Clips de la escena, en orden.</param>
public sealed record EscenaParseada(
    int Numero,
    int LineaEncabezado,
    IntExt IntExt,
    string Lugar,
    string Momento,
    string Locacion,
    string Iluminacion,
    string PuestaEnEscena,
    string Audio,
    IReadOnlyList<ClipParseado> Clips);

/// <summary>Clip con su acción, sus líneas de diálogo y su texto en pantalla opcional.</summary>
/// <param name="Numero">Número del clip dentro de la escena (desde 1).</param>
/// <param name="Linea">Línea del guion en que está el marcador <c>CLIP N</c>.</param>
/// <param name="LineasAccion">Líneas sin marcador, en orden, sin espacios en los extremos.</param>
/// <param name="Dialogos">Líneas de diálogo, en orden.</param>
/// <param name="TextoEnPantalla">Texto en pantalla sin comillas, o null.</param>
/// <param name="TextoEnPantallaOriginal">Texto en pantalla con sus comillas originales, o null.</param>
public sealed record ClipParseado(
    int Numero,
    int Linea,
    IReadOnlyList<string> LineasAccion,
    IReadOnlyList<LineaDialogoParseada> Dialogos,
    string? TextoEnPantalla,
    string? TextoEnPantallaOriginal);

/// <summary>Línea de diálogo de un clip.</summary>
/// <param name="Codigo">Id <c>c{clip}-d{n}</c>, único dentro de la escena.</param>
/// <param name="Orden">Posición dentro del clip, desde 1.</param>
/// <param name="Personaje">Marcador en mayúsculas, sin la acotación.</param>
/// <param name="Acotacion">Texto entre paréntesis, o null.</param>
/// <param name="Texto">Texto del diálogo sin comillas.</param>
/// <param name="Original">La línea completa tal como está en el guion, sin espacios en los extremos.</param>
public sealed record LineaDialogoParseada(
    string Codigo,
    int Orden,
    string Personaje,
    string? Acotacion,
    string Texto,
    string Original);
