using System.Diagnostics;
using System.Text;
using System.Text.Json;
using RodajeIA.Web.Guiones;
using static RodajeIA.Tests.Guiones.GuionesDePrueba;

namespace RodajeIA.Tests.Guiones;

public class GuionParserTests
{
    private const string MotivoSinEscena = "El guion no tiene ninguna escena (ESCENA N — …)";
    private const string MotivoEncabezadoInvalido =
        "Encabezado inválido: se espera ESCENA N — INT./EXT. LUGAR — MOMENTO";
    private const string MotivoClipInvalido = "Marcador de clip inválido: se espera CLIP N";
    private const string MotivoFueraDeLugar =
        "Línea fuera de lugar: entre el encabezado y el primer CLIP solo van los campos de escena";

    // ---------- Guion válido ----------

    [Fact]
    public void EscenaDeEjemplo_ObtieneEncabezadoCamposYClips()
    {
        var resultado = GuionParser.Parsear(EscenaDeEjemplo);

        Assert.Empty(resultado.Errores);
        Assert.NotNull(resultado.Guion);
        var escena = Assert.Single(resultado.Guion.Escenas);
        Assert.Equal(1, escena.Numero);
        Assert.Equal(1, escena.LineaEncabezado);
        Assert.Equal(IntExt.Int, escena.IntExt);
        Assert.Equal("RESTAURANTE", escena.Lugar);
        Assert.Equal("NOCHE", escena.Momento);
        Assert.Equal(Locacion, escena.Locacion);
        Assert.Equal(Iluminacion, escena.Iluminacion);
        Assert.Equal(PuestaEnEscena, escena.PuestaEnEscena);
        Assert.Equal(Audio, escena.Audio);
        Assert.Equal(2, escena.Clips.Count);

        var clip1 = escena.Clips[0];
        Assert.Equal(1, clip1.Numero);
        Assert.Equal(6, clip1.Linea);
        Assert.Equal(
            [
                "Ana y José Daniel conversan en una mesa junto a la ventana. José Daniel sonríe pícaramente.",
                "Ana niega con la cabeza, divertida, con una sonrisa amplia.",
            ],
            clip1.LineasAccion);
        Assert.Collection(
            clip1.Dialogos,
            d =>
            {
                Assert.Equal("ANA", d.Personaje);
                Assert.Null(d.Acotacion);
                Assert.Equal("Mentiroso.", d.Texto);
                Assert.Equal("ANA: \"Mentiroso.\"", d.Original);
            },
            d =>
            {
                Assert.Equal("JOSÉ DANIEL", d.Personaje);
                Assert.Equal("sonriendo", d.Acotacion);
                Assert.Equal("Bueno... pasó parecido.", d.Texto);
                Assert.Equal("JOSÉ DANIEL (sonriendo): \"Bueno... pasó parecido.\"", d.Original);
            });
        Assert.Null(clip1.TextoEnPantalla);

        var clip2 = escena.Clips[1];
        Assert.Equal(2, clip2.Numero);
        Assert.Equal(11, clip2.Linea);
        Assert.Equal(
            ["Un mesero deja dos platos sobre la mesa. Ana y José Daniel agradecen con gestos breves."],
            clip2.LineasAccion);
        Assert.Empty(clip2.Dialogos);
    }

    [Fact]
    public void EscenaDeEjemplo_ConGuionComunYLineasEnBlanco_IgualCampoPorCampo()
    {
        var original = GuionParser.Parsear(EscenaDeEjemplo);
        var variante = GuionParser.Parsear(EscenaDeEjemploConGuionComunYLineasEnBlanco);

        Assert.Empty(variante.Errores);
        Assert.NotNull(original.Guion);
        Assert.NotNull(variante.Guion);
        Assert.NotEmpty(original.Guion.Escenas);
        Assert.Equal(DescribirSinLineas(original.Guion), DescribirSinLineas(variante.Guion));
        // Las líneas en blanco cuentan para el número de línea.
        Assert.Equal(2, variante.Guion.Escenas[0].LineaEncabezado);
        Assert.Equal(12, variante.Guion.Escenas[0].Clips[0].Linea);

        // Un BOM inicial se quita al leer: mismo resultado, incluidos los números de línea.
        var conBom = GuionParser.Parsear("﻿" + EscenaDeEjemplo);
        Assert.Empty(conBom.Errores);
        Assert.Equal(JsonSerializer.Serialize(original.Guion), JsonSerializer.Serialize(conBom.Guion));
    }

    [Fact]
    public void AcotacionConDosPuntosOParentesisInternos_CierraEnElUltimoParentesisSeguidoDeDosPuntos()
    {
        // Semántica de la regex del spec ^NOMBRE(\s*\((.*)\))?\s*:\s*(.+)$ con (.*) codicioso.
        (string Linea, string Acotacion, string Texto)[] casos =
        [
            ("ANA (al teléfono: susurrando): \"Ya voy.\"", "al teléfono: susurrando", "Ya voy."),
            ("ANA (mira (de reojo)): \"x\"", "mira (de reojo)", "x"),
            ("ANA (sonriendo) : \"Hola (bis)\"", "sonriendo", "Hola (bis)"),
        ];

        foreach (var (linea, acotacion, texto) in casos)
        {
            var resultado = GuionParser.Parsear(Unir([.. EscenaMinima(1), linea]));

            Assert.Empty(resultado.Errores);
            Assert.NotNull(resultado.Guion);
            var dialogo = Assert.Single(Assert.Single(resultado.Guion.Escenas).Clips[0].Dialogos);
            Assert.Equal("ANA", dialogo.Personaje);
            Assert.Equal(acotacion, dialogo.Acotacion);
            Assert.Equal(texto, dialogo.Texto);
            Assert.Equal(linea, dialogo.Original);
        }
    }

    [Fact]
    public void TextoEnPantalla_SeGuardaAparteDeAccionYDialogo()
    {
        var texto = Unir(
        [
            .. EncabezadoYCampos(1),
            "CLIP 1",
            "Ana escribe en su teléfono.",
            "TEXTO EN PANTALLA: \"Hermano, ¿puedes salir conmigo?\"",
            "ANA: \"Listo.\"",
        ]);

        var resultado = GuionParser.Parsear(texto);

        Assert.Empty(resultado.Errores);
        Assert.NotNull(resultado.Guion);
        var clip = Assert.Single(Assert.Single(resultado.Guion.Escenas).Clips);
        Assert.Equal("Hermano, ¿puedes salir conmigo?", clip.TextoEnPantalla);
        Assert.Equal("\"Hermano, ¿puedes salir conmigo?\"", clip.TextoEnPantallaOriginal);
        Assert.Equal(["Ana escribe en su teléfono."], clip.LineasAccion);
        var dialogo = Assert.Single(clip.Dialogos);
        Assert.Equal("Listo.", dialogo.Texto);
    }

    [Fact]
    public void ComillasTipograficas_SeAceptanYConservanOriginal()
    {
        var texto = Unir(
        [
            .. EncabezadoYCampos(1),
            "CLIP 1",
            "  ANA (en voz baja): “Ya llegó.”  ",
            "TEXTO EN PANTALLA: “Tres días después”",
        ]);

        var resultado = GuionParser.Parsear(texto);

        Assert.Empty(resultado.Errores);
        Assert.NotNull(resultado.Guion);
        var clip = Assert.Single(Assert.Single(resultado.Guion.Escenas).Clips);
        var dialogo = Assert.Single(clip.Dialogos);
        Assert.Equal("ANA", dialogo.Personaje);
        Assert.Equal("en voz baja", dialogo.Acotacion);
        Assert.Equal("Ya llegó.", dialogo.Texto);
        Assert.Equal("ANA (en voz baja): “Ya llegó.”", dialogo.Original);
        Assert.Equal("Tres días después", clip.TextoEnPantalla);
        Assert.Equal("“Tres días después”", clip.TextoEnPantallaOriginal);
    }

    [Fact]
    public void LugarConGuion_SeTomaEntrePrimerYUltimoSeparador()
    {
        var lineas = EscenaMinima(1);
        lineas[0] = "ESCENA 1 — EXT. CALLE - SAN JOSÉ — NOCHE";

        var resultado = GuionParser.Parsear(Unir(lineas));

        Assert.Empty(resultado.Errores);
        Assert.NotNull(resultado.Guion);
        var escena = Assert.Single(resultado.Guion.Escenas);
        Assert.Equal(IntExt.Ext, escena.IntExt);
        Assert.Equal("CALLE - SAN JOSÉ", escena.Lugar);
        Assert.Equal("NOCHE", escena.Momento);
    }

    [Fact]
    public void IdsDeDialogo_SonCDeClipGuionDDeOrden()
    {
        var texto = Unir(
        [
            .. EncabezadoYCampos(1),
            "CLIP 1",
            "ANA: \"Hola.\"",
            "Ana sonríe.",
            "JOSÉ DANIEL: \"Hola, Ana.\"",
            "CLIP 2",
            "MESERO: \"¿Algo más?\"",
        ]);

        var resultado = GuionParser.Parsear(texto);

        Assert.Empty(resultado.Errores);
        Assert.NotNull(resultado.Guion);
        var clips = Assert.Single(resultado.Guion.Escenas).Clips;
        Assert.Equal(["c1-d1", "c1-d2"], clips[0].Dialogos.Select(d => d.Codigo));
        Assert.Equal([1, 2], clips[0].Dialogos.Select(d => d.Orden));
        var mesero = Assert.Single(clips[1].Dialogos);
        Assert.Equal("c2-d1", mesero.Codigo);
        Assert.Equal(1, mesero.Orden);
        Assert.Equal("MESERO", mesero.Personaje);
    }

    [Fact]
    public void MismoGuionDosVeces_ResultadosIguales()
    {
        var texto = Generar(3, 6);

        var primero = GuionParser.Parsear(texto);
        var segundo = GuionParser.Parsear(texto);

        Assert.Empty(primero.Errores);
        Assert.NotNull(primero.Guion);
        Assert.Equal(3, primero.Guion.Escenas.Count);
        Assert.Equal(JsonSerializer.Serialize(primero.Guion), JsonSerializer.Serialize(segundo.Guion));
    }

    // ---------- Errores ----------

    [Fact]
    public void FaltaAudioEnEscena1YSinClipEnEscena2_DosErroresEnLosEncabezados()
    {
        var texto = Unir(
            "ESCENA 1 — INT. CASA — DÍA",           // 1
            "LOCACIÓN: Sala.",                      // 2
            "ILUMINACIÓN: Día.",                    // 3
            "PUESTA EN ESCENA: Ana de pie.",        // 4
            "CLIP 1",                               // 5
            "Ana entra.",                           // 6
            "ESCENA 2 — INT. CASA — NOCHE",         // 7
            "LOCACIÓN: Sala.",                      // 8
            "ILUMINACIÓN: Noche.",                  // 9
            "PUESTA EN ESCENA: Ana sentada.",       // 10
            "AUDIO: Silencio.");                    // 11

        var resultado = GuionParser.Parsear(texto);

        Assert.Null(resultado.Guion);
        Assert.Equal(
            [new ErrorGuion(1, "Falta el campo AUDIO"), new ErrorGuion(7, "La escena no tiene ningún CLIP")],
            resultado.Errores);
    }

    [Fact]
    public void GuionVacio_ErrorEnLinea1()
    {
        foreach (var texto in new[] { "", null, "\n\n   \n", "\uFEFF" })
        {
            var resultado = GuionParser.Parsear(texto);

            Assert.Null(resultado.Guion);
            Assert.Equal([new ErrorGuion(1, MotivoSinEscena)], resultado.Errores);
        }
    }

    [Fact]
    public void GuionSinEscena_ErrorEnLinea1()
    {
        var texto = Unir("", "Ana entra al restaurante.", "ANA: Mentiroso.", "CLIP 1", "AUDIO:");

        var resultado = GuionParser.Parsear(texto);

        Assert.Null(resultado.Guion);
        Assert.Equal([new ErrorGuion(1, MotivoSinEscena)], resultado.Errores);
    }

    [Fact]
    public void TextoAntesDeLaPrimeraEscena_ErrorEnEsaLinea()
    {
        var texto = Unir(["", "Notas del guionista", "CLIP 1", .. EscenaMinima(1)]);

        var resultado = GuionParser.Parsear(texto);

        Assert.Null(resultado.Guion);
        Assert.Equal([new ErrorGuion(2, "Texto antes de la primera escena")], resultado.Errores);
    }

    [Fact]
    public void EncabezadoIE_ErrorEnElEncabezado()
    {
        var lineas = EscenaMinima(1);
        lineas[0] = "ESCENA 1 — I/E RESTAURANTE — NOCHE";

        var resultado = GuionParser.Parsear(Unir(lineas));

        Assert.Null(resultado.Guion);
        Assert.Equal([new ErrorGuion(1, MotivoEncabezadoInvalido)], resultado.Errores);
    }

    [Fact]
    public void EncabezadoIntSinPuntoNoPrimero_ErrorEnElEncabezado()
    {
        string[] invalidos =
        [
            "ESCENA 2 — INT RESTAURANTE — NOCHE",   // sin punto
            "ESCENA 2 — INT.RESTAURANTE — NOCHE",   // sin espacio tras el punto
            "ESCENA 2 – INT. RESTAURANTE – NOCHE",  // guion medio: no es separador
            "ESCENA 2 — INT. RESTAURANTE NOCHE",    // sin segundo separador
            "ESCENA 2 — INT. — NOCHE",              // LUGAR vacío
            "ESCENA 2 — INT. RESTAURANTE —",        // MOMENTO vacío
            "Escena 2 — INT. RESTAURANTE — NOCHE",  // ESCENA en minúsculas
            "ESCENA 2 — int. RESTAURANTE — NOCHE",  // int. en minúsculas
        ];

        foreach (var encabezado in invalidos)
        {
            var escena2 = EscenaMinima(2);
            escena2[0] = encabezado;
            var texto = Unir([.. EscenaMinima(1), .. escena2, .. EscenaMinima(3)]);

            var resultado = GuionParser.Parsear(texto);

            Assert.Null(resultado.Guion);
            Assert.Equal([new ErrorGuion(8, MotivoEncabezadoInvalido)], resultado.Errores);
        }
    }

    [Fact]
    public void Escena1SeguidaDeEscena3_UnSoloErrorEnEscena3()
    {
        var texto = Unir([.. EscenaMinima(1), .. EscenaMinima(3), .. EscenaMinima(4)]);

        var resultado = GuionParser.Parsear(texto);

        Assert.Null(resultado.Guion);
        Assert.Equal([new ErrorGuion(8, "Se esperaba ESCENA 2")], resultado.Errores);

        var repetida = GuionParser.Parsear(Unir([.. EscenaMinima(1), .. EscenaMinima(1)]));
        Assert.Equal([new ErrorGuion(8, "Se esperaba ESCENA 2")], repetida.Errores);

        var noEmpiezaEn1 = GuionParser.Parsear(Unir(EscenaMinima(2)));
        Assert.Equal([new ErrorGuion(1, "Se esperaba ESCENA 1")], noEmpiezaEn1.Errores);
    }

    [Fact]
    public void DosClip1_ErrorEnElSegundo()
    {
        var texto = Unir([.. EscenaMinima(1), "CLIP 1", "Ana se sienta.", "CLIP 2", "Ana bebe."]);

        var resultado = GuionParser.Parsear(texto);

        Assert.Null(resultado.Guion);
        Assert.Equal([new ErrorGuion(8, "Se esperaba CLIP 2")], resultado.Errores);
    }

    [Fact]
    public void ClipMalEscrito_ErrorEnEsaLineaYNoEsAccion()
    {
        foreach (var marcador in new[] { "CLIP1", "Clip 2", "CLIP 01", "CLIP 2 bis", "clip" })
        {
            var texto = Unir([.. EscenaMinima(1), marcador, "Ana se sienta.", "CLIP 3", "Ana bebe."]);

            var resultado = GuionParser.Parsear(texto);

            Assert.Null(resultado.Guion);
            Assert.Equal([new ErrorGuion(8, MotivoClipInvalido)], resultado.Errores);
        }
    }

    [Fact]
    public void OnceEscenas_ErrorEnElEncabezadoDeLaEscena11()
    {
        var texto = Generar(11, 1);

        var resultado = GuionParser.Parsear(texto);

        Assert.Null(resultado.Guion);
        Assert.Equal(
            [new ErrorGuion(LineaDe(texto, "ESCENA 11 "), "Se superó el límite de 10 escenas")],
            resultado.Errores);

        var doce = Generar(12, 1);
        Assert.Equal(
            [new ErrorGuion(LineaDe(doce, "ESCENA 11 "), "Se superó el límite de 10 escenas")],
            GuionParser.Parsear(doce).Errores);
    }

    [Fact]
    public void VeintiunClips_ErrorEnElClip21()
    {
        var texto = Generar(1, 21);

        var resultado = GuionParser.Parsear(texto);

        Assert.Null(resultado.Guion);
        Assert.Equal(
            [new ErrorGuion(LineaDe(texto, "CLIP 21"), "Se superó el límite de 20 clips por escena")],
            resultado.Errores);
    }

    [Fact]
    public void LocacionRepetida_ErrorEnLaSegunda()
    {
        var texto = Unir([.. EncabezadoYCampos(1)[..2], "LOCACIÓN: Otra sala.", .. EscenaMinima(1)[2..]]);

        var resultado = GuionParser.Parsear(texto);

        Assert.Null(resultado.Guion);
        Assert.Equal([new ErrorGuion(3, "Campo repetido: LOCACIÓN")], resultado.Errores);
    }

    [Fact]
    public void AudioSinTexto_ErrorEnEsaLinea()
    {
        var lineas = EscenaMinima(1);
        lineas[4] = "AUDIO:   ";

        var resultado = GuionParser.Parsear(Unir(lineas));

        Assert.Null(resultado.Guion);
        Assert.Equal([new ErrorGuion(5, "Campo sin valor: AUDIO")], resultado.Errores);
    }

    [Fact]
    public void IluminacionAntesDeLocacion_UnSoloErrorEnIluminacion()
    {
        var lineas = EscenaMinima(1);
        (lineas[1], lineas[2]) = (lineas[2], lineas[1]);

        var resultado = GuionParser.Parsear(Unir(lineas));

        Assert.Null(resultado.Guion);
        Assert.Equal(
            [new ErrorGuion(2, "Campo fuera de orden: va después de LOCACIÓN")],
            resultado.Errores);
    }

    [Fact]
    public void AudioDespuesDeClip1_ErrorEnEseAudio()
    {
        var texto = Unir(
        [
            .. EncabezadoYCampos(1)[..4],   // 1-4, sin AUDIO
            "CLIP 1",                       // 5
            "Ana entra.",                   // 6
            "AUDIO: Silencio.",             // 7
        ]);

        var resultado = GuionParser.Parsear(texto);

        Assert.Null(resultado.Guion);
        Assert.Equal(
            [
                new ErrorGuion(1, "Falta el campo AUDIO"),
                new ErrorGuion(7, "El campo AUDIO va antes del primer CLIP"),
            ],
            resultado.Errores);
    }

    [Fact]
    public void TextoEntreAudioYClip1_ErrorEnEsaLinea()
    {
        foreach (var intrusa in new[] { "Ana entra.", "ANA: \"Hola.\"", "TEXTO EN PANTALLA: \"Hola\"" })
        {
            var texto = Unir([.. EncabezadoYCampos(1), intrusa, "CLIP 1", "Ana se sienta."]);

            var resultado = GuionParser.Parsear(texto);

            Assert.Null(resultado.Guion);
            Assert.Equal([new ErrorGuion(6, MotivoFueraDeLugar)], resultado.Errores);
        }
    }

    [Fact]
    public void Clip2SinContenido_ErrorEnClip2()
    {
        var texto = Unir([.. EscenaMinima(1), "CLIP 2", "", "   ", "CLIP 3", "Ana bebe.", "CLIP 4", ""]);

        var resultado = GuionParser.Parsear(texto);

        Assert.Null(resultado.Guion);
        Assert.Equal(
            [new ErrorGuion(8, "El clip no tiene contenido"), new ErrorGuion(13, "El clip no tiene contenido")],
            resultado.Errores);
    }

    [Fact]
    public void ClipSoloConTextoEnPantalla_EsValido()
    {
        var texto = Unir([.. EscenaMinima(1), "CLIP 2", "TEXTO EN PANTALLA: \"FIN\""]);

        var resultado = GuionParser.Parsear(texto);

        Assert.Empty(resultado.Errores);
        Assert.NotNull(resultado.Guion);
        var clip2 = Assert.Single(resultado.Guion.Escenas).Clips[1];
        Assert.Empty(clip2.LineasAccion);
        Assert.Empty(clip2.Dialogos);
        Assert.Equal("FIN", clip2.TextoEnPantalla);
    }

    [Fact]
    public void DialogoSinComillas_ErrorEnEsaLinea()
    {
        foreach (var linea in new[]
        {
            "ANA: Mentiroso.",
            "ANA: \"Mentiroso.",
            "ANA: “Mentiroso.\"",
            "ANA:",
            "JOSÉ DANIEL (sonriendo): Bueno...",
            "ANA (sonriendo):",
            // Paréntesis cerrado pero sin «:»: la regex del spec no lo acepta → forma inválida.
            "ANA (x) \"hola\"",
        })
        {
            var texto = Unir([.. EscenaMinima(1), linea]);

            var resultado = GuionParser.Parsear(texto);

            Assert.Null(resultado.Guion);
            Assert.Equal([new ErrorGuion(8, "El diálogo debe ir entre comillas")], resultado.Errores);
        }
    }

    [Fact]
    public void TextoDespuesDeLaComilla_ErrorEnEsaLinea()
    {
        foreach (var linea in new[] { "ANA: \"Hola\" y se va.", "ANA: “Hola” y se va." })
        {
            var texto = Unir([.. EscenaMinima(1), linea]);

            var resultado = GuionParser.Parsear(texto);

            Assert.Null(resultado.Guion);
            Assert.Equal(
                [new ErrorGuion(8, "Hay texto después de la comilla de cierre")],
                resultado.Errores);
        }
    }

    [Fact]
    public void ComillasVacias_ErrorEnEsaLinea()
    {
        foreach (var linea in new[] { "ANA: \"\"", "ANA: \"   \"", "ANA (seria): “”" })
        {
            var texto = Unir([.. EscenaMinima(1), linea]);

            var resultado = GuionParser.Parsear(texto);

            Assert.Null(resultado.Guion);
            Assert.Equal([new ErrorGuion(8, "Las comillas están vacías")], resultado.Errores);
        }
    }

    [Fact]
    public void AcotacionVaciaOAbierta_ErrorEnEsaLinea()
    {
        (string Linea, string Motivo)[] casos =
        [
            ("ANA (): \"x\"", "La acotación está vacía"),
            ("ANA (   ): \"x\"", "La acotación está vacía"),
            ("ANA (sonriendo: \"x\"", "Falta cerrar el paréntesis de la acotación"),
            ("ANA (sonriendo: \"x)\"", "Falta cerrar el paréntesis de la acotación"),
        ];

        foreach (var (linea, motivo) in casos)
        {
            var texto = Unir([.. EscenaMinima(1), linea]);

            var resultado = GuionParser.Parsear(texto);

            Assert.Null(resultado.Guion);
            Assert.Equal([new ErrorGuion(8, motivo)], resultado.Errores);
        }
    }

    [Fact]
    public void TextoEnPantallaSinComillas_ErrorEnEsaLinea()
    {
        foreach (var linea in new[]
        {
            "TEXTO EN PANTALLA: Hola",
            "TEXTO EN PANTALLA:",
            "TEXTO EN PANTALLA \"Hola\"",
            "Texto en pantalla: \"Hola\"",
            "TEXTO EN PANTALLA: \"Hola\" y más",
        })
        {
            var texto = Unir([.. EscenaMinima(1), linea]);

            var resultado = GuionParser.Parsear(texto);

            Assert.Null(resultado.Guion);
            Assert.Equal(
                [new ErrorGuion(8, "El texto en pantalla debe ir entre comillas")],
                resultado.Errores);
        }
    }

    [Fact]
    public void DosTextosEnPantalla_ErrorEnElSegundo()
    {
        var texto = Unir([.. EscenaMinima(1), "TEXTO EN PANTALLA: \"Uno\"", "TEXTO EN PANTALLA: \"Dos\""]);

        var resultado = GuionParser.Parsear(texto);

        Assert.Null(resultado.Guion);
        Assert.Equal([new ErrorGuion(9, "Más de un texto en pantalla en el clip")], resultado.Errores);
    }

    [Fact]
    public void GuionInvalido_NoDevuelveGuionParseado()
    {
        // Errores emitidos fuera de orden (el de "falta campo" se detecta al cerrar la escena):
        // se devuelven todos, ordenados por línea, y no hay guion parseado.
        var texto = Unir(
        [
            .. EncabezadoYCampos(1)[..4],   // 1-4, sin AUDIO
            "CLIP 1",                       // 5
            "ANA: Hola.",                   // 6
            "CLIP 3",                       // 7
            "Ana sale.",                    // 8
        ]);

        var invalido = GuionParser.Parsear(texto);

        Assert.False(invalido.EsValido);
        Assert.Null(invalido.Guion);
        Assert.Equal([1, 6, 7], invalido.Errores.Select(e => e.Linea));

        var valido = GuionParser.Parsear(EscenaDeEjemplo);
        Assert.True(valido.EsValido);
        Assert.NotNull(valido.Guion);
        Assert.NotEmpty(valido.Guion.Escenas);
    }

    [Fact]
    public void LineaEnNFD_ErrorEnEsaLinea()
    {
        const string motivo = "Texto con tildes en formato no estándar (NFD); vuelve a pegarlo desde otro editor";
        string[] lineas =
        [
            .. EncabezadoYCampos(1),                    // 1-5
            "CLIP 1",                                   // 6
            "José Daniel mira por la ventana.",         // 7: acción
            "JOSÉ DANIEL: \"Hola.\"",                   // 8: marcador de diálogo
            "CLIP 2",                                   // 9
            "Ana saluda a José Daniel.",                // 10: única línea del clip
        ];
        var enNfd = (string[])lineas.Clone();
        foreach (var i in new[] { 6, 7, 9 })
        {
            enNfd[i] = lineas[i].Normalize(NormalizationForm.FormD);
            Assert.NotEqual(lineas[i], enNfd[i]);
        }

        var nfc = GuionParser.Parsear(Unir(lineas));
        var nfd = GuionParser.Parsear(Unir(enNfd));

        Assert.Empty(nfc.Errores);
        Assert.NotNull(nfc.Guion);
        Assert.Null(nfd.Guion);
        // Un error por línea en NFD y ninguno en cascada (ni «clip sin contenido» en el CLIP 2).
        Assert.Equal(
            [new ErrorGuion(7, motivo), new ErrorGuion(8, motivo), new ErrorGuion(10, motivo)],
            nfd.Errores);
    }

    private const string MotivoNoNfc =
        "Texto con tildes en formato no estándar (NFD); vuelve a pegarlo desde otro editor";

    /// <summary>Une las líneas pasando a NFD las de los índices (desde 0) indicados.</summary>
    private static string UnirConNfd(string[] lineas, params int[] indicesNfd)
    {
        var copia = (string[])lineas.Clone();
        foreach (var i in indicesNfd)
        {
            copia[i] = lineas[i].Normalize(NormalizationForm.FormD);
            Assert.NotEqual(lineas[i], copia[i]);
        }

        return Unir(copia);
    }

    [Fact]
    public void GuionEnteroEnNFD_UnErrorNfdPorLineaConTilde()
    {
        var lineas = EscenaDeEjemplo.Split('\n');
        var esperadas = lineas
            .Select((linea, i) => (Linea: i + 1, Texto: linea.Normalize(NormalizationForm.FormD)))
            .Where(x => x.Texto != x.Texto.Normalize(NormalizationForm.FormC))
            .Select(x => new ErrorGuion(x.Linea, MotivoNoNfc))
            .ToList();
        Assert.True(esperadas.Count > 3);

        var resultado = GuionParser.Parsear(EscenaDeEjemplo.Normalize(NormalizationForm.FormD));

        Assert.Null(resultado.Guion);
        Assert.Equal(esperadas, resultado.Errores);
    }

    [Fact]
    public void EncabezadoEscena1EnNFD_SoloErrorNfd()
    {
        var resultado = GuionParser.Parsear(UnirConNfd(EscenaMinima(1), 0));

        Assert.Null(resultado.Guion);
        Assert.Equal([new ErrorGuion(1, MotivoNoNfc)], resultado.Errores);
    }

    [Fact]
    public void EncabezadoEscenaNEnNFD_SoloErrorNfd()
    {
        var resultado = GuionParser.Parsear(UnirConNfd([.. EscenaMinima(1), .. EscenaMinima(2)], 7));

        Assert.Null(resultado.Guion);
        Assert.Equal([new ErrorGuion(8, MotivoNoNfc)], resultado.Errores);
    }

    [Fact]
    public void EncabezadoEnNFD_ConservaLosErroresDeLaEscenaAnterior()
    {
        // Al abrir la escena 2 se cierra la 1, que no tiene AUDIO: ese error es de la línea 1 y
        // no se descarta junto con los de la línea en NFD.
        string[] lineas = [.. EscenaMinima(1)[..4], .. EscenaMinima(1)[5..], .. EscenaMinima(2)];

        var resultado = GuionParser.Parsear(UnirConNfd(lineas, 6));

        Assert.Null(resultado.Guion);
        Assert.Equal(
            [new ErrorGuion(1, "Falta el campo AUDIO"), new ErrorGuion(7, MotivoNoNfc)],
            resultado.Errores);
    }

    [Fact]
    public void CampoLocacionEnNFD_SoloErrorNfd()
    {
        var resultado = GuionParser.Parsear(UnirConNfd(EscenaMinima(1), 1));

        Assert.Null(resultado.Guion);
        Assert.Equal([new ErrorGuion(2, MotivoNoNfc)], resultado.Errores);
    }

    // ---------- Rendimiento ----------

    [Fact]
    public void Rendimiento_GuionDe10x20_P95MenorA1Segundo()
    {
        var texto = Generar(10, 20);
        var calentamiento = GuionParser.Parsear(texto);
        Assert.Empty(calentamiento.Errores);
        Assert.NotNull(calentamiento.Guion);
        Assert.Equal(10, calentamiento.Guion.Escenas.Count);
        Assert.All(calentamiento.Guion.Escenas, e => Assert.Equal(20, e.Clips.Count));

        var tiempos = new List<TimeSpan>(100);
        for (var i = 0; i < 100; i++)
        {
            var reloj = Stopwatch.StartNew();
            GuionParser.Parsear(texto);
            tiempos.Add(reloj.Elapsed);
        }

        tiempos.Sort();
        var p95 = tiempos[94];
        Assert.True(p95 < TimeSpan.FromSeconds(1), $"p95 = {p95.TotalMilliseconds} ms");
    }

    [Fact]
    public void Rendimiento_LineaPatologica200000_MenorA1Segundo()
    {
        const int Largo = 200_000;
        string[] patologicas =
        [
            // Encabezado con miles de " - ": LUGAR/MOMENTO no deben partirse con backtracking.
            Rellenar("ESCENA 1 - INT. ", "A - ", Largo - 8) + " - NOCHE",
            // Encabezado inválido (sin INT./EXT.) con miles de separadores.
            Rellenar("ESCENA 1 - ", "- ", Largo - 2) + "x",
            // Encabezado con separadores sin espacios alrededor.
            Rellenar("ESCENA 1 — EXT. ", "—-", Largo - 2) + " x",
            // Marcador en mayúsculas con espacios: [A-Z ]* seguido de \s* no debe ser cuadrático.
            Rellenar("A", " ", Largo - 1) + "x",
            // Acotación con miles de paréntesis.
            Rellenar("ANA (", ") ", Largo - 1) + "x",
        ];

        GuionParser.Parsear(Unir(EscenaMinima(1)));

        foreach (var linea in patologicas)
        {
            Assert.True(linea.Length >= Largo - 10, $"largo = {linea.Length}");
            var texto = linea.StartsWith("ESCENA", StringComparison.Ordinal)
                ? Unir([linea, .. EscenaMinima(1)[1..]])
                : Unir([.. EscenaMinima(1), linea]);

            var reloj = Stopwatch.StartNew();
            GuionParser.Parsear(texto);
            reloj.Stop();

            Assert.True(
                reloj.Elapsed < TimeSpan.FromSeconds(1),
                $"{linea[..20]}…: {reloj.Elapsed.TotalMilliseconds} ms");
        }

        var invalido = GuionParser.Parsear(Unir([patologicas[1], .. EscenaMinima(1)[1..]]));
        Assert.Equal([new ErrorGuion(1, MotivoEncabezadoInvalido)], invalido.Errores);
    }

    // ---------- Auxiliares ----------

    private static string Rellenar(string prefijo, string patron, int largo)
    {
        var sb = new StringBuilder(prefijo, largo + patron.Length);
        while (sb.Length < largo)
        {
            sb.Append(patron);
        }

        return sb.ToString(0, largo);
    }

    /// <summary>Todos los campos del guion excepto los números de línea.</summary>
    private static string DescribirSinLineas(GuionParseado guion) =>
        JsonSerializer.Serialize(guion.Escenas.Select(e => new
        {
            e.Numero,
            e.IntExt,
            e.Lugar,
            e.Momento,
            e.Locacion,
            e.Iluminacion,
            e.PuestaEnEscena,
            e.Audio,
            Clips = e.Clips.Select(c => new
            {
                c.Numero,
                c.LineasAccion,
                c.Dialogos,
                c.TextoEnPantalla,
                c.TextoEnPantallaOriginal,
            }),
        }));
}
