using System.Collections.Frozen;

namespace RodajeIA.Web.Guiones;

/// <summary>
/// Personajes con hoja detectados en un guion parseado: por clip (escena, clip) y por escena.
/// Los nombres se devuelven tal como están en la hoja, en el orden de la lista de hojas. Es
/// inmutable: el constructor copia las colecciones recibidas.
/// </summary>
public sealed class PersonajesDetectados
{
    /// <summary>Crea el resultado copiando los diccionarios y las listas recibidos.</summary>
    /// <exception cref="ArgumentNullException">Si un diccionario, una lista o un nombre es null.</exception>
    public PersonajesDetectados(
        IReadOnlyDictionary<(int Escena, int Clip), IReadOnlyList<string>> porClip,
        IReadOnlyDictionary<int, IReadOnlyList<string>> porEscena)
    {
        ArgumentNullException.ThrowIfNull(porClip);
        ArgumentNullException.ThrowIfNull(porEscena);
        PorClip = porClip.ToFrozenDictionary(kv => kv.Key, kv => Copiar(kv.Value, nameof(porClip)));
        PorEscena = porEscena.ToFrozenDictionary(kv => kv.Key, kv => Copiar(kv.Value, nameof(porEscena)));
    }

    /// <summary>Nombres presentes en cada clip, con clave (número de escena, número de clip).</summary>
    public IReadOnlyDictionary<(int Escena, int Clip), IReadOnlyList<string>> PorClip { get; }

    /// <summary>Nombres presentes en cada escena (unión de sus clips y su puesta en escena).</summary>
    public IReadOnlyDictionary<int, IReadOnlyList<string>> PorEscena { get; }

    /// <summary>Nombres presentes en el clip; vacío si el clip no existe.</summary>
    public IReadOnlyList<string> EnClip(int escena, int clip) =>
        PorClip.TryGetValue((escena, clip), out var nombres) ? nombres : [];

    /// <summary>Nombres presentes en la escena; vacío si la escena no existe.</summary>
    public IReadOnlyList<string> EnEscena(int escena) =>
        PorEscena.TryGetValue(escena, out var nombres) ? nombres : [];

    private static IReadOnlyList<string> Copiar(IReadOnlyList<string>? nombres, string parametro)
    {
        if (nombres is null || nombres.Any(n => n is null))
        {
            throw new ArgumentNullException(parametro, "Una lista de nombres o un nombre es null.");
        }

        return Array.AsReadOnly(nombres.ToArray());
    }
}
