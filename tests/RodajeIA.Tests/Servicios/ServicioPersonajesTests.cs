using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RodajeIA.Tests.Datos;
using RodajeIA.Web.Datos;
using RodajeIA.Web.Servicios;

namespace RodajeIA.Tests.Servicios;

public sealed class ServicioPersonajesTests : IDisposable
{
    private readonly BaseDeDatosEnMemoria _base = new();
    private readonly RegistroDePrueba<ServicioPersonajes> _registro = new();
    private readonly ServicioPersonajes _servicio;

    public ServicioPersonajesTests() => _servicio = new ServicioPersonajes(_base.Fabrica, _registro);

    public void Dispose() => _base.Dispose();

    private static DatosPersonaje Completo(string nombre = "Ana") => new(
        Nombre: nombre,
        Edad: "34 años",
        DescripcionFisica: "Mujer delgada, piel morena, ojos cafés; voz grave y pausada.",
        Peinado: "Cabello negro lacio a los hombros, raya al lado.",
        Vestuario: "Blusa blanca de lino y pantalón azul marino.",
        HeridasMarcas: "Cicatriz fina en la ceja izquierda.",
        Personalidad: "Irónica, observadora, cálida con los suyos.",
        Rol: "Protagonista");

    private async Task<int> CrearSerieAsync(string nombre = "Rodaje")
    {
        await using var contexto = _base.CrearContexto();
        var serie = new Serie { Nombre = nombre };
        contexto.Series.Add(serie);
        await contexto.SaveChangesAsync(TestContext.Current.CancellationToken);
        return serie.Id;
    }

    private async Task<List<Personaje>> PersonajesGuardadosAsync()
    {
        await using var contexto = _base.CrearContexto();
        return await contexto.Personajes.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CrearPersonaje_TodosLosCampos_SeGuardaConEsosValores()
    {
        var ct = TestContext.Current.CancellationToken;
        var serieId = await CrearSerieAsync();
        var datos = Completo();

        var resultado = await _servicio.CrearAsync(serieId, datos, ct);

        Assert.True(resultado.EsExito);
        var guardado = Assert.Single(await PersonajesGuardadosAsync());
        Assert.Equal(resultado.Valor.Id, guardado.Id);
        Assert.Equal(serieId, guardado.SerieId);
        Assert.Equal(datos.Nombre, guardado.Nombre);
        Assert.Equal(datos.Edad, guardado.Edad);
        Assert.Equal(datos.DescripcionFisica, guardado.DescripcionFisica);
        Assert.Equal(datos.Peinado, guardado.Peinado);
        Assert.Equal(datos.Vestuario, guardado.Vestuario);
        Assert.Equal(datos.HeridasMarcas, guardado.HeridasMarcas);
        Assert.Equal(datos.Personalidad, guardado.Personalidad);
        Assert.Equal(datos.Rol, guardado.Rol);
        Assert.Equal(
            new PersonajeResumen(guardado.Id, serieId, datos.Nombre, datos.Edad, datos.DescripcionFisica,
                datos.Peinado, datos.Vestuario, datos.HeridasMarcas, datos.Personalidad, datos.Rol),
            resultado.Valor);
    }

    [Fact]
    public async Task CrearPersonaje_RecortaEspaciosEnLosExtremos()
    {
        var ct = TestContext.Current.CancellationToken;
        var serieId = await CrearSerieAsync();

        var resultado = await _servicio.CrearAsync(serieId, Completo() with { Nombre = "  Ana  ", Rol = " Protagonista\t" }, ct);

        Assert.True(resultado.EsExito);
        var guardado = Assert.Single(await PersonajesGuardadosAsync());
        Assert.Equal("Ana", guardado.Nombre);
        Assert.Equal("Protagonista", guardado.Rol);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CrearPersonaje_SinHeridas_SeGuarda(string? heridas)
    {
        var ct = TestContext.Current.CancellationToken;
        var serieId = await CrearSerieAsync();

        var resultado = await _servicio.CrearAsync(serieId, Completo() with { HeridasMarcas = heridas }, ct);

        Assert.True(resultado.EsExito);
        var guardado = Assert.Single(await PersonajesGuardadosAsync());
        Assert.Null(guardado.HeridasMarcas);
    }

    [Fact]
    public async Task CrearPersonaje_PeinadoVacio_NoSeGuardaYFaltaElPeinado()
    {
        var ct = TestContext.Current.CancellationToken;
        var serieId = await CrearSerieAsync();

        var resultado = await _servicio.CrearAsync(serieId, Completo() with { Peinado = "  " }, ct);

        Assert.False(resultado.EsExito);
        Assert.Equal(new Error("Falta el peinado", Campo: "Peinado"), Assert.Single(resultado.Errores));
        Assert.Empty(await PersonajesGuardadosAsync());
    }

    [Fact]
    public async Task CrearPersonaje_VariosCamposVacios_UnErrorPorCampo()
    {
        var ct = TestContext.Current.CancellationToken;
        var serieId = await CrearSerieAsync();
        var datos = new DatosPersonaje("", "", "", "", "", null, "", "");

        var resultado = await _servicio.CrearAsync(serieId, datos, ct);

        Assert.Equal(
            ["Nombre", "Edad", "DescripcionFisica", "Peinado", "Vestuario", "Personalidad", "Rol"],
            resultado.Errores.Select(e => e.Campo));
        Assert.All(resultado.Errores, e => Assert.StartsWith("Falta ", e.Motivo));
        Assert.Empty(await PersonajesGuardadosAsync());
    }

    [Theory]
    [InlineData("Nombre", 60)]
    [InlineData("Edad", 40)]
    [InlineData("DescripcionFisica", 2000)]
    [InlineData("Peinado", 300)]
    [InlineData("Vestuario", 500)]
    [InlineData("HeridasMarcas", 500)]
    [InlineData("Personalidad", 1000)]
    [InlineData("Rol", 200)]
    public async Task CrearPersonaje_CampoDemasiadoLargo_Falla(string campo, int maximo)
    {
        var ct = TestContext.Current.CancellationToken;
        var serieId = await CrearSerieAsync();
        var largo = new string('a', maximo + 1);
        var datos = campo switch
        {
            "Nombre" => Completo() with { Nombre = largo },
            "Edad" => Completo() with { Edad = largo },
            "DescripcionFisica" => Completo() with { DescripcionFisica = largo },
            "Peinado" => Completo() with { Peinado = largo },
            "Vestuario" => Completo() with { Vestuario = largo },
            "HeridasMarcas" => Completo() with { HeridasMarcas = largo },
            "Personalidad" => Completo() with { Personalidad = largo },
            "Rol" => Completo() with { Rol = largo },
            _ => throw new ArgumentOutOfRangeException(nameof(campo)),
        };

        var resultado = await _servicio.CrearAsync(serieId, datos, ct);

        var error = Assert.Single(resultado.Errores);
        Assert.Equal(campo, error.Campo);
        Assert.Contains($"máximo {maximo}", error.Motivo);
        Assert.Empty(await PersonajesGuardadosAsync());

        var justo = await _servicio.CrearAsync(serieId, datos with
        {
            Nombre = campo == "Nombre" ? new string('a', maximo) : datos.Nombre,
            Edad = campo == "Edad" ? new string('a', maximo) : datos.Edad,
            DescripcionFisica = campo == "DescripcionFisica" ? new string('a', maximo) : datos.DescripcionFisica,
            Peinado = campo == "Peinado" ? new string('a', maximo) : datos.Peinado,
            Vestuario = campo == "Vestuario" ? new string('a', maximo) : datos.Vestuario,
            HeridasMarcas = campo == "HeridasMarcas" ? new string('a', maximo) : datos.HeridasMarcas,
            Personalidad = campo == "Personalidad" ? new string('a', maximo) : datos.Personalidad,
            Rol = campo == "Rol" ? new string('a', maximo) : datos.Rol,
        }, ct);
        Assert.True(justo.EsExito);
    }

    [Fact]
    public async Task CrearPersonaje_SerieInexistente_Falla()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await _servicio.CrearAsync(999, Completo(), ct);

        Assert.Equal(new Error("La serie no existe"), Assert.Single(resultado.Errores));
        Assert.Empty(await PersonajesGuardadosAsync());
    }

    [Fact]
    public async Task CrearPersonaje_NombreRepetidoEnLaSerie_NoSeGuardaYNoLanza()
    {
        var ct = TestContext.Current.CancellationToken;
        var serieId = await CrearSerieAsync();
        Assert.True((await _servicio.CrearAsync(serieId, Completo("Ana"), ct)).EsExito);

        var resultado = await _servicio.CrearAsync(serieId, Completo(" Ana "), ct);

        Assert.Equal(
            new Error("Ya existe un personaje con ese nombre en esta serie", Campo: "Nombre"),
            Assert.Single(resultado.Errores));
        Assert.Single(await PersonajesGuardadosAsync());
    }

    [Theory]
    [InlineData("Ana", "ANA")]
    [InlineData("ANA", "ana")]
    [InlineData("José", "JOSÉ")]
    [InlineData("José Daniel", "josé daniel")]
    public async Task CrearPersonaje_NombreQueSoloDifiereEnMayusculas_NoSeGuarda(string existente, string nuevo)
    {
        var ct = TestContext.Current.CancellationToken;
        var serieId = await CrearSerieAsync();
        Assert.True((await _servicio.CrearAsync(serieId, Completo(existente), ct)).EsExito);

        var resultado = await _servicio.CrearAsync(serieId, Completo(nuevo), ct);

        Assert.Equal(
            new Error("Ya existe un personaje con ese nombre en esta serie", Campo: "Nombre"),
            Assert.Single(resultado.Errores));
        Assert.Equal(existente, Assert.Single(await PersonajesGuardadosAsync()).Nombre);
    }

    [Fact]
    public async Task CrearPersonaje_FalloDeBase_DevuelveErrorSinLanzar()
    {
        var ct = TestContext.Current.CancellationToken;
        var servicio = new ServicioPersonajes(new FabricaSinBase(), _registro);

        var resultado = await servicio.CrearAsync(1, Completo(), ct);

        Assert.Equal(new Error("No se pudo guardar la hoja de personaje"), Assert.Single(resultado.Errores));
        Assert.Contains(_registro.Entradas, e => e.Nivel == LogLevel.Error);
    }

    [Fact]
    public async Task CrearPersonaje_MismoNombreEnOtraSerie_SeGuarda()
    {
        var ct = TestContext.Current.CancellationToken;
        var serieA = await CrearSerieAsync("Serie A");
        var serieB = await CrearSerieAsync("Serie B");
        Assert.True((await _servicio.CrearAsync(serieA, Completo("Ana"), ct)).EsExito);

        var resultado = await _servicio.CrearAsync(serieB, Completo("Ana"), ct);

        Assert.True(resultado.EsExito);
        Assert.Equal(2, (await PersonajesGuardadosAsync()).Count);
    }

    [Fact]
    public async Task CrearPersonaje_NombreNoNFC_SeRechazaSinNormalizar()
    {
        var ct = TestContext.Current.CancellationToken;
        var serieId = await CrearSerieAsync();
        Assert.True((await _servicio.CrearAsync(serieId, Completo("José Daniel"), ct)).EsExito);

        // «José Daniel» en NFD: «e» + tilde combinante (U+0301).
        var resultado = await _servicio.CrearAsync(serieId, Completo("Jose\u0301 Daniel"), ct);

        var error = Assert.Single(resultado.Errores);
        Assert.Equal("Nombre", error.Campo);
        Assert.Contains("NFD", error.Motivo);
        var guardado = Assert.Single(await PersonajesGuardadosAsync());
        Assert.Equal("José Daniel", guardado.Nombre);
    }

    // ---------- Caracteres del nombre (RF-01i, FIX-001) ----------

    private const string FraseCaracteresPermitidos =
        "letras, dígitos, espacio, guion (-), apóstrofo (' o ’) y punto, empezando con una letra";

    [Theory]
    [InlineData("Ana (niña)")]
    [InlineData("-Ana")]
    [InlineData("1Ana")]
    [InlineData("'Ana")]
    [InlineData("Ana/María")]
    public async Task CrearPersonaje_NombreConCaracteresNoPermitidos_SeRechaza(string nombre)
    {
        // AC-01l
        var ct = TestContext.Current.CancellationToken;
        var serieId = await CrearSerieAsync();

        var resultado = await _servicio.CrearAsync(serieId, Completo(nombre), ct);

        Assert.False(resultado.EsExito);
        var error = Assert.Single(resultado.Errores);
        Assert.Equal("Nombre", error.Campo);
        Assert.Contains(FraseCaracteresPermitidos, error.Motivo);
        Assert.Empty(await PersonajesGuardadosAsync());
    }

    [Theory]
    [InlineData("Ana-María")]
    [InlineData("O'Brien")]
    [InlineData("O’Brien")]
    [InlineData("François")]
    [InlineData("St. James")]
    [InlineData("Ana 2")]
    public async Task CrearPersonaje_NombresConCaracteresPermitidos_SeGuardan(string nombre)
    {
        // AC-01m
        var ct = TestContext.Current.CancellationToken;
        var serieId = await CrearSerieAsync();

        var resultado = await _servicio.CrearAsync(serieId, Completo(nombre), ct);

        Assert.True(resultado.EsExito);
        Assert.Equal(nombre, Assert.Single(await PersonajesGuardadosAsync()).Nombre);
    }

    [Fact]
    public async Task CrearPersonaje_ApostrofoRectoYTipografico_SonRepetidos()
    {
        // M-02: «O'Brien» y «O’Brien» darían el mismo marcador de diálogo.
        var ct = TestContext.Current.CancellationToken;
        var serieId = await CrearSerieAsync();
        Assert.True((await _servicio.CrearAsync(serieId, Completo("O'Brien"), ct)).EsExito);

        var resultado = await _servicio.CrearAsync(serieId, Completo("O’Brien"), ct);

        var error = Assert.Single(resultado.Errores);
        Assert.Equal("Nombre", error.Campo);
        Assert.Equal(ServicioPersonajes.MotivoNombreRepetido, error.Motivo);
        Assert.Equal("O'Brien", Assert.Single(await PersonajesGuardadosAsync()).Nombre);
    }

    [Fact]
    public async Task ListarPersonajes_DosHojas_DevuelveLasDos()
    {
        var ct = TestContext.Current.CancellationToken;
        var serieId = await CrearSerieAsync("Serie A");
        var otraSerie = await CrearSerieAsync("Serie B");
        Assert.True((await _servicio.CrearAsync(serieId, Completo("José Daniel"), ct)).EsExito);
        Assert.True((await _servicio.CrearAsync(serieId, Completo("Ana"), ct)).EsExito);
        Assert.True((await _servicio.CrearAsync(otraSerie, Completo("Bruno"), ct)).EsExito);

        var personajes = await _servicio.ListarAsync(serieId, ct);

        Assert.Equal(["Ana", "José Daniel"], personajes.Select(p => p.Nombre));
        Assert.All(personajes, p => Assert.Equal(serieId, p.SerieId));
    }
}
