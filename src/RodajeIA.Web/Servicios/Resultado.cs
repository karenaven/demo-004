using System.Diagnostics.CodeAnalysis;

namespace RodajeIA.Web.Servicios;

/// <summary>
/// Motivo por el que una operación de servicio no pudo completarse.
/// </summary>
/// <param name="Motivo">Descripción legible del problema, pensada para mostrarse al usuario.</param>
/// <param name="Campo">Campo del formulario al que se refiere el error, si aplica.</param>
/// <param name="Linea">Número de línea del guion (desde 1) en que se detectó el error, si aplica.</param>
public sealed record Error(string Motivo, string? Campo = null, int? Linea = null);

/// <summary>
/// Resultado de una operación de servicio: un valor (éxito) o una lista no vacía de errores (fallo).
/// </summary>
/// <typeparam name="T">Tipo del valor devuelto en caso de éxito.</typeparam>
public sealed class Resultado<T>
{
    private Resultado(T? valor, IReadOnlyList<Error> errores)
    {
        Valor = valor;
        Errores = errores;
    }

    /// <summary>
    /// Valor producido por la operación. En un fallo es <c>default</c>: consulta
    /// <see cref="EsExito"/> antes de usarlo.
    /// </summary>
    public T? Valor { get; }

    /// <summary>
    /// Errores de la operación. Vacía en un éxito; con al menos un elemento en un fallo.
    /// </summary>
    public IReadOnlyList<Error> Errores { get; }

    /// <summary>
    /// <c>true</c> si la operación terminó sin errores. Cuando es <c>true</c>, el análisis de
    /// nulabilidad considera que <see cref="Valor"/> no es null.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Valor))]
    public bool EsExito => Errores.Count == 0;

    /// <summary>
    /// Crea un resultado exitoso con el valor indicado.
    /// </summary>
    /// <param name="valor">Valor producido por la operación.</param>
    public static Resultado<T> Exito(T valor) => new(valor, []);

    /// <summary>
    /// Crea un resultado fallido con los errores indicados. <see cref="Valor"/> queda en
    /// <c>default</c>.
    /// </summary>
    /// <param name="errores">Errores de la operación; debe contener al menos uno.</param>
    /// <exception cref="ArgumentNullException">Si <paramref name="errores"/> es null.</exception>
    /// <exception cref="ArgumentException">Si <paramref name="errores"/> está vacía: un fallo sin
    /// motivo no existe.</exception>
    public static Resultado<T> Fallo(IEnumerable<Error> errores)
    {
        ArgumentNullException.ThrowIfNull(errores);

        var lista = errores.ToList();
        if (lista.Count == 0)
        {
            throw new ArgumentException("Un fallo necesita al menos un error.", nameof(errores));
        }

        return new Resultado<T>(default, lista.AsReadOnly());
    }
}
