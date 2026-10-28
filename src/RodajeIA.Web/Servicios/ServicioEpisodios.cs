using Microsoft.EntityFrameworkCore;
using RodajeIA.Web.Datos;

namespace RodajeIA.Web.Servicios;

/// <summary>Alta y listado de episodios de una serie (FR-05, FR-06).</summary>
public sealed class ServicioEpisodios(IDbContextFactory<RodajeDbContext> fabrica, ILogger<ServicioEpisodios> logger)
{
    /// <summary>Número máximo de episodio.</summary>
    public const int MaximoNumero = 9999;

    /// <summary>Largo máximo del título.</summary>
    public const int MaximoTitulo = 150;

    internal const string MotivoFalloAlGuardar = "No se pudo guardar el episodio";

    /// <summary>Crea un episodio con el título recortado en los extremos. El número es único en la serie.</summary>
    public async Task<Resultado<EpisodioResumen>> CrearAsync(int serieId, int numero, string titulo, CancellationToken ct = default)
    {
        var recortado = (titulo ?? "").Trim();
        var errores = new List<Error>();
        if (numero < 1 || numero > MaximoNumero)
        {
            errores.Add(new Error($"El número debe estar entre 1 y {MaximoNumero}", Campo: "Numero"));
        }

        if (recortado.Length == 0)
        {
            errores.Add(new Error("Falta el título", Campo: "Titulo"));
        }
        else if (recortado.Length > MaximoTitulo)
        {
            errores.Add(new Error($"El título admite como máximo {MaximoTitulo} caracteres", Campo: "Titulo"));
        }

        if (errores.Count > 0)
        {
            return Resultado<EpisodioResumen>.Fallo(errores);
        }

        try
        {
            await using var contexto = await fabrica.CreateDbContextAsync(ct);
            if (!await contexto.Series.AnyAsync(s => s.Id == serieId, ct))
            {
                return Resultado<EpisodioResumen>.Fallo([new Error("La serie no existe")]);
            }

            if (await contexto.Episodios.AnyAsync(e => e.SerieId == serieId && e.Numero == numero, ct))
            {
                return Resultado<EpisodioResumen>.Fallo([NumeroRepetido(numero)]);
            }

            var episodio = new Episodio { SerieId = serieId, Numero = numero, Titulo = recortado };
            contexto.Episodios.Add(episodio);
            try
            {
                await contexto.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                // Otro alta con el mismo número pudo ganar la carrera contra la consulta anterior;
                // se confirma con un contexto nuevo antes de decirlo.
                if (await NumeroExisteAsync(serieId, numero, ct))
                {
                    return Resultado<EpisodioResumen>.Fallo([NumeroRepetido(numero)]);
                }

                logger.LogError(ex, "Alta de episodio en la serie {SerieId}: fallo al guardar", serieId);
                return Resultado<EpisodioResumen>.Fallo([new Error(MotivoFalloAlGuardar)]);
            }

            return Resultado<EpisodioResumen>.Exito(new EpisodioResumen(episodio.Id, serieId, numero, recortado));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Alta de episodio en la serie {SerieId}: fallo de base de datos", serieId);
            return Resultado<EpisodioResumen>.Fallo([new Error(MotivoFalloAlGuardar)]);
        }
    }

    private async Task<bool> NumeroExisteAsync(int serieId, int numero, CancellationToken ct)
    {
        await using var contexto = await fabrica.CreateDbContextAsync(ct);
        return await contexto.Episodios.AnyAsync(e => e.SerieId == serieId && e.Numero == numero, ct);
    }

    /// <summary>Episodios de la serie, ordenados por número.</summary>
    public async Task<IReadOnlyList<EpisodioResumen>> ListarAsync(int serieId, CancellationToken ct = default)
    {
        await using var contexto = await fabrica.CreateDbContextAsync(ct);
        return await contexto.Episodios
            .AsNoTracking()
            .Where(e => e.SerieId == serieId)
            .OrderBy(e => e.Numero)
            .Select(e => new EpisodioResumen(e.Id, e.SerieId, e.Numero, e.Titulo))
            .ToListAsync(ct);
    }

    private static Error NumeroRepetido(int numero) =>
        new($"El número {numero} ya existe en esta serie", Campo: "Numero");
}
