using System.Text;
using System.Text.RegularExpressions;

namespace RodajeIA.Web.Guiones;

/// <summary>
/// Fuente única de los caracteres que admiten el marcador de diálogo (Anexo A del PRD base) y el
/// nombre de una hoja de personaje (RF-01i), y de la clave con que se comparan (RF-05a, ADR-003).
/// </summary>
public static class NombrePersonaje
{
    /// <summary>
    /// Nombre de un marcador de diálogo: empieza con una mayúscula y sigue con mayúsculas de
    /// cualquier idioma, dígitos, espacio, guion, apóstrofo recto o tipográfico y punto.
    /// Sin cuantificadores anidados (M-01).
    /// </summary>
    internal const string FragmentoMarcador = @"\p{Lu}[\p{Lu}0-9 '’.\-]*";

    /// <summary>Descripción de los caracteres permitidos, para los mensajes de error.</summary>
    public const string CaracteresPermitidos =
        "letras, dígitos, espacio, guion (-), apóstrofo (' o ’) y punto, empezando con una letra";

    private static readonly Regex NombreDeHoja = new(
        @"^\p{L}[\p{L}0-9 '’.\-]*$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    /// <summary>
    /// <c>true</c> si el nombre empieza con letra y solo tiene letras, dígitos, espacio, guion,
    /// apóstrofo recto o tipográfico y punto (RF-01i).
    /// </summary>
    public static bool EsNombreDeHojaValido(string nombre)
    {
        ArgumentNullException.ThrowIfNull(nombre);
        return NombreDeHoja.IsMatch(nombre);
    }

    /// <summary>
    /// Clave para comparar un marcador con el nombre de una hoja: NFC y el apóstrofo tipográfico
    /// (<c>’</c>) como recto (<c>'</c>). Solo sirve para comparar: nunca se guarda ni se devuelve
    /// en lugar del texto original (M-03).
    /// </summary>
    public static string ClaveDeMarcador(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);
        return texto.Normalize(NormalizationForm.FormC).Replace('’', '\'');
    }
}
