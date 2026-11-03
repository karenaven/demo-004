using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RodajeIA.Tests.Datos;
using RodajeIA.Tests.Guiones;
using RodajeIA.Web.Components.Pages;
using RodajeIA.Web.Guiones;
using RodajeIA.Web.Servicios;

namespace RodajeIA.Tests.Components.Pages;

public sealed class CargarGuionTests : BunitContext
{
    private readonly BaseDeDatosEnMemoria _base = new();

    public CargarGuionTests()
    {
        Services.AddLogging();
        Services.AddSingleton(_base.Fabrica);
        Services.AddScoped<ServicioSeries>();
        Services.AddScoped<ServicioEpisodios>();
        Services.AddScoped<ServicioGuiones>();
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

    private async Task<int> CrearEpisodioAsync(int serieId, int numero, string titulo)
    {
        var resultado = await Services.GetRequiredService<ServicioEpisodios>().CrearAsync(serieId, numero, titulo, Ct);
        Assert.True(resultado.EsExito);
        return resultado.Valor.Id;
    }

    private static IEnumerable<string> ValoresDe(IRenderedComponent<CargarGuion> pagina, string selector) =>
        pagina.FindAll(selector + " option")
            .Select(o => o.GetAttribute("value") ?? "")
            .Where(v => v.Length > 0);

    private static IEnumerable<string> TextosDe(IRenderedComponent<CargarGuion> pagina, string selector) =>
        pagina.FindAll(selector + " option")
            .Where(o => (o.GetAttribute("value") ?? "").Length > 0)
            .Select(o => o.TextContent.Trim());

    [Fact]
    public async Task CargarGuion_SelectorOfreceLasSeriesCreadas()
    {
        var a = await CrearSerieAsync("Serie A");
        var b = await CrearSerieAsync("Serie B");

        var pagina = Render<CargarGuion>();

        pagina.WaitForAssertion(() =>
        {
            Assert.Equal([a.ToString(), b.ToString()], ValoresDe(pagina, "#selector-serie"));
            Assert.Equal(["Serie A", "Serie B"], TextosDe(pagina, "#selector-serie"));
        });
    }

    [Fact]
    public async Task CargarGuion_SelectorDeEpisodioOfreceLosDeLaSerie()
    {
        var a = await CrearSerieAsync("Serie A");
        var b = await CrearSerieAsync("Serie B");
        await CrearEpisodioAsync(a, 1, "Piloto");
        await CrearEpisodioAsync(a, 2, "La cena");
        await CrearEpisodioAsync(b, 1, "Otro piloto");

        var pagina = Render<CargarGuion>();
        pagina.WaitForAssertion(() => Assert.Equal(2, ValoresDe(pagina, "#selector-serie").Count()));

        pagina.Find("#selector-serie").Change(a.ToString());

        pagina.WaitForAssertion(() =>
        {
            var textos = TextosDe(pagina, "#selector-episodio").ToList();
            Assert.Equal(2, textos.Count);
            Assert.Contains("Piloto", textos[0]);
            Assert.Contains("La cena", textos[1]);
            Assert.DoesNotContain(textos, t => t.Contains("Otro piloto"));
        });
    }

    [Fact]
    public async Task CargarGuion_SinSerieOEpisodio_BotonDeshabilitado()
    {
        var a = await CrearSerieAsync("Serie A");
        var b = await CrearSerieAsync("Serie B");
        var episodio = await CrearEpisodioAsync(a, 1, "Piloto");
        await CrearEpisodioAsync(b, 1, "Otro piloto");

        var pagina = Render<CargarGuion>();
        pagina.WaitForAssertion(() => Assert.Equal(2, ValoresDe(pagina, "#selector-serie").Count()));
        pagina.Find("#guion").Input(GuionesDePrueba.EscenaDeEjemplo);

        // Sin nada elegido.
        Assert.True(pagina.Find("#validar-y-guardar").HasAttribute("disabled"));

        // Solo serie.
        pagina.Find("#selector-serie").Change(a.ToString());
        pagina.WaitForAssertion(() => Assert.Single(ValoresDe(pagina, "#selector-episodio")));
        Assert.True(pagina.Find("#validar-y-guardar").HasAttribute("disabled"));

        // Serie y episodio: habilitado.
        pagina.Find("#selector-episodio").Change(episodio.ToString());
        pagina.WaitForAssertion(() => Assert.False(pagina.Find("#validar-y-guardar").HasAttribute("disabled")));

        // Cambiar de serie vacía el episodio elegido y vuelve a deshabilitar.
        pagina.Find("#selector-serie").Change(b.ToString());
        pagina.WaitForAssertion(() => Assert.True(pagina.Find("#validar-y-guardar").HasAttribute("disabled")));

        await using var contexto = _base.CrearContexto();
        Assert.False(await contexto.Escenas.AnyAsync(Ct));
        Assert.Null((await contexto.Episodios.SingleAsync(e => e.Id == episodio, Ct)).Guion);
    }

    [Fact]
    public async Task CargarGuion_PegarYUnSoloClic_GuardaElGuion()
    {
        var serie = await CrearSerieAsync("Serie A");
        var episodio = await CrearEpisodioAsync(serie, 1, "Piloto");

        var pagina = Render<CargarGuion>();
        pagina.WaitForAssertion(() => Assert.Single(ValoresDe(pagina, "#selector-serie")));
        pagina.Find("#selector-serie").Change(serie.ToString());
        pagina.WaitForAssertion(() => Assert.Single(ValoresDe(pagina, "#selector-episodio")));
        pagina.Find("#selector-episodio").Change(episodio.ToString());

        // Pegar (oninput) y un único clic, sin blur ni ninguna otra interacción.
        pagina.Find("#guion").Input(GuionesDePrueba.EscenaDeEjemplo);
        await pagina.Find("#validar-y-guardar").ClickAsync(new());

        pagina.WaitForAssertion(() =>
            Assert.Contains("Guion guardado: 1 escenas en estado pendiente", pagina.Find("#resultado-carga").TextContent));

        await using var contexto = _base.CrearContexto();
        var guardado = await contexto.Episodios.SingleAsync(e => e.Id == episodio, Ct);
        Assert.Equal(GuionesDePrueba.EscenaDeEjemplo, guardado.Guion);
        Assert.Equal(1, await contexto.Escenas.CountAsync(e => e.EpisodioId == episodio, Ct));
    }

    [Fact]
    public async Task CargarGuion_Invalido_MuestraErroresConLineaYConservaTexto()
    {
        var serie = await CrearSerieAsync("Serie A");
        var episodio = await CrearEpisodioAsync(serie, 1, "Piloto");
        // Escena 1 sin AUDIO: y escena 2 sin ningún CLIP: al menos dos errores con línea.
        var invalido = GuionesDePrueba.Unir(
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
        var esperados = GuionParser.Parsear(invalido).Errores
            .Select(e => $"Línea {e.Linea}: {e.Motivo}")
            .ToList();
        Assert.True(esperados.Count >= 2);

        var pagina = Render<CargarGuion>();
        pagina.WaitForAssertion(() => Assert.Single(ValoresDe(pagina, "#selector-serie")));
        pagina.Find("#selector-serie").Change(serie.ToString());
        pagina.WaitForAssertion(() => Assert.Single(ValoresDe(pagina, "#selector-episodio")));
        pagina.Find("#selector-episodio").Change(episodio.ToString());
        pagina.Find("#guion").Input(invalido);
        await pagina.Find("#validar-y-guardar").ClickAsync(new());

        pagina.WaitForAssertion(() =>
            Assert.Equal(esperados, pagina.FindAll("#errores-guion li").Select(li => li.TextContent.Trim())));

        Assert.Equal(invalido, pagina.Find("#guion").GetAttribute("value"));
        Assert.Empty(pagina.FindAll("#resultado-carga"));
        await using var contexto = _base.CrearContexto();
        Assert.Null((await contexto.Episodios.SingleAsync(e => e.Id == episodio, Ct)).Guion);
    }
}
