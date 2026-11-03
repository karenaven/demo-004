using Microsoft.Extensions.Logging;
using RodajeIA.Tests.Datos;
using RodajeIA.Web.Servicios;

namespace RodajeIA.Tests.Servicios;

public sealed class ServicioSeriesTests : IDisposable
{
    private readonly BaseDeDatosEnMemoria _base = new();
    private readonly RegistroDePrueba<ServicioSeries> _registro = new();
    private readonly ServicioSeries _servicio;

    public ServicioSeriesTests() => _servicio = new ServicioSeries(_base.Fabrica, _registro);

    public void Dispose() => _base.Dispose();

    [Fact]
    public async Task CrearSeries_ListarDevuelveLasDos()
    {
        var ct = TestContext.Current.CancellationToken;

        var b = await _servicio.CrearAsync("Serie B", ct);
        var a = await _servicio.CrearAsync("  Serie A  ", ct);

        Assert.True(b.EsExito);
        Assert.True(a.EsExito);
        Assert.Equal("Serie A", a.Valor.Nombre);
        var series = await _servicio.ListarAsync(ct);
        Assert.Equal(["Serie A", "Serie B"], series.Select(s => s.Nombre));
        Assert.Equal([a.Valor.Id, b.Valor.Id], series.Select(s => s.Id));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CrearSerie_NombreVacio_Falla(string nombre)
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await _servicio.CrearAsync(nombre, ct);

        Assert.False(resultado.EsExito);
        var error = Assert.Single(resultado.Errores);
        Assert.Equal(new Error("El nombre es obligatorio", Campo: "Nombre"), error);
        Assert.Empty(await _servicio.ListarAsync(ct));
    }

    [Fact]
    public async Task CrearSerie_NombreDeMasDe100_Falla()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await _servicio.CrearAsync(new string('a', 101), ct);

        var error = Assert.Single(resultado.Errores);
        Assert.Equal("Nombre", error.Campo);
        Assert.Contains("máximo 100", error.Motivo);
        Assert.Empty(await _servicio.ListarAsync(ct));
    }

    [Fact]
    public async Task CrearSerie_FalloDeBase_DevuelveErrorSinLanzar()
    {
        var ct = TestContext.Current.CancellationToken;
        var servicio = new ServicioSeries(new FabricaSinBase(), _registro);

        var resultado = await servicio.CrearAsync("Serie A", ct);

        Assert.Equal(new Error("No se pudo guardar la serie"), Assert.Single(resultado.Errores));
        Assert.Contains(_registro.Entradas, e => e.Nivel == LogLevel.Error);
    }
}
