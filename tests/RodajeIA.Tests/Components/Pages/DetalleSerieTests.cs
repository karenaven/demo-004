using Bunit;
using Microsoft.Extensions.DependencyInjection;
using RodajeIA.Tests.Datos;
using RodajeIA.Web.Components.Pages;
using RodajeIA.Web.Servicios;

namespace RodajeIA.Tests.Components.Pages;

public sealed class DetalleSerieTests : BunitContext
{
    private readonly BaseDeDatosEnMemoria _base = new();

    public DetalleSerieTests()
    {
        Services.AddLogging();
        Services.AddSingleton(_base.Fabrica);
        Services.AddScoped<ServicioSeries>();
        Services.AddScoped<ServicioPersonajes>();
        Services.AddScoped<ServicioEpisodios>();
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

    private async Task<int> CrearSerieAsync(string nombre)
    {
        var resultado = await Services.GetRequiredService<ServicioSeries>().CrearAsync(nombre, Ct);
        Assert.True(resultado.EsExito);
        return resultado.Valor.Id;
    }

    [Fact]
    public async Task DetalleSerie_CrearDosEpisodios_ListaMuestraLosDos()
    {
        var serieId = await CrearSerieAsync("Serie A");
        var pagina = Render<DetalleSerie>(p => p.Add(c => c.SerieId, serieId));
        pagina.WaitForElement("#form-episodio");

        foreach (var (numero, titulo) in new[] { ("1", "Piloto"), ("2", "La cena") })
        {
            pagina.Find("#numero-episodio").Change(numero);
            pagina.Find("#titulo-episodio").Change(titulo);
            await pagina.Find("#form-episodio").SubmitAsync();
        }

        pagina.WaitForAssertion(() =>
        {
            var items = pagina.FindAll("#lista-episodios li").Select(i => i.TextContent).ToList();
            Assert.Equal(2, items.Count);
            Assert.Contains("Piloto", items[0]);
            Assert.Contains("La cena", items[1]);
        });

        var guardados = await Services.GetRequiredService<ServicioEpisodios>().ListarAsync(serieId, Ct);
        Assert.Equal(["Piloto", "La cena"], guardados.Select(e => e.Titulo));
    }

    [Fact]
    public void DetalleSerie_SerieInexistente_MuestraMensaje()
    {
        var pagina = Render<DetalleSerie>(p => p.Add(c => c.SerieId, 999));

        pagina.WaitForAssertion(() =>
        {
            Assert.Contains("La serie no existe", pagina.Markup);
            Assert.NotEmpty(pagina.FindAll("a[href='/series']"));
            Assert.Empty(pagina.FindAll("#form-personaje"));
            Assert.Empty(pagina.FindAll("#form-episodio"));
        });
    }

    [Fact]
    public async Task DetalleSerie_PersonajeSinPeinado_MuestraFaltaElPeinado()
    {
        var serieId = await CrearSerieAsync("Serie A");
        var pagina = Render<DetalleSerie>(p => p.Add(c => c.SerieId, serieId));
        pagina.WaitForElement("#form-personaje");

        pagina.Find("#personaje-Nombre").Change("Ana");
        pagina.Find("#personaje-Edad").Change("30");
        pagina.Find("#personaje-DescripcionFisica").Change("Alta, ojos verdes.");
        pagina.Find("#personaje-Vestuario").Change("Vestido rojo.");
        pagina.Find("#personaje-Personalidad").Change("Ingeniosa.");
        pagina.Find("#personaje-Rol").Change("Protagonista.");
        pagina.Find("#form-personaje").Submit();

        pagina.WaitForAssertion(() =>
        {
            var error = pagina.Find("[data-campo='Peinado']");
            Assert.Contains("Falta el peinado", error.TextContent);
        });

        // No se guardó ni se listó, y el formulario conserva lo escrito para corregirlo.
        Assert.Empty(pagina.FindAll("#lista-personajes li"));
        Assert.Equal("Ana", pagina.Find("#personaje-Nombre").GetAttribute("value"));
        var guardados = await Services.GetRequiredService<ServicioPersonajes>().ListarAsync(serieId, Ct);
        Assert.Empty(guardados);
    }
}
