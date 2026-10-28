using Microsoft.EntityFrameworkCore;
using RodajeIA.Web.Datos;
using RodajeIA.Web.Guiones;
using DatosIntExt = RodajeIA.Web.Datos.IntExt;
using GuionesIntExt = RodajeIA.Web.Guiones.IntExt;

namespace RodajeIA.Web.Servicios;

/// <summary>
/// Carga del guion de un episodio (FR-07, FR-09, FR-12, FR-13, FR-15 a FR-17): valida la entrada,
/// parsea sin IA, detecta los personajes con hoja y, solo si el guion es válido, reemplaza en una
/// transacción el guion anterior con sus escenas, clips, líneas de diálogo y personajes presentes.
/// </summary>
public sealed class ServicioGuiones(IDbContextFactory<RodajeDbContext> fabrica, ILogger<ServicioGuiones> logger)
{
    /// <summary>Largo máximo del guion, en caracteres (unidades UTF-16).</summary>
    public const int MaximoCaracteres = 200_000;

    internal const string MotivoSinSeleccion = "Elige una serie y un episodio";
    internal const string MotivoEpisodioAjeno = "El episodio no pertenece a la serie elegida";
    internal const string MotivoGuionLargo = "El guion supera los 200 000 caracteres";
    internal const string MotivoSinGuion = "Falta el guion";
    internal const string MotivoFalloAlGuardar = "No se pudo guardar el guion; no se modificó nada";

    /// <summary>
    /// Valida y guarda el guion en el episodio. Nunca lanza hacia la UI: los problemas vuelven
    /// como errores del <see cref="Resultado{T}"/>. Si el guion es inválido o algo falla al
    /// guardar, el episodio queda como estaba.
    /// </summary>
    public async Task<Resultado<ResumenCarga>> CargarAsync(int? serieId, int? episodioId, string texto, CancellationToken ct = default)
    {
        if (serieId is not int serie || episodioId is not int episodio)
        {
            return Rechazar(episodioId, [new Error(MotivoSinSeleccion)]);
        }

        if (texto is null)
        {
            return Rechazar(episodio, [new Error(MotivoSinGuion, Campo: "Guion")]);
        }

        if (texto.Length > MaximoCaracteres)
        {
            return Rechazar(episodio, [new Error(MotivoGuionLargo, Campo: "Guion")]);
        }

        // Todo el acceso a la base, desde crear el contexto hasta el commit, queda cubierto: ningún
        // fallo llega a la UI como excepción.
        try
        {
            return await ValidarYGuardarAsync(serie, episodio, texto, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // La transacción, si llegó a abrirse, se revirtió al salir de su using sin commit.
            logger.LogError(ex, "Carga de guion en el episodio {EpisodioId}: fallo de base de datos", episodio);
            logger.LogInformation("Carga de guion en el episodio {EpisodioId}: fallido, no se modificó nada", episodio);
            return Resultado<ResumenCarga>.Fallo([new Error(MotivoFalloAlGuardar)]);
        }
    }

    private async Task<Resultado<ResumenCarga>> ValidarYGuardarAsync(int serie, int episodio, string texto, CancellationToken ct)
    {
        await using var contexto = await fabrica.CreateDbContextAsync(ct);
        if (!await contexto.Episodios.AnyAsync(e => e.Id == episodio && e.SerieId == serie, ct))
        {
            return Rechazar(episodio, [new Error(MotivoEpisodioAjeno)]);
        }

        // FR-13: un guion inválido no abre transacción ni toca la base.
        var parseo = GuionParser.Parsear(texto);
        if (!parseo.EsValido)
        {
            return Rechazar(episodio, parseo.Errores.Select(e => new Error(e.Motivo, Linea: e.Linea)));
        }

        var hojas = await contexto.Personajes
            .Where(p => p.SerieId == serie)
            .OrderBy(p => p.Id)
            .ToListAsync(ct);
        var porNombre = new Dictionary<string, Personaje>(StringComparer.Ordinal);
        foreach (var hoja in hojas)
        {
            porNombre.TryAdd(hoja.Nombre, hoja);
        }

        var detectados = DetectorPersonajes.Detectar(parseo.Guion, [.. porNombre.Keys]);
        var escenas = parseo.Guion.Escenas.Select(e => CrearEscena(episodio, e, detectados, porNombre)).ToList();

        await using (var transaccion = await contexto.Database.BeginTransactionAsync(ct))
        {
            // Cascada en la base: clips, líneas de diálogo y tablas de unión del guion anterior.
            await contexto.Escenas.Where(e => e.EpisodioId == episodio).ExecuteDeleteAsync(ct);

            var entidad = await contexto.Episodios.SingleAsync(e => e.Id == episodio, ct);
            entidad.Guion = texto;
            contexto.Escenas.AddRange(escenas);
            await contexto.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
        }

        var resumen = new ResumenCarga(escenas.Count, escenas.Sum(e => e.Clips.Count));
        logger.LogInformation(
            "Carga de guion en el episodio {EpisodioId}: guardado, {Escenas} escenas y {Clips} clips",
            episodio, resumen.Escenas, resumen.Clips);
        return Resultado<ResumenCarga>.Exito(resumen);
    }

    /// <summary>Traduce el interior/exterior del parser al enum de la entidad.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Si el valor no está definido.</exception>
    internal static DatosIntExt ConvertirIntExt(GuionesIntExt intExt) => intExt switch
    {
        GuionesIntExt.Int => DatosIntExt.Int,
        GuionesIntExt.Ext => DatosIntExt.Ext,
        _ => throw new ArgumentOutOfRangeException(nameof(intExt), intExt, "Valor de INT./EXT. desconocido."),
    };

    private Resultado<ResumenCarga> Rechazar(int? episodioId, IEnumerable<Error> errores)
    {
        var resultado = Resultado<ResumenCarga>.Fallo(errores);
        logger.LogInformation(
            "Carga de guion en el episodio {EpisodioId}: rechazado, {Errores} errores",
            episodioId, resultado.Errores.Count);
        return resultado;
    }

    private static Escena CrearEscena(
        int episodioId, EscenaParseada escena, PersonajesDetectados detectados, Dictionary<string, Personaje> porNombre) => new()
        {
            EpisodioId = episodioId,
            Numero = escena.Numero,
            IntExt = ConvertirIntExt(escena.IntExt),
            Lugar = escena.Lugar,
            Momento = escena.Momento,
            Locacion = escena.Locacion,
            Iluminacion = escena.Iluminacion,
            PuestaEnEscena = escena.PuestaEnEscena,
            Audio = escena.Audio,
            Estado = EstadoGeneracion.Pendiente,
            Personajes = [.. detectados.EnEscena(escena.Numero).Select(n => porNombre[n])],
            Clips = [.. escena.Clips.Select(c => new Clip
            {
                Numero = c.Numero,
                Accion = string.Join("\n", c.LineasAccion),
                TextoEnPantalla = c.TextoEnPantalla,
                TextoEnPantallaOriginal = c.TextoEnPantallaOriginal,
                Personajes = [.. detectados.EnClip(escena.Numero, c.Numero).Select(n => porNombre[n])],
                LineasDialogo = [.. c.Dialogos.Select(d => new LineaDialogo
                {
                    Codigo = d.Codigo,
                    Orden = d.Orden,
                    Personaje = d.Personaje,
                    Acotacion = d.Acotacion,
                    Texto = d.Texto,
                    Original = d.Original,
                })],
            })],
        };
}
