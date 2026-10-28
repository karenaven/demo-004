using System.Diagnostics.CodeAnalysis;

namespace RodajeIA.Web.Guiones;

/// <summary>
/// Resultado de parsear un guion: el guion parseado (si es válido) o la lista de errores con su
/// línea. <see cref="Guion"/> es null si y solo si <see cref="Errores"/> no está vacía.
/// </summary>
public sealed class ResultadoParseo
{
    private ResultadoParseo(GuionParseado? guion, IReadOnlyList<ErrorGuion> errores)
    {
        Guion = guion;
        Errores = errores;
    }

    /// <summary>Guion parseado; null si el guion tiene errores.</summary>
    public GuionParseado? Guion { get; }

    /// <summary>Errores de validación ordenados por línea; vacía si el guion es válido.</summary>
    public IReadOnlyList<ErrorGuion> Errores { get; }

    /// <summary><c>true</c> si el guion no tiene errores (y por lo tanto hay <see cref="Guion"/>).</summary>
    [MemberNotNullWhen(true, nameof(Guion))]
    public bool EsValido => Errores.Count == 0;

    /// <summary>Crea un resultado válido con el guion parseado.</summary>
    public static ResultadoParseo Valido(GuionParseado guion)
    {
        ArgumentNullException.ThrowIfNull(guion);
        return new ResultadoParseo(guion, []);
    }

    /// <summary>Crea un resultado inválido, sin guion, con los errores indicados.</summary>
    /// <exception cref="ArgumentException">Si no hay ningún error.</exception>
    public static ResultadoParseo Invalido(IEnumerable<ErrorGuion> errores)
    {
        ArgumentNullException.ThrowIfNull(errores);

        var lista = errores.ToList();
        if (lista.Count == 0)
        {
            throw new ArgumentException("Un guion inválido necesita al menos un error.", nameof(errores));
        }

        return new ResultadoParseo(null, lista.AsReadOnly());
    }
}
