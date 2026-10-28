using Microsoft.EntityFrameworkCore;
using RodajeIA.Web.Datos;

namespace RodajeIA.Web.Servicios;

/// <summary>Alta y listado de series (FR-01, FR-02).</summary>
public sealed class ServicioSeries(IDbContextFactory<RodajeDbContext> fabrica, ILogger<ServicioSeries> logger)
{
    /// <summary>Largo máximo del nombre de una serie.</summary>
    public const int MaximoNombre = 100;

    internal const string MotivoFalloAlGuardar = "No se pudo guardar la serie";

    /// <summary>
    /// Crea una serie con el nombre recortado en los extremos. Nunca lanza hacia la UI: un fallo de
    /// la base vuelve como error genérico.
    /// </summary>
    public async Task<Resultado<SerieResumen>> CrearAsync(string nombre, CancellationToken ct = default)
    {
        var recortado = (nombre ?? "").Trim();
        if (recortado.Length == 0)
        {
            return Resultado<SerieResumen>.Fallo([new Error("El nombre es obligatorio", Campo: "Nombre")]);
        }

        if (recortado.Length > MaximoNombre)
        {
            return Resultado<SerieResumen>.Fallo(
                [new Error($"El nombre admite como máximo {MaximoNombre} caracteres", Campo: "Nombre")]);
        }

        try
        {
            await using var contexto = await fabrica.CreateDbContextAsync(ct);
            var serie = new Serie { Nombre = recortado };
            contexto.Series.Add(serie);
            await contexto.SaveChangesAsync(ct);
            return Resultado<SerieResumen>.Exito(new SerieResumen(serie.Id, serie.Nombre));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Alta de serie: fallo de base de datos");
            return Resultado<SerieResumen>.Fallo([new Error(MotivoFalloAlGuardar)]);
        }
    }

    /// <summary>Todas las series, ordenadas por nombre.</summary>
    public async Task<IReadOnlyList<SerieResumen>> ListarAsync(CancellationToken ct = default)
    {
        await using var contexto = await fabrica.CreateDbContextAsync(ct);
        return await contexto.Series
            .AsNoTracking()
            .OrderBy(s => s.Nombre)
            .ThenBy(s => s.Id)
            .Select(s => new SerieResumen(s.Id, s.Nombre))
            .ToListAsync(ct);
    }
}
