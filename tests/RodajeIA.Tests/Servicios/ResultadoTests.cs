using RodajeIA.Web.Servicios;

namespace RodajeIA.Tests.Servicios;

public class ResultadoTests
{
    [Fact]
    public void Exito_DevuelveValorYSinErrores()
    {
        var resultado = Resultado<string>.Exito("guion");

        Assert.True(resultado.EsExito);
        Assert.Equal("guion", resultado.Valor);
        Assert.Empty(resultado.Errores);
    }

    [Fact]
    public void Fallo_ConErrores_ConservaLineaCampoYMotivo()
    {
        var resultado = Resultado<int>.Fallo(
        [
            new Error("Falta el campo AUDIO", Linea: 3),
            new Error("El nombre es obligatorio", Campo: "Nombre"),
        ]);

        Assert.False(resultado.EsExito);
        Assert.Collection(
            resultado.Errores,
            error =>
            {
                Assert.Equal("Falta el campo AUDIO", error.Motivo);
                Assert.Equal(3, error.Linea);
                Assert.Null(error.Campo);
            },
            error =>
            {
                Assert.Equal("El nombre es obligatorio", error.Motivo);
                Assert.Equal("Nombre", error.Campo);
                Assert.Null(error.Linea);
            });
    }

    [Fact]
    public void Fallo_SinErrores_LanzaArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Resultado<string>.Fallo([]));
    }

    [Fact]
    public void Fallo_Null_LanzaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Resultado<string>.Fallo(null!));
    }

    [Fact]
    public void Fallo_ValorEsDefault()
    {
        var conReferencia = Resultado<string>.Fallo([new Error("x")]);
        var conValor = Resultado<int>.Fallo([new Error("x")]);

        Assert.Null(conReferencia.Valor);
        Assert.Equal(0, conValor.Valor);
    }
}
