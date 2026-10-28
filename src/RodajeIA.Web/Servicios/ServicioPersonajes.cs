using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RodajeIA.Web.Datos;

namespace RodajeIA.Web.Servicios;

/// <summary>Alta y listado de hojas de personaje de una serie (FR-03, FR-04).</summary>
public sealed class ServicioPersonajes(IDbContextFactory<RodajeDbContext> fabrica, ILogger<ServicioPersonajes> logger)
{
    /// <summary>Largo máximo del nombre (también acota la regex del detector, M-06).</summary>
    public const int MaximoNombre = 60;

    internal const string MotivoNombreRepetido = "Ya existe un personaje con ese nombre en esta serie";

    internal const string MotivoFalloAlGuardar = "No se pudo guardar la hoja de personaje";

    private static readonly CultureInfo Espanol = CultureInfo.GetCultureInfo("es-ES");

    internal const string MotivoNombreNoNfc =
        "El nombre tiene tildes en formato no estándar (NFD); vuelve a escribirlo desde otro editor";

    /// <summary>
    /// Crea una hoja de personaje. Cada campo se recorta en los extremos y no se reescribe de otra
    /// forma; un nombre que no esté en NFC se rechaza (no se normaliza, ADR-002).
    /// </summary>
    public async Task<Resultado<PersonajeResumen>> CrearAsync(int serieId, DatosPersonaje datos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var nombre = Recortar(datos.Nombre);
        var edad = Recortar(datos.Edad);
        var descripcion = Recortar(datos.DescripcionFisica);
        var peinado = Recortar(datos.Peinado);
        var vestuario = Recortar(datos.Vestuario);
        var heridas = Recortar(datos.HeridasMarcas);
        var personalidad = Recortar(datos.Personalidad);
        var rol = Recortar(datos.Rol);

        var errores = new List<Error>();
        Requerido(errores, nombre, "Nombre", "el nombre", MaximoNombre);
        Requerido(errores, edad, "Edad", "la edad", 40);
        Requerido(errores, descripcion, "DescripcionFisica", "la descripción física", 2000);
        Requerido(errores, peinado, "Peinado", "el peinado", 300);
        Requerido(errores, vestuario, "Vestuario", "el vestuario", 500);
        if (heridas.Length > 500)
        {
            errores.Add(new Error("Las heridas o marcas admiten como máximo 500 caracteres", Campo: "HeridasMarcas"));
        }

        Requerido(errores, personalidad, "Personalidad", "la personalidad", 1000);
        Requerido(errores, rol, "Rol", "el rol", 200);

        if (nombre.Length > 0 && !nombre.IsNormalized(NormalizationForm.FormC))
        {
            errores.Add(new Error(MotivoNombreNoNfc, Campo: "Nombre"));
        }

        if (errores.Count > 0)
        {
            return Resultado<PersonajeResumen>.Fallo(errores);
        }

        try
        {
            await using var contexto = await fabrica.CreateDbContextAsync(ct);
            if (!await contexto.Series.AnyAsync(s => s.Id == serieId, ct))
            {
                return Resultado<PersonajeResumen>.Fallo([new Error("La serie no existe")]);
            }

            // «Ana» y «ANA» darían el mismo marcador de diálogo (ANA:): se comparan como el detector,
            // en NFC y en mayúsculas es-ES. La serie tiene pocas hojas; se compara en memoria.
            var existentes = await contexto.Personajes
                .Where(p => p.SerieId == serieId)
                .Select(p => p.Nombre)
                .ToListAsync(ct);
            var clave = ClaveDeNombre(nombre);
            if (existentes.Any(n => ClaveDeNombre(n) == clave))
            {
                return Resultado<PersonajeResumen>.Fallo([new Error(MotivoNombreRepetido, Campo: "Nombre")]);
            }

            var personaje = new Personaje
            {
                SerieId = serieId,
                Nombre = nombre,
                Edad = edad,
                DescripcionFisica = descripcion,
                Peinado = peinado,
                Vestuario = vestuario,
                HeridasMarcas = heridas.Length == 0 ? null : heridas,
                Personalidad = personalidad,
                Rol = rol,
            };
            contexto.Personajes.Add(personaje);
            await contexto.SaveChangesAsync(ct);
            return Resultado<PersonajeResumen>.Exito(AResumen(personaje));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Alta de personaje en la serie {SerieId}: fallo de base de datos", serieId);
            return Resultado<PersonajeResumen>.Fallo([new Error(MotivoFalloAlGuardar)]);
        }
    }

    /// <summary>Clave de comparación de nombres: NFC y mayúsculas es-ES (solo para comparar; nada se guarda así).</summary>
    private static string ClaveDeNombre(string nombre) =>
        nombre.Normalize(NormalizationForm.FormC).ToUpper(Espanol).Normalize(NormalizationForm.FormC);

    /// <summary>Hojas de personaje de la serie, ordenadas por nombre.</summary>
    public async Task<IReadOnlyList<PersonajeResumen>> ListarAsync(int serieId, CancellationToken ct = default)
    {
        await using var contexto = await fabrica.CreateDbContextAsync(ct);
        return await contexto.Personajes
            .AsNoTracking()
            .Where(p => p.SerieId == serieId)
            .OrderBy(p => p.Nombre)
            .ThenBy(p => p.Id)
            .Select(p => new PersonajeResumen(
                p.Id, p.SerieId, p.Nombre, p.Edad, p.DescripcionFisica, p.Peinado, p.Vestuario,
                p.HeridasMarcas, p.Personalidad, p.Rol))
            .ToListAsync(ct);
    }

    private static string Recortar(string? valor) => (valor ?? "").Trim();

    private static void Requerido(List<Error> errores, string valor, string campo, string etiqueta, int maximo)
    {
        if (valor.Length == 0)
        {
            errores.Add(new Error($"Falta {etiqueta}", Campo: campo));
        }
        else if (valor.Length > maximo)
        {
            var mayuscula = char.ToUpperInvariant(etiqueta[0]) + etiqueta[1..];
            errores.Add(new Error($"{mayuscula} admite como máximo {maximo} caracteres", Campo: campo));
        }
    }

    private static PersonajeResumen AResumen(Personaje p) => new(
        p.Id, p.SerieId, p.Nombre, p.Edad, p.DescripcionFisica, p.Peinado, p.Vestuario,
        p.HeridasMarcas, p.Personalidad, p.Rol);
}
