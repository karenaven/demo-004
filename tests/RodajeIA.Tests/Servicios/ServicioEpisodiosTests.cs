using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using RodajeIA.Tests.Datos;
using RodajeIA.Web.Datos;
using RodajeIA.Web.Servicios;

namespace RodajeIA.Tests.Servicios;

public sealed class ServicioEpisodiosTests : IDisposable
{
    private readonly BaseDeDatosEnMemoria _base = new();
    private readonly RegistroDePrueba<ServicioEpisodios> _registro = new();
    private readonly ServicioEpisodios _servicio;

    public ServicioEpisodiosTests() => _servicio = new ServicioEpisodios(_base.Fabrica, _registro);

    [Fact]
    public async Task CrearEpisodio_OtraAltaGanaLaCarreraConElMismoNumero_IndicaQueExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var serieId = await CrearSerieAsync();
        var servicio = new ServicioEpisodios(
            new FabricaConInterceptores(_base.Conexion, new InterceptorQueAdelantaElMismoEpisodio(_base)), _registro);

        var resultado = await servicio.CrearAsync(serieId, 1, "Piloto", ct);

        Assert.Equal(new Error("El número 1 ya existe en esta serie", Campo: "Numero"), Assert.Single(resultado.Errores));
        Assert.Equal(1, await ContarEpisodiosAsync());
    }

    [Fact]
    public async Task CrearEpisodio_FalloAlGuardarSinNumeroRepetido_ErrorGenerico()
    {
        var ct = TestContext.Current.CancellationToken;
        var serieId = await CrearSerieAsync();
        var servicio = new ServicioEpisodios(
            new FabricaConInterceptores(_base.Conexion, new InterceptorQueFallaAlGuardar()), _registro);

        var resultado = await servicio.CrearAsync(serieId, 1, "Piloto", ct);

        Assert.Equal(new Error("No se pudo guardar el episodio"), Assert.Single(resultado.Errores));
        Assert.Contains(_registro.Entradas, e => e.Nivel == LogLevel.Error);
        Assert.Equal(0, await ContarEpisodiosAsync());
    }

    [Fact]
    public async Task CrearEpisodio_FalloDeBase_DevuelveErrorSinLanzar()
    {
        var ct = TestContext.Current.CancellationToken;
        var servicio = new ServicioEpisodios(new FabricaSinBase(), _registro);

        var resultado = await servicio.CrearAsync(1, 1, "Piloto", ct);

        Assert.Equal(new Error("No se pudo guardar el episodio"), Assert.Single(resultado.Errores));
        Assert.Contains(_registro.Entradas, e => e.Nivel == LogLevel.Error);
    }

    /// <summary>
    /// Simula la carrera: justo antes de guardar, otro contexto inserta un episodio con la misma
    /// serie y número, así el guardado real choca con el índice único.
    /// </summary>
    private sealed class InterceptorQueAdelantaElMismoEpisodio(BaseDeDatosEnMemoria baseDeDatos) : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var nuevo = eventData.Context!.ChangeTracker.Entries<Episodio>().Single(e => e.State == EntityState.Added).Entity;
            await using var otro = baseDeDatos.CrearContexto();
            otro.Episodios.Add(new Episodio { SerieId = nuevo.SerieId, Numero = nuevo.Numero, Titulo = "Ganador" });
            await otro.SaveChangesAsync(cancellationToken);
            return result;
        }
    }

    public void Dispose() => _base.Dispose();

    private async Task<int> CrearSerieAsync(string nombre = "Rodaje")
    {
        await using var contexto = _base.CrearContexto();
        var serie = new Serie { Nombre = nombre };
        contexto.Series.Add(serie);
        await contexto.SaveChangesAsync(TestContext.Current.CancellationToken);
        return serie.Id;
    }

    private async Task<int> ContarEpisodiosAsync()
    {
        await using var contexto = _base.CrearContexto();
        return await contexto.Episodios.CountAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CrearEpisodios_ListarDevuelveLosDos()
    {
        var ct = TestContext.Current.CancellationToken;
        var serieId = await CrearSerieAsync();
        var otraSerie = await CrearSerieAsync("Otra");

        var dos = await _servicio.CrearAsync(serieId, 2, "La cena", ct);
        var uno = await _servicio.CrearAsync(serieId, 1, "  Piloto ", ct);
        Assert.True((await _servicio.CrearAsync(otraSerie, 1, "Ajeno", ct)).EsExito);

        Assert.True(uno.EsExito);
        Assert.True(dos.EsExito);
        var episodios = await _servicio.ListarAsync(serieId, ct);
        Assert.Equal(
            [new EpisodioResumen(uno.Valor.Id, serieId, 1, "Piloto"), new EpisodioResumen(dos.Valor.Id, serieId, 2, "La cena")],
            episodios);
    }

    [Fact]
    public async Task CrearEpisodio_NumeroRepetido_NoSeGuardaEIndicaQueExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var serieId = await CrearSerieAsync();
        Assert.True((await _servicio.CrearAsync(serieId, 1, "Piloto", ct)).EsExito);

        var resultado = await _servicio.CrearAsync(serieId, 1, "Otro", ct);

        Assert.Equal(new Error("El número 1 ya existe en esta serie", Campo: "Numero"), Assert.Single(resultado.Errores));
        Assert.Equal(1, await ContarEpisodiosAsync());
    }

    [Theory]
    [InlineData(0, "Piloto", "Numero")]
    [InlineData(-3, "Piloto", "Numero")]
    [InlineData(10000, "Piloto", "Numero")]
    [InlineData(1, "", "Titulo")]
    [InlineData(1, "   ", "Titulo")]
    public async Task CrearEpisodio_NumeroCeroOTituloVacio_Falla(int numero, string titulo, string campo)
    {
        var ct = TestContext.Current.CancellationToken;
        var serieId = await CrearSerieAsync();

        var resultado = await _servicio.CrearAsync(serieId, numero, titulo, ct);

        Assert.False(resultado.EsExito);
        Assert.Equal(campo, Assert.Single(resultado.Errores).Campo);
        Assert.Equal(0, await ContarEpisodiosAsync());
    }

    [Fact]
    public async Task CrearEpisodio_TituloDeMasDe150_Falla()
    {
        var ct = TestContext.Current.CancellationToken;
        var serieId = await CrearSerieAsync();

        var resultado = await _servicio.CrearAsync(serieId, 1, new string('t', 151), ct);

        var error = Assert.Single(resultado.Errores);
        Assert.Equal("Titulo", error.Campo);
        Assert.Contains("máximo 150", error.Motivo);
        Assert.Equal(0, await ContarEpisodiosAsync());
    }

    [Fact]
    public async Task CrearEpisodio_SerieInexistente_Falla()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await _servicio.CrearAsync(999, 1, "Piloto", ct);

        Assert.Equal(new Error("La serie no existe"), Assert.Single(resultado.Errores));
        Assert.Equal(0, await ContarEpisodiosAsync());
    }
}
