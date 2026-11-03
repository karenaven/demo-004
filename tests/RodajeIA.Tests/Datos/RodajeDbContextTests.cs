using Microsoft.EntityFrameworkCore;
using RodajeIA.Web.Datos;

namespace RodajeIA.Tests.Datos;

public sealed class RodajeDbContextTests : IDisposable
{
    private readonly BaseDeDatosEnMemoria _base = new();

    public void Dispose() => _base.Dispose();

    [Fact]
    public async Task EliminarEpisodio_BorraEscenasClipsYLineasEnCascada()
    {
        var ct = TestContext.Current.CancellationToken;
        int episodioId;
        await using (var contexto = _base.CrearContexto())
        {
            var serie = new Serie { Nombre = "Rodaje" };
            var ana = NuevoPersonaje(serie, "Ana");
            var episodio = new Episodio { Serie = serie, Numero = 1, Titulo = "Piloto", Guion = "ESCENA 1" };
            var escena = NuevaEscena(episodio, 1);
            escena.Personajes.Add(ana);
            var clip = new Clip { Escena = escena, Numero = 1, Accion = "Ana entra." };
            clip.Personajes.Add(ana);
            clip.LineasDialogo.Add(new LineaDialogo
            {
                Codigo = "c1-d1",
                Orden = 1,
                Personaje = "ANA",
                Texto = "Hola",
                Original = "ANA: \"Hola\"",
            });
            contexto.AddRange(serie, ana, episodio, escena, clip);
            await contexto.SaveChangesAsync(ct);
            episodioId = episodio.Id;
        }

        await using (var contexto = _base.CrearContexto())
        {
            var episodio = await contexto.Episodios.SingleAsync(e => e.Id == episodioId, ct);
            contexto.Episodios.Remove(episodio);
            await contexto.SaveChangesAsync(ct);
        }

        await using (var contexto = _base.CrearContexto())
        {
            Assert.Equal(0, await contexto.Escenas.CountAsync(ct));
            Assert.Equal(0, await contexto.Clips.CountAsync(ct));
            Assert.Equal(0, await contexto.LineasDialogo.CountAsync(ct));
            Assert.Equal(0, await contexto.Set<Dictionary<string, object>>("EscenaPersonaje").CountAsync(ct));
            Assert.Equal(0, await contexto.Set<Dictionary<string, object>>("ClipPersonaje").CountAsync(ct));
            // La serie y la hoja del personaje no dependen del episodio: siguen ahí.
            Assert.Equal(1, await contexto.Series.CountAsync(ct));
            Assert.Equal(1, await contexto.Personajes.CountAsync(ct));
        }
    }

    [Fact]
    public async Task EpisodioConNumeroRepetidoEnLaSerie_LanzaDbUpdateException()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var contexto = _base.CrearContexto();
        var serie = new Serie { Nombre = "Rodaje" };
        contexto.Episodios.Add(new Episodio { Serie = serie, Numero = 1, Titulo = "Piloto" });
        await contexto.SaveChangesAsync(ct);

        contexto.Episodios.Add(new Episodio { SerieId = serie.Id, Numero = 1, Titulo = "Otro piloto" });

        await Assert.ThrowsAsync<DbUpdateException>(() => contexto.SaveChangesAsync(ct));
    }

    [Fact]
    public async Task EpisodioConMismoNumeroEnOtraSerie_SeGuarda()
    {
        var ct = TestContext.Current.CancellationToken;
        await using (var contexto = _base.CrearContexto())
        {
            contexto.Episodios.Add(new Episodio { Serie = new Serie { Nombre = "Rodaje" }, Numero = 1, Titulo = "Piloto" });
            contexto.Episodios.Add(new Episodio { Serie = new Serie { Nombre = "Otra" }, Numero = 1, Titulo = "Piloto" });
            await contexto.SaveChangesAsync(ct);
        }

        await using (var contexto = _base.CrearContexto())
        {
            Assert.Equal(2, await contexto.Episodios.CountAsync(e => e.Numero == 1, ct));
        }
    }

    [Fact]
    public async Task LineaDialogoConCodigoRepetidoEnElClip_LanzaDbUpdateException()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var contexto = _base.CrearContexto();
        var episodio = new Episodio { Serie = new Serie { Nombre = "Rodaje" }, Numero = 1, Titulo = "Piloto" };
        var clip = new Clip { Escena = NuevaEscena(episodio, 1), Numero = 1, Accion = "" };
        clip.LineasDialogo.Add(new LineaDialogo { Codigo = "c1-d1", Orden = 1, Personaje = "ANA", Texto = "Hola", Original = "ANA: \"Hola\"" });
        contexto.Clips.Add(clip);
        await contexto.SaveChangesAsync(ct);

        contexto.LineasDialogo.Add(new LineaDialogo { ClipId = clip.Id, Codigo = "c1-d1", Orden = 2, Personaje = "ANA", Texto = "Chau", Original = "ANA: \"Chau\"" });

        await Assert.ThrowsAsync<DbUpdateException>(() => contexto.SaveChangesAsync(ct));
    }

    [Fact]
    public async Task EscenaNueva_TieneEstadoPendientePorDefecto()
    {
        var ct = TestContext.Current.CancellationToken;
        await using (var contexto = _base.CrearContexto())
        {
            var episodio = new Episodio { Serie = new Serie { Nombre = "Rodaje" }, Numero = 1, Titulo = "Piloto" };
            contexto.Escenas.Add(NuevaEscena(episodio, 1));
            await contexto.SaveChangesAsync(ct);
        }

        await using (var contexto = _base.CrearContexto())
        {
            var escena = await contexto.Escenas.SingleAsync(ct);
            Assert.Equal(EstadoGeneracion.Pendiente, escena.Estado);
        }

        // El estado y INT/EXT se guardan como texto, no como número.
        await using (var comando = _base.Conexion.CreateCommand())
        {
            comando.CommandText = "SELECT Estado, IntExt FROM Escenas";
            await using var lector = await comando.ExecuteReaderAsync(ct);
            Assert.True(await lector.ReadAsync(ct));
            Assert.Equal("Pendiente", lector.GetString(0));
            Assert.Equal("Int", lector.GetString(1));
        }

        // El default vive en la columna: una fila insertada sin Estado también queda Pendiente.
        await using (var comando = _base.Conexion.CreateCommand())
        {
            comando.CommandText = "SELECT dflt_value FROM pragma_table_info('Escenas') WHERE name = 'Estado'";
            Assert.Equal("'Pendiente'", await comando.ExecuteScalarAsync(ct));
        }
    }

    [Fact]
    public async Task PersonajeSinHeridas_SeGuardaConHeridasNull()
    {
        var ct = TestContext.Current.CancellationToken;
        await using (var contexto = _base.CrearContexto())
        {
            var personaje = NuevoPersonaje(new Serie { Nombre = "Rodaje" }, "Ana");
            personaje.HeridasMarcas = null;
            contexto.Personajes.Add(personaje);
            await contexto.SaveChangesAsync(ct);
        }

        await using (var contexto = _base.CrearContexto())
        {
            var personaje = await contexto.Personajes.SingleAsync(ct);
            Assert.Equal("Ana", personaje.Nombre);
            Assert.Null(personaje.HeridasMarcas);

            var propiedad = contexto.Model.FindEntityType(typeof(Personaje))!.FindProperty(nameof(Personaje.HeridasMarcas))!;
            Assert.True(propiedad.IsNullable);
            Assert.Equal(500, propiedad.GetMaxLength());
        }
    }

    private static Personaje NuevoPersonaje(Serie serie, string nombre) => new()
    {
        Serie = serie,
        Nombre = nombre,
        Edad = "30 años",
        DescripcionFisica = "Alta, voz grave.",
        Peinado = "Cola de caballo",
        Vestuario = "Chaqueta roja",
        Personalidad = "Decidida",
        Rol = "Protagonista",
    };

    private static Escena NuevaEscena(Episodio episodio, int numero) => new()
    {
        Episodio = episodio,
        Numero = numero,
        IntExt = IntExt.Int,
        Lugar = "COCINA",
        Momento = "NOCHE",
        Locacion = "Cocina pequeña.",
        Iluminacion = "Luz cálida.",
        PuestaEnEscena = "Ana junto a la mesa.",
        Audio = "Lluvia.",
    };
}
