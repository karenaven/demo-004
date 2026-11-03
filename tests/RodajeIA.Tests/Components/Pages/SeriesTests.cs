using Bunit;
using Microsoft.Extensions.DependencyInjection;
using RodajeIA.Tests.Datos;
using RodajeIA.Web.Components.Pages;
using RodajeIA.Web.Servicios;

namespace RodajeIA.Tests.Components.Pages;

public sealed class SeriesTests : BunitContext
{
    private readonly BaseDeDatosEnMemoria _base = new();

    public SeriesTests()
    {
        Services.AddLogging();
        Services.AddSingleton(_base.Fabrica);
        Services.AddScoped<ServicioSeries>();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _base.Dispose();
        }

        base.Dispose(disposing);
    }

    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    [Fact]
    public async Task Series_CrearDos_ListadoMuestraLasDos()
    {
        var pagina = Render<Series>();

        foreach (var nombre in new[] { "Serie A", "Serie B" })
        {
            pagina.Find("#nombre-serie").Change(nombre);
            await pagina.Find("#form-serie").SubmitAsync();
        }

        pagina.WaitForAssertion(() =>
        {
            var items = pagina.FindAll("#lista-series li");
            Assert.Equal(["Serie A", "Serie B"], items.Select(i => i.TextContent.Trim()));
        });

        // El formulario queda limpio tras el alta y las series están guardadas de verdad.
        Assert.Equal("", pagina.Find("#nombre-serie").GetAttribute("value") ?? "");
        var guardadas = await Services.GetRequiredService<ServicioSeries>().ListarAsync(Ct);
        Assert.Equal(["Serie A", "Serie B"], guardadas.Select(s => s.Nombre));
    }
}
