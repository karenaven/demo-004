using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using RodajeIA.Tests.Datos;
using RodajeIA.Tests.Guiones;
using RodajeIA.Web.Datos;
using RodajeIA.Web.Guiones;
using RodajeIA.Web.Servicios;
using DatosIntExt = RodajeIA.Web.Datos.IntExt;
using GuionesIntExt = RodajeIA.Web.Guiones.IntExt;

namespace RodajeIA.Tests.Servicios;

public sealed class ServicioGuionesTests : IDisposable
{
    private readonly BaseDeDatosEnMemoria _base = new();
    private readonly RegistroDePrueba<ServicioGuiones> _registro = new();
    private readonly ServicioGuiones _servicio;

    public ServicioGuionesTests() => _servicio = new ServicioGuiones(_base.Fabrica, _registro);

    public void Dispose() => _base.Dispose();

    private static string GuionDe(int escenas) =>
        GuionesDePrueba.Unir([.. Enumerable.Range(1, escenas).SelectMany(GuionesDePrueba.EscenaMinima)]);

    /// <summary>Escena 1 sin <c>AUDIO:</c> (línea 5) y escena 2 sin ningún <c>CLIP</c> (AC-13).</summary>
    private static readonly string GuionInvalido = GuionesDePrueba.Unir(
        "ESCENA 1 — INT. CASA — DÍA",
        "LOCACIÓN: Sala con un sofá gris.",
        "ILUMINACIÓN: Luz de día por la ventana.",
        "PUESTA EN ESCENA: Ana de pie junto a la ventana.",
        "CLIP 1",
        "Ana mira por la ventana.",
        "ESCENA 2 — INT. CASA — NOCHE",
        "LOCACIÓN: Sala con un sofá gris.",
        "ILUMINACIÓN: Lámpara de pie.",
        "PUESTA EN ESCENA: Ana sentada.",
        "AUDIO: Silencio.");

    private async Task<(int SerieId, int EpisodioId)> SembrarAsync(params string[] hojas)
    {
        await using var contexto = _base.CrearContexto();
        var serie = new Serie { Nombre = "Rodaje" };
        foreach (var nombre in hojas)
        {
            serie.Personajes.Add(new Personaje
            {
                Nombre = nombre,
                Edad = "30",
                DescripcionFisica = "d",
                Peinado = "p",
                Vestuario = "v",
                Personalidad = "p",
                Rol = "r",
            });
        }

        var episodio = new Episodio { Serie = serie, Numero = 1, Titulo = "Piloto" };
        contexto.AddRange(serie, episodio);
        await contexto.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (serie.Id, episodio.Id);
    }

    private sealed record Foto(
        string? Guion,
        List<(int Id, int Numero, EstadoGeneracion Estado, List<string> Personajes)> Escenas,
        List<(int Id, int Escena, int Numero, string Accion, List<string> Personajes)> Clips,
        List<(int Id, string Codigo, string Texto)> Lineas);

    private async Task<Foto> FotografiarAsync(int episodioId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var contexto = _base.CrearContexto();
        var episodio = await contexto.Episodios.AsNoTracking().SingleAsync(e => e.Id == episodioId, ct);
        var escenas = await contexto.Escenas.AsNoTracking()
            .Where(e => e.EpisodioId == episodioId)
            .Include(e => e.Personajes)
            .Include(e => e.Clips).ThenInclude(c => c.Personajes)
            .Include(e => e.Clips).ThenInclude(c => c.LineasDialogo)
            .OrderBy(e => e.Numero)
            .ToListAsync(ct);
        return new Foto(
            episodio.Guion,
            [.. escenas.Select(e => (e.Id, e.Numero, e.Estado, e.Personajes.Select(p => p.Nombre).Order(StringComparer.Ordinal).ToList()))],
            [.. escenas.SelectMany(e => e.Clips.OrderBy(c => c.Numero).Select(c =>
                (c.Id, e.Numero, c.Numero, c.Accion, c.Personajes.Select(p => p.Nombre).Order(StringComparer.Ordinal).ToList())))],
            [.. escenas.SelectMany(e => e.Clips).SelectMany(c => c.LineasDialogo).OrderBy(l => l.Id).Select(l => (l.Id, l.Codigo, l.Texto))]);
    }

    private async Task<(int Escenas, int Clips, int Lineas, int EscenaPersonaje, int ClipPersonaje)> ContarTodoAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var contexto = _base.CrearContexto();
        return (
            await contexto.Escenas.CountAsync(ct),
            await contexto.Clips.CountAsync(ct),
            await contexto.LineasDialogo.CountAsync(ct),
            await contexto.Set<Dictionary<string, object>>("EscenaPersonaje").CountAsync(ct),
            await contexto.Set<Dictionary<string, object>>("ClipPersonaje").CountAsync(ct));
    }

    [Fact]
    public async Task CargarGuion_Valido_QuedaAsociadoAlEpisodio()
    {
        var ct = TestContext.Current.CancellationToken;
        var (serieId, episodioId) = await SembrarAsync("Ana", "José Daniel");
        var texto = GuionesDePrueba.EscenaDeEjemplo;

        var resultado = await _servicio.CargarAsync(serieId, episodioId, texto, ct);

        Assert.True(resultado.EsExito);
        Assert.Equal(new ResumenCarga(1, 2), resultado.Valor);
        var foto = await FotografiarAsync(episodioId);
        Assert.Equal(texto, foto.Guion);
        Assert.Equal([1], foto.Escenas.Select(e => e.Numero));
        Assert.Equal([1, 2], foto.Clips.Select(c => c.Numero));
        Assert.Equal(
            "Ana y José Daniel conversan en una mesa junto a la ventana. José Daniel sonríe pícaramente.\nAna niega con la cabeza, divertida, con una sonrisa amplia.",
            foto.Clips[0].Accion);
        Assert.Equal(["c1-d1", "c1-d2"], foto.Lineas.Select(l => l.Codigo));
        Assert.Equal(["Mentiroso.", "Bueno... pasó parecido."], foto.Lineas.Select(l => l.Texto));

        await using var contexto = _base.CrearContexto();
        var escena = await contexto.Escenas.AsNoTracking().SingleAsync(ct);
        Assert.Equal(DatosIntExt.Int, escena.IntExt);
        Assert.Equal("RESTAURANTE", escena.Lugar);
        Assert.Equal("NOCHE", escena.Momento);
        Assert.Equal(GuionesDePrueba.Locacion, escena.Locacion);
        Assert.Equal(GuionesDePrueba.Iluminacion, escena.Iluminacion);
        Assert.Equal(GuionesDePrueba.PuestaEnEscena, escena.PuestaEnEscena);
        Assert.Equal(GuionesDePrueba.Audio, escena.Audio);
        var linea = await contexto.LineasDialogo.AsNoTracking().SingleAsync(l => l.Codigo == "c1-d2", ct);
        Assert.Equal("JOSÉ DANIEL", linea.Personaje);
        Assert.Equal("sonriendo", linea.Acotacion);
        Assert.Equal(2, linea.Orden);
        Assert.Equal("JOSÉ DANIEL (sonriendo): \"Bueno... pasó parecido.\"", linea.Original);
    }

    [Fact]
    public async Task CargarGuion_TextoEnPantalla_SeGuardaConYSinComillas()
    {
        var ct = TestContext.Current.CancellationToken;
        var (serieId, episodioId) = await SembrarAsync();
        var texto = GuionesDePrueba.Unir([.. GuionesDePrueba.EscenaMinima(1), "TEXTO EN PANTALLA: “Tres años después”"]);

        var resultado = await _servicio.CargarAsync(serieId, episodioId, texto, ct);

        Assert.True(resultado.EsExito);
        await using var contexto = _base.CrearContexto();
        var clip = await contexto.Clips.AsNoTracking().SingleAsync(ct);
        Assert.Equal("Tres años después", clip.TextoEnPantalla);
        Assert.Equal("“Tres años después”", clip.TextoEnPantallaOriginal);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(1, null)]
    [InlineData(null, 1)]
    public async Task CargarGuion_SinSerieOEpisodio_NoGuardaNada(int? usarSerie, int? usarEpisodio)
    {
        var ct = TestContext.Current.CancellationToken;
        var (serieId, episodioId) = await SembrarAsync();

        var resultado = await _servicio.CargarAsync(
            usarSerie is null ? null : serieId,
            usarEpisodio is null ? null : episodioId,
            GuionDe(1),
            ct);

        Assert.Equal(new Error("Elige una serie y un episodio"), Assert.Single(resultado.Errores));
        Assert.Null((await FotografiarAsync(episodioId)).Guion);
        Assert.Equal((0, 0, 0, 0, 0), await ContarTodoAsync());
    }

    [Fact]
    public async Task CargarGuion_EpisodioDeOtraSerie_Falla()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, episodioId) = await SembrarAsync();
        int otraSerieId;
        await using (var contexto = _base.CrearContexto())
        {
            var otra = new Serie { Nombre = "Otra" };
            contexto.Series.Add(otra);
            await contexto.SaveChangesAsync(ct);
            otraSerieId = otra.Id;
        }

        var deOtraSerie = await _servicio.CargarAsync(otraSerieId, episodioId, GuionDe(1), ct);
        var inexistente = await _servicio.CargarAsync(otraSerieId, 999, GuionDe(1), ct);

        Assert.Equal(new Error("El episodio no pertenece a la serie elegida"), Assert.Single(deOtraSerie.Errores));
        Assert.Equal(new Error("El episodio no pertenece a la serie elegida"), Assert.Single(inexistente.Errores));
        Assert.Null((await FotografiarAsync(episodioId)).Guion);
        Assert.Equal((0, 0, 0, 0, 0), await ContarTodoAsync());
    }

    [Fact]
    public async Task CargarGuion_Mayor200000_Falla()
    {
        var ct = TestContext.Current.CancellationToken;
        var (serieId, episodioId) = await SembrarAsync();
        var valido = GuionDe(1);
        var justo = valido + new string('\n', 200_000 - valido.Length);
        var demasiado = justo + "\n";

        var rechazado = await _servicio.CargarAsync(serieId, episodioId, demasiado, ct);

        Assert.Equal(new Error("El guion supera los 200 000 caracteres", Campo: "Guion"), Assert.Single(rechazado.Errores));
        Assert.Null((await FotografiarAsync(episodioId)).Guion);

        var aceptado = await _servicio.CargarAsync(serieId, episodioId, justo, ct);
        Assert.True(aceptado.EsExito);
    }

    [Fact]
    public async Task CargarGuion_ValidoSobreUnoDe3Escenas_QuedanSolo2()
    {
        var ct = TestContext.Current.CancellationToken;
        var (serieId, episodioId) = await SembrarAsync("Ana");
        Assert.True((await _servicio.CargarAsync(serieId, episodioId, GuionesDePrueba.Generar(3, 2), ct)).EsExito);
        await using (var contexto = _base.CrearContexto())
        {
            // Escenas "generadas" del guion anterior.
            await contexto.Escenas.ExecuteUpdateAsync(s => s.SetProperty(e => e.Estado, EstadoGeneracion.Generada), ct);
        }

        var anteriores = await FotografiarAsync(episodioId);
        Assert.Equal(3, anteriores.Escenas.Count);
        var nuevo = GuionDe(2);

        var resultado = await _servicio.CargarAsync(serieId, episodioId, nuevo, ct);

        Assert.True(resultado.EsExito);
        Assert.Equal(new ResumenCarga(2, 2), resultado.Valor);
        var foto = await FotografiarAsync(episodioId);
        Assert.Equal(nuevo, foto.Guion);
        Assert.Equal([1, 2], foto.Escenas.Select(e => e.Numero));
        Assert.All(foto.Escenas, e => Assert.Equal(EstadoGeneracion.Pendiente, e.Estado));
        Assert.Empty(foto.Escenas.Select(e => e.Id).Intersect(anteriores.Escenas.Select(e => e.Id)));
        Assert.Empty(foto.Lineas);
        // Ni rastro del guion anterior: solo 2 escenas, 2 clips, ninguna línea; uniones solo de Ana.
        Assert.Equal((2, 2, 0, 2, 2), await ContarTodoAsync());
    }

    [Fact]
    public async Task CargarGuion_InvalidoSobreUnoValido_ConservaElAnterior()
    {
        var ct = TestContext.Current.CancellationToken;
        var (serieId, episodioId) = await SembrarAsync("Ana", "José Daniel");
        var valido = GuionesDePrueba.EscenaDeEjemplo;
        Assert.True((await _servicio.CargarAsync(serieId, episodioId, valido, ct)).EsExito);
        var antes = await FotografiarAsync(episodioId);
        var conteoAntes = await ContarTodoAsync();

        var resultado = await _servicio.CargarAsync(serieId, episodioId, GuionInvalido, ct);

        Assert.False(resultado.EsExito);
        Assert.All(resultado.Errores, e => Assert.NotNull(e.Linea));
        var despues = await FotografiarAsync(episodioId);
        Assert.Equal(valido, despues.Guion);
        Assert.Equal(antes.Escenas.Select(e => (e.Id, e.Numero, e.Estado, string.Join(",", e.Personajes))),
            despues.Escenas.Select(e => (e.Id, e.Numero, e.Estado, string.Join(",", e.Personajes))));
        Assert.Equal(antes.Clips.Select(c => (c.Id, c.Accion, string.Join(",", c.Personajes))),
            despues.Clips.Select(c => (c.Id, c.Accion, string.Join(",", c.Personajes))));
        Assert.Equal(antes.Lineas, despues.Lineas);
        Assert.Equal(conteoAntes, await ContarTodoAsync());
    }

    [Fact]
    public async Task CargarGuion_InvalidoEnEpisodioSinGuion_NoGuardaNada()
    {
        var ct = TestContext.Current.CancellationToken;
        var (serieId, episodioId) = await SembrarAsync("Ana");

        var resultado = await _servicio.CargarAsync(serieId, episodioId, GuionInvalido, ct);

        Assert.False(resultado.EsExito);
        Assert.Null((await FotografiarAsync(episodioId)).Guion);
        Assert.Equal((0, 0, 0, 0, 0), await ContarTodoAsync());
    }

    [Fact]
    public async Task CargarGuion_ErroresTraducidosConLineaYEnOrden()
    {
        var ct = TestContext.Current.CancellationToken;
        var (serieId, episodioId) = await SembrarAsync();
        var esperados = GuionParser.Parsear(GuionInvalido).Errores;
        Assert.Equal(2, esperados.Count);

        var resultado = await _servicio.CargarAsync(serieId, episodioId, GuionInvalido, ct);

        Assert.Equal(esperados.Select(e => new Error(e.Motivo, Linea: e.Linea)), resultado.Errores);
        Assert.True(resultado.Errores[0].Linea < resultado.Errores[1].Linea);
    }

    [Fact]
    public async Task CargarGuion_Valido_EscenasEnPendienteYSinClienteGemini()
    {
        var ct = TestContext.Current.CancellationToken;
        var (serieId, episodioId) = await SembrarAsync("Ana");

        var resultado = await _servicio.CargarAsync(serieId, episodioId, GuionDe(2), ct);

        Assert.True(resultado.EsExito);
        var foto = await FotografiarAsync(episodioId);
        Assert.Equal(2, foto.Escenas.Count);
        Assert.All(foto.Escenas, e => Assert.Equal(EstadoGeneracion.Pendiente, e.Estado));

        // AC-30: ninguna dependencia del servicio es (ni contiene) un cliente de Gemini.
        var dependencias = typeof(ServicioGuiones).GetConstructors()
            .SelectMany(c => c.GetParameters())
            .SelectMany(p => Desplegar(p.ParameterType))
            .ToList();
        Assert.DoesNotContain(dependencias, t => t.Name == "IClienteGemini");
    }

    private static IEnumerable<Type> Desplegar(Type tipo) =>
        tipo.HasElementType && tipo.GetElementType() is { } elemento
            ? Desplegar(elemento)
            : [tipo, .. tipo.GetGenericArguments().SelectMany(Desplegar)];

    [Fact]
    public async Task CargarGuion_GuardaPersonajesPorClipYPorEscena()
    {
        var ct = TestContext.Current.CancellationToken;
        var (serieId, episodioId) = await SembrarAsync("Ana", "José Daniel", "Bruno", "Carla");
        var texto = GuionesDePrueba.Unir(
            "ESCENA 1 — INT. CASA — DÍA",
            "LOCACIÓN: Sala.",
            "ILUMINACIÓN: Luz de día.",
            "PUESTA EN ESCENA: Bruno espera en la puerta.",
            "AUDIO: Silencio.",
            "CLIP 1",
            "Ana entra.",
            "CLIP 2",
            "JOSÉ DANIEL: \"Hola, Carla.\"",
            "ESCENA 2 — EXT. CALLE — NOCHE",
            "LOCACIÓN: Calle.",
            "ILUMINACIÓN: Farolas.",
            "PUESTA EN ESCENA: Nadie a la vista.",
            "AUDIO: Tráfico.",
            "CLIP 1",
            "Un auto pasa.");

        var resultado = await _servicio.CargarAsync(serieId, episodioId, texto, ct);

        Assert.True(resultado.EsExito);
        var foto = await FotografiarAsync(episodioId);
        Assert.Equal(["Ana", "Bruno", "José Daniel"], foto.Escenas[0].Personajes);
        Assert.Empty(foto.Escenas[1].Personajes);
        Assert.Equal(["Ana"], foto.Clips.Single(c => c.Escena == 1 && c.Numero == 1).Personajes);
        Assert.Equal(["José Daniel"], foto.Clips.Single(c => c.Escena == 1 && c.Numero == 2).Personajes);
        Assert.Empty(foto.Clips.Single(c => c.Escena == 2).Personajes);
        await using var contexto = _base.CrearContexto();
        Assert.Equal(DatosIntExt.Ext, (await contexto.Escenas.AsNoTracking().SingleAsync(e => e.Numero == 2, ct)).IntExt);
    }

    [Fact]
    public async Task CargarGuion_FalloAlGuardar_RollbackYEpisodioSinCambios()
    {
        var ct = TestContext.Current.CancellationToken;
        var (serieId, episodioId) = await SembrarAsync("Ana", "José Daniel");
        var anterior = GuionesDePrueba.Unir([.. GuionesDePrueba.EscenaDeEjemplo.Split('\n'), .. GuionesDePrueba.EscenaMinima(2)]);
        Assert.True((await _servicio.CargarAsync(serieId, episodioId, anterior, ct)).EsExito);
        var antes = await FotografiarAsync(episodioId);
        var conteoAntes = await ContarTodoAsync();
        var interceptor = new InterceptorQueCuentaYFalla(episodioId);
        var servicioQueFalla = new ServicioGuiones(new FabricaConInterceptores(_base.Conexion, interceptor), _registro);

        var resultado = await servicioQueFalla.CargarAsync(serieId, episodioId, GuionDe(1), ct);

        Assert.Equal(new Error("No se pudo guardar el guion; no se modificó nada"), Assert.Single(resultado.Errores));
        // Dentro de la transacción, al guardar, las escenas anteriores ya estaban borradas: el rollback las restauró.
        Assert.Equal(0, interceptor.EscenasAlGuardar);
        var despues = await FotografiarAsync(episodioId);
        Assert.Equal(anterior, despues.Guion);
        Assert.Equal(antes.Escenas.Select(e => (e.Id, e.Numero, string.Join(",", e.Personajes))),
            despues.Escenas.Select(e => (e.Id, e.Numero, string.Join(",", e.Personajes))));
        Assert.Equal(antes.Clips.Select(c => (c.Id, c.Accion, string.Join(",", c.Personajes))),
            despues.Clips.Select(c => (c.Id, c.Accion, string.Join(",", c.Personajes))));
        Assert.Equal(antes.Lineas, despues.Lineas);
        Assert.Equal(conteoAntes, await ContarTodoAsync());
        Assert.Contains(_registro.Entradas, e => e.Nivel == LogLevel.Error);
    }

    [Fact]
    public async Task CargarGuion_RegistraCadaCargaEnInformationSinElContenido()
    {
        var ct = TestContext.Current.CancellationToken;
        var (serieId, episodioId) = await SembrarAsync("Ana");

        var servicioQueFalla = new ServicioGuiones(
            new FabricaConInterceptores(_base.Conexion, new InterceptorQueFallaAlGuardar()), _registro);

        await _servicio.CargarAsync(serieId, episodioId, GuionesDePrueba.EscenaDeEjemplo, ct);
        await _servicio.CargarAsync(serieId, episodioId, GuionInvalido, ct);
        await servicioQueFalla.CargarAsync(serieId, episodioId, GuionesDePrueba.EscenaDeEjemplo, ct);

        var informacion = _registro.Entradas.Where(e => e.Nivel == LogLevel.Information).ToList();
        Assert.Equal(3, informacion.Count);
        Assert.All(informacion, e => Assert.Contains(episodioId.ToString(), e.Mensaje));
        Assert.Contains("guardado", informacion[0].Mensaje);
        Assert.Contains("rechazado", informacion[1].Mensaje);
        Assert.Contains("fallido", informacion[2].Mensaje);
        string[] fragmentos = ["RESTAURANTE", "Mentiroso", "Ana", "ESCENA", "sofá", "LOCACIÓN"];
        Assert.All(_registro.Entradas, e => Assert.All(fragmentos, f => Assert.DoesNotContain(f, e.Mensaje)));
    }

    [Fact]
    public void ConvertirIntExt_CubreTodosLosValores()
    {
        var valores = Enum.GetValues<GuionesIntExt>();
        Assert.Equal(Enum.GetNames<DatosIntExt>().Length, valores.Length);

        foreach (var valor in valores)
        {
            Assert.Equal(valor.ToString(), ServicioGuiones.ConvertirIntExt(valor).ToString());
        }

        Assert.Equal(DatosIntExt.Int, ServicioGuiones.ConvertirIntExt(GuionesIntExt.Int));
        Assert.Equal(DatosIntExt.Ext, ServicioGuiones.ConvertirIntExt(GuionesIntExt.Ext));
        Assert.Throws<ArgumentOutOfRangeException>(() => ServicioGuiones.ConvertirIntExt((GuionesIntExt)99));
    }

    [Fact]
    public async Task CargarGuion_TextoNull_FaltaElGuion()
    {
        var ct = TestContext.Current.CancellationToken;
        var (serieId, episodioId) = await SembrarAsync();

        var resultado = await _servicio.CargarAsync(serieId, episodioId, null!, ct);

        Assert.Equal(new Error("Falta el guion", Campo: "Guion"), Assert.Single(resultado.Errores));
        Assert.Null((await FotografiarAsync(episodioId)).Guion);
    }

    [Fact]
    public async Task CargarGuion_FalloDeBaseFueraDeLaTransaccion_DevuelveErrorSinLanzar()
    {
        var ct = TestContext.Current.CancellationToken;
        var servicio = new ServicioGuiones(new FabricaSinBase(), _registro);

        var resultado = await servicio.CargarAsync(1, 1, GuionDe(1), ct);

        Assert.Equal(new Error("No se pudo guardar el guion; no se modificó nada"), Assert.Single(resultado.Errores));
        Assert.Contains(_registro.Entradas, e => e.Nivel == LogLevel.Error);
        Assert.Contains(_registro.Entradas, e => e.Nivel == LogLevel.Information && e.Mensaje.Contains("fallido"));
    }

    /// <summary>
    /// Al guardar, cuenta en la base (dentro de la transacción del servicio) las escenas del
    /// episodio, y luego lanza <see cref="DbUpdateException"/>.
    /// </summary>
    private sealed class InterceptorQueCuentaYFalla(int episodioId) : SaveChangesInterceptor
    {
        public int? EscenasAlGuardar { get; private set; }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) =>
            throw new DbUpdateException("Fallo simulado al guardar");

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            EscenasAlGuardar = await eventData.Context!.Set<Escena>()
                .CountAsync(e => e.EpisodioId == episodioId, cancellationToken);
            throw new DbUpdateException("Fallo simulado al guardar");
        }
    }
}
