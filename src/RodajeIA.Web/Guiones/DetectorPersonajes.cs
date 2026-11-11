using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RodajeIA.Web.Guiones;

/// <summary>
/// Detecta, sin IA, los personajes con hoja presentes en cada clip y en cada escena (FR-15,
/// FR-16). Un personaje está en un clip si su nombre aparece como palabra completa (sin letra,
/// marca combinante ni número pegados), escrito exactamente como en la hoja, en alguna línea de
/// acción, o si es el marcador (en mayúsculas) de alguna línea de diálogo. Está en una escena si
/// está en alguno de sus clips o si su nombre aparece en la puesta en escena. No se busca en el texto del diálogo, la acotación ni el texto
/// en pantalla. La comparación se hace en forma Unicode NFC; al comparar el marcador con la hoja,
/// el apóstrofo recto y el tipográfico cuentan como el mismo (RF-05a, ADR-003). Los textos no se
/// modifican.
/// </summary>
public static class DetectorPersonajes
{
    private static readonly CultureInfo Espanol = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>Detecta los personajes de <paramref name="nombresDeHojas"/> en el guion.</summary>
    /// <param name="guion">Guion ya validado por <see cref="GuionParser"/>.</param>
    /// <param name="nombresDeHojas">Nombres de las hojas de personaje de la serie. Una lista vacía
    /// da un resultado sin personajes; los nombres vacíos se ignoran y los repetidos (comparados en
    /// NFC) cuentan una vez, con el primero tal como vino.</param>
    /// <returns>Los nombres presentes por clip y por escena, tal como están en la hoja y en el
    /// orden de la lista; cada escena y cada clip del guion tiene su entrada, aunque esté vacía.</returns>
    public static PersonajesDetectados Detectar(GuionParseado guion, IReadOnlyList<string> nombresDeHojas)
    {
        ArgumentNullException.ThrowIfNull(guion);
        ArgumentNullException.ThrowIfNull(nombresDeHojas);

        // Se descartan los repetidos ya en NFC: «José Daniel» en NFC y en NFD es una sola hoja, y
        // se conserva el primer nombre tal como vino.
        var hojas = nombresDeHojas
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .DistinctBy(NFC, StringComparer.Ordinal)
            .Select(Hoja.Crear)
            .ToList();

        var porClip = new Dictionary<(int Escena, int Clip), IReadOnlyList<string>>();
        var porEscena = new Dictionary<int, IReadOnlyList<string>>();

        foreach (var escena in guion.Escenas)
        {
            var presentesEnEscena = new HashSet<string>(StringComparer.Ordinal);
            foreach (var clip in escena.Clips)
            {
                var accion = clip.LineasAccion.Select(NFC).ToList();
                var marcadores = clip.Dialogos
                    .Select(d => NombrePersonaje.ClaveDeMarcador(d.Personaje))
                    .ToHashSet(StringComparer.Ordinal);

                var presentes = hojas
                    .Where(h => marcadores.Contains(h.Marcador) || accion.Any(h.Patron.IsMatch))
                    .Select(h => h.Nombre)
                    .ToList();

                porClip[(escena.Numero, clip.Numero)] = presentes;
                presentesEnEscena.UnionWith(presentes);
            }

            var puesta = NFC(escena.PuestaEnEscena);
            porEscena[escena.Numero] = hojas
                .Where(h => presentesEnEscena.Contains(h.Nombre) || h.Patron.IsMatch(puesta))
                .Select(h => h.Nombre)
                .ToList();
        }

        return new PersonajesDetectados(porClip, porEscena);
    }

    private static string NFC(string texto) => texto.Normalize(NormalizationForm.FormC);

    /// <param name="Nombre">El nombre tal como está en la hoja (es el que se devuelve).</param>
    /// <param name="Patron">Nombre en NFC, escapado, con bordes de letra, marca combinante o
    /// número Unicode.</param>
    /// <param name="Marcador">Nombre en mayúsculas (es-ES) como <see cref="NombrePersonaje.ClaveDeMarcador"/>,
    /// para los diálogos.</param>
    private sealed record Hoja(string Nombre, Regex Patron, string Marcador)
    {
        public static Hoja Crear(string nombre)
        {
            var nfc = NFC(nombre);
            var patron = new Regex(
                @"(?<![\p{L}\p{M}\p{N}])" + Regex.Escape(nfc) + @"(?![\p{L}\p{M}\p{N}])",
                RegexOptions.CultureInvariant);
            return new Hoja(nombre, patron, NombrePersonaje.ClaveDeMarcador(nfc.ToUpper(Espanol)));
        }
    }
}
