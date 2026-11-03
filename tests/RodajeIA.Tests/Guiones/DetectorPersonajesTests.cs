using System.Text;
using RodajeIA.Web.Guiones;
using static RodajeIA.Tests.Guiones.GuionesDePrueba;

namespace RodajeIA.Tests.Guiones;

public class DetectorPersonajesTests
{
    private static readonly string[] HojasAnaYJoseDaniel = ["Ana", "José Daniel"];

    /// <summary>
    /// Escena con los cuatro campos (la puesta en escena se puede cambiar) seguida de los clips
    /// dados; cada clip es la lista de sus líneas sin el marcador <c>CLIP N</c>.
    /// </summary>
    private static GuionParseado Guion(string puestaEnEscena, params string[][] clips)
    {
        var lineas = new List<string>
        {
            "ESCENA 1 — INT. CASA — DÍA",
            "LOCACIÓN: Sala con un sofá gris.",
            "ILUMINACIÓN: Luz de día por la ventana.",
            "PUESTA EN ESCENA: " + puestaEnEscena,
            "AUDIO: Silencio, tráfico lejano.",
        };
        for (var i = 0; i < clips.Length; i++)
        {
            lineas.Add($"CLIP {i + 1}");
            lineas.AddRange(clips[i]);
        }

        var resultado = GuionParser.Parsear(Unir([.. lineas]));
        Assert.Empty(resultado.Errores);
        Assert.NotNull(resultado.Guion);
        return resultado.Guion;
    }

    private const string PuestaNeutra = "Una mesa en el centro del encuadre.";

    [Fact]
    public void AnaEnLaAccion_PresenteEnClipYEscena()
    {
        var guion = Guion(PuestaNeutra, ["Ana mira por la ventana."], ["La lluvia golpea el vidrio."]);

        var detectados = DetectorPersonajes.Detectar(guion, HojasAnaYJoseDaniel);

        Assert.Equal(["Ana"], detectados.EnClip(1, 1));
        Assert.Empty(detectados.EnClip(1, 2));
        Assert.Equal(["Ana"], detectados.EnEscena(1));
    }

    [Fact]
    public void DialogoAna_PresenteEnClipYEscena()
    {
        var guion = Guion(PuestaNeutra, ["La lluvia golpea el vidrio."], ["ANA: \"Claro.\""]);

        var detectados = DetectorPersonajes.Detectar(guion, HojasAnaYJoseDaniel);

        Assert.Empty(detectados.EnClip(1, 1));
        Assert.Equal(["Ana"], detectados.EnClip(1, 2));
        Assert.Equal(["Ana"], detectados.EnEscena(1));
    }

    [Fact]
    public void DialogoConAcotacion_JoseDaniel_PresenteEnClip()
    {
        // La escena de ejemplo del Anexo A: «JOSÉ DANIEL (sonriendo): …» y ambos en la acción.
        var guion = GuionParser.Parsear(EscenaDeEjemplo).Guion!;

        var detectados = DetectorPersonajes.Detectar(guion, HojasAnaYJoseDaniel);

        Assert.Equal(["Ana", "José Daniel"], detectados.EnClip(1, 1));
        Assert.Equal(["Ana", "José Daniel"], detectados.EnClip(1, 2));
        Assert.Equal(["Ana", "José Daniel"], detectados.EnEscena(1));
    }

    [Fact]
    public void Anabel_NoDetectaAna()
    {
        var guion = Guion(PuestaNeutra, ["Anabel entra a la sala."], ["Entra Ana, empapada."]);

        var detectados = DetectorPersonajes.Detectar(guion, HojasAnaYJoseDaniel);

        Assert.Empty(detectados.EnClip(1, 1));
        Assert.Equal(["Ana"], detectados.EnClip(1, 2));
    }

    [Fact]
    public void JoseDanielSinTilde_NoDetectaJoseDaniel()
    {
        var guion = Guion(
            PuestaNeutra,
            ["Jose Daniel se sienta.", "JOSE DANIEL espera."],
            ["José Daniel se levanta."]);

        var detectados = DetectorPersonajes.Detectar(guion, HojasAnaYJoseDaniel);

        Assert.Empty(detectados.EnClip(1, 1));
        Assert.Equal(["José Daniel"], detectados.EnClip(1, 2));
    }

    [Fact]
    public void MeseroSinHoja_NoSeDetecta()
    {
        var guion = Guion(PuestaNeutra, ["Ana espera la cuenta.", "MESERO: \"¿Algo más?\""]);

        var detectados = DetectorPersonajes.Detectar(guion, HojasAnaYJoseDaniel);

        var clip = Assert.Single(guion.Escenas[0].Clips);
        Assert.Contains(clip.Dialogos, d => d.Personaje == "MESERO");
        Assert.Equal(["Ana"], detectados.EnClip(1, 1));
        Assert.Equal(["Ana"], detectados.EnEscena(1));
    }

    [Fact]
    public void AnaSoloEnPuestaEnEscena_PresenteEnEscenaYEnNingunClip()
    {
        var guion = Guion(
            "Ana a la izquierda del encuadre.",
            ["La lluvia golpea el vidrio."],
            ["Un perro ladra a lo lejos."]);

        var detectados = DetectorPersonajes.Detectar(guion, HojasAnaYJoseDaniel);

        Assert.Equal(["Ana"], detectados.EnEscena(1));
        Assert.Empty(detectados.EnClip(1, 1));
        Assert.Empty(detectados.EnClip(1, 2));
    }

    [Fact]
    public void NombreDentroDelTextoDeDialogo_NoSeDetecta()
    {
        var guion = Guion(
            PuestaNeutra,
            [
                "ANA: \"Saludos a José Daniel\"",
                "ANA (por José Daniel): \"Ya viene.\"",
                "TEXTO EN PANTALLA: \"José Daniel, 1998\"",
            ]);

        var detectados = DetectorPersonajes.Detectar(guion, HojasAnaYJoseDaniel);

        Assert.Equal(["Ana"], detectados.EnClip(1, 1));
        Assert.Equal(["Ana"], detectados.EnEscena(1));
    }

    [Fact]
    public void JoseDanielEnFormaNFD_SeDetecta()
    {
        // El guion está en NFC (el parser rechaza líneas en NFD); la hoja viene en NFD.
        var hojaNfd = "José Daniel".Normalize(NormalizationForm.FormD);
        Assert.NotEqual("José Daniel", hojaNfd);
        var guion = Guion(
            "José Daniel a la derecha.",
            ["José Daniel se sienta."],
            ["JOSÉ DANIEL: \"Hola.\""],
            ["La lluvia golpea el vidrio."]);

        var detectados = DetectorPersonajes.Detectar(guion, [hojaNfd]);

        Assert.Equal([hojaNfd], detectados.EnClip(1, 1));
        Assert.Equal([hojaNfd], detectados.EnClip(1, 2));
        Assert.Empty(detectados.EnClip(1, 3));
        Assert.Equal([hojaNfd], detectados.EnEscena(1));
    }

    [Fact]
    public void MarcadorEnNFDArmadoAMano_SeDetecta()
    {
        // El parser ya no produce marcadores en NFD; el contrato del detector igual compara en NFC.
        var parseado = Guion(PuestaNeutra, ["JOSÉ DANIEL: \"Hola.\""]);
        var escena = parseado.Escenas[0];
        var clip = escena.Clips[0];
        var marcadorNfd = "JOSÉ DANIEL".Normalize(NormalizationForm.FormD);
        Assert.NotEqual("JOSÉ DANIEL", marcadorNfd);
        var guion = new GuionParseado(
        [
            escena with { Clips = [clip with { Dialogos = [clip.Dialogos[0] with { Personaje = marcadorNfd }] }] },
        ]);

        var detectados = DetectorPersonajes.Detectar(guion, HojasAnaYJoseDaniel);

        Assert.Equal(["José Daniel"], detectados.EnClip(1, 1));
    }

    [Fact]
    public void AccionYPuestaEnNFDArmadasAMano_SeDetectan()
    {
        // El parser ya no deja pasar texto en NFD; el detector igual compara en NFC. Escena 1:
        // solo la acción del clip nombra a José Daniel; escena 2: solo su puesta en escena.
        var parseado = GuionParser.Parsear(Unir([.. EscenaMinima(1), .. EscenaMinima(2)])).Guion!;
        var nfd = "José Daniel".Normalize(NormalizationForm.FormD);
        Assert.NotEqual("José Daniel", nfd);
        var escena1 = parseado.Escenas[0];
        var escena2 = parseado.Escenas[1];
        var guion = new GuionParseado(
        [
            escena1 with { Clips = [escena1.Clips[0] with { LineasAccion = [nfd + " se sienta."] }] },
            escena2 with { PuestaEnEscena = nfd + " a la derecha." },
        ]);

        var detectados = DetectorPersonajes.Detectar(guion, ["José Daniel"]);

        Assert.Equal(["José Daniel"], detectados.EnClip(1, 1));
        Assert.Equal(["José Daniel"], detectados.EnEscena(1));
        Assert.Empty(detectados.EnClip(2, 1));
        Assert.Equal(["José Daniel"], detectados.EnEscena(2));
    }

    [Fact]
    public void NombrePegadoAMarcaCombinante_NoSeDetecta()
    {
        // U+0359 no tiene forma precompuesta: el texto sigue en NFC y la marca queda suelta.
        var marca = char.ConvertFromUtf32(0x0359);
        var guion = Guion(
            PuestaNeutra,
            ["Entra Ana" + marca + " con un paraguas."],
            ["Entra Lu" + marca + "Ana sola."],
            ["Entra Ana."]);

        var detectados = DetectorPersonajes.Detectar(guion, HojasAnaYJoseDaniel);

        Assert.Empty(detectados.EnClip(1, 1));
        Assert.Empty(detectados.EnClip(1, 2));
        Assert.Equal(["Ana"], detectados.EnClip(1, 3));
    }

    [Fact]
    public void HojaRepetidaEnNFCyNFD_CuentaUnaVez()
    {
        var nfd = "José Daniel".Normalize(NormalizationForm.FormD);
        var guion = Guion("José Daniel a la derecha.", ["José Daniel se sienta."]);

        var detectados = DetectorPersonajes.Detectar(guion, ["José Daniel", nfd, "Ana"]);

        Assert.Equal(["José Daniel"], detectados.EnClip(1, 1));
        Assert.Equal(["José Daniel"], detectados.EnEscena(1));
    }

    [Fact]
    public void PersonajesDetectados_CopiaLasColecciones()
    {
        var lista = new List<string> { "Ana" };
        var porClip = new Dictionary<(int Escena, int Clip), IReadOnlyList<string>> { [(1, 1)] = lista };
        var porEscena = new Dictionary<int, IReadOnlyList<string>> { [1] = lista };

        var detectados = new PersonajesDetectados(porClip, porEscena);
        lista.Add("José Daniel");
        porClip[(1, 2)] = lista;
        porEscena[2] = lista;

        Assert.Equal(["Ana"], detectados.EnClip(1, 1));
        Assert.Equal(["Ana"], detectados.EnEscena(1));
        Assert.Single(detectados.PorClip);
        Assert.Single(detectados.PorEscena);
        Assert.False(detectados.EnClip(1, 1) is IList<string> { IsReadOnly: false });
    }

    [Fact]
    public void PersonajesDetectados_ArgumentoNull_Lanza()
    {
        var porClip = new Dictionary<(int Escena, int Clip), IReadOnlyList<string>> { [(1, 1)] = null! };
        var porEscena = new Dictionary<int, IReadOnlyList<string>> { [1] = ["Ana"] };
        var escenaConNull = new Dictionary<int, IReadOnlyList<string>> { [1] = null! };
        var clipsVacios = new Dictionary<(int Escena, int Clip), IReadOnlyList<string>>();

        Assert.Throws<ArgumentNullException>(() => new PersonajesDetectados(null!, porEscena));
        Assert.Throws<ArgumentNullException>(() => new PersonajesDetectados(clipsVacios, null!));
        Assert.Throws<ArgumentNullException>(() => new PersonajesDetectados(porClip, porEscena));
        Assert.Throws<ArgumentNullException>(() => new PersonajesDetectados(clipsVacios, escenaConNull));
    }

    [Fact]
    public void SinHojas_ResultadoVacio()
    {
        var guion = GuionParser.Parsear(Generar(2, 3)).Guion!;

        var detectados = DetectorPersonajes.Detectar(guion, []);

        // Cada escena y cada clip del guion figuran en el resultado, todos sin personajes.
        Assert.Equal([1, 2], detectados.PorEscena.Keys.Order());
        Assert.Equal(6, detectados.PorClip.Count);
        Assert.All(detectados.PorEscena.Values, Assert.Empty);
        Assert.All(detectados.PorClip.Values, Assert.Empty);
    }

    [Fact]
    public void NombreConCaracteresDeRegex_NoLanza()
    {
        var guion = Guion(
            PuestaNeutra,
            ["Entra Dr. (Ana) con un maletín.", "Abo y A.b discuten."],
            ["Entra el Drx (Ana) sin maletín.", "Aab se va."]);
        string[] hojas = ["Dr. (Ana)", "A.b", "[", "a+*?"];

        var detectados = DetectorPersonajes.Detectar(guion, hojas);

        // «.» se escapa: «A.b» no coincide con «Aab» ni «Abo»; y «Dr. (Ana)» no coincide con «Drx (Ana)».
        Assert.Equal(["Dr. (Ana)", "A.b"], detectados.EnClip(1, 1));
        Assert.Empty(detectados.EnClip(1, 2));
        Assert.Equal(["Dr. (Ana)", "A.b"], detectados.EnEscena(1));
    }
}
