using System.Text;
using System.Text.RegularExpressions;

namespace RodajeIA.Web.Guiones;

/// <summary>
/// Parser determinístico del guion (Anexo A del PRD base), sin IA y de una sola pasada.
/// Valida el guion acumulando todos los errores con su línea y motivo; solo devuelve un
/// <see cref="GuionParseado"/> si no hay ninguno. Nunca corrige ni normaliza el texto.
/// </summary>
/// <remarks>
/// Cada línea no vacía se clasifica con detección permisiva (lo que parece un marcador se trata
/// como marcador) y luego se valida su forma de manera estricta. Las regex son estáticas, sin
/// cuantificadores anidados y con <see cref="RegexOptions.NonBacktracking"/> (tiempo lineal);
/// LUGAR y MOMENTO del encabezado se separan con una búsqueda lineal del último separador (M-06).
/// </remarks>
public static class GuionParser
{
    /// <summary>Máximo de escenas por guion.</summary>
    public const int MaximoEscenas = 10;

    /// <summary>Máximo de clips por escena.</summary>
    public const int MaximoClipsPorEscena = 20;

    private static readonly string[] NombresCampos = ["LOCACIÓN", "ILUMINACIÓN", "PUESTA EN ESCENA", "AUDIO"];

    private const RegexOptions Opciones = RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;

    private static readonly Regex PrefijoEncabezado =
        new(@"^ESCENA ([1-9][0-9]*)\s+[—-]\s+(INT\.|EXT\.)\s+", Opciones);

    private static readonly Regex MarcadorClip = new(@"^CLIP ([1-9][0-9]*)$", Opciones);

    private static readonly Regex TextoEnPantalla = new(@"^TEXTO EN PANTALLA:\s*(.+)$", Opciones);

    private static readonly Regex CampoEscena =
        new(@"^(LOCACIÓN|ILUMINACIÓN|PUESTA EN ESCENA|AUDIO):", Opciones);

    private static readonly Regex MarcadorDialogo =
        new("^" + NombrePersonaje.FragmentoMarcador + @"\s*[(:]", Opciones);

    internal const string MotivoSinEscena = "El guion no tiene ninguna escena (ESCENA N — …)";
    internal const string MotivoTextoAntes = "Texto antes de la primera escena";
    internal const string MotivoEncabezadoInvalido =
        "Encabezado inválido: se espera ESCENA N — INT./EXT. LUGAR — MOMENTO";
    internal const string MotivoClipInvalido = "Marcador de clip inválido: se espera CLIP N";
    internal const string MotivoLimiteEscenas = "Se superó el límite de 10 escenas";
    internal const string MotivoLimiteClips = "Se superó el límite de 20 clips por escena";
    internal const string MotivoFueraDeLugar =
        "Línea fuera de lugar: entre el encabezado y el primer CLIP solo van los campos de escena";
    internal const string MotivoSinClips = "La escena no tiene ningún CLIP";
    internal const string MotivoClipSinContenido = "El clip no tiene contenido";
    internal const string MotivoTextoEnPantallaSinComillas = "El texto en pantalla debe ir entre comillas";
    internal const string MotivoTextoEnPantallaRepetido = "Más de un texto en pantalla en el clip";
    internal const string MotivoDialogoSinComillas = "El diálogo debe ir entre comillas";
    internal const string MotivoTextoTrasComilla = "Hay texto después de la comilla de cierre";
    internal const string MotivoComillasVacias = "Las comillas están vacías";
    internal const string MotivoAcotacionVacia = "La acotación está vacía";
    internal const string MotivoAcotacionSinCerrar = "Falta cerrar el paréntesis de la acotación";
    internal const string MotivoNoNfc =
        "Texto con tildes en formato no estándar (NFD); vuelve a pegarlo desde otro editor";

    /// <summary>
    /// Parsea y valida el guion. Un texto null se trata como vacío. Nunca lanza por el contenido.
    /// </summary>
    /// <param name="texto">Texto completo del guion.</param>
    /// <returns>El guion parseado, o todos los errores ordenados por línea.</returns>
    public static ResultadoParseo Parsear(string? texto)
    {
        var analizador = new Analizador();
        var lineas = (texto ?? string.Empty).Split('\n');
        if (lineas[0].StartsWith('\uFEFF'))
        {
            lineas[0] = lineas[0][1..];
        }

        for (var i = 0; i < lineas.Length; i++)
        {
            var linea = lineas[i].Trim();
            if (linea.Length == 0)
            {
                continue;
            }

            if (linea.IsNormalized(NormalizationForm.FormC))
            {
                analizador.Procesar(linea, i + 1);
            }
            else
            {
                analizador.ProcesarNoNormalizada(linea, i + 1);
            }
        }

        return analizador.Terminar();
    }

    private enum Estado
    {
        AntesDeEscena,
        EnCampos,
        EnClip,
    }

    private sealed class EscenaEnCurso(int numero, int linea, bool valida, IntExt intExt, string lugar, string momento)
    {
        public int Numero { get; } = numero;
        public int Linea { get; } = linea;
        public bool Valida { get; } = valida;
        public IntExt IntExt { get; } = intExt;
        public string Lugar { get; } = lugar;
        public string Momento { get; } = momento;
        public string?[] Campos { get; } = new string?[NombresCampos.Length];
        public bool[] Presentes { get; } = new bool[NombresCampos.Length];
        public List<ClipParseado> Clips { get; } = [];
        public int ClipsContados { get; set; }
        public int UltimoNumeroClip { get; set; }
        public ClipEnCurso? Clip { get; set; }
    }

    private sealed class ClipEnCurso(int numero, int linea, bool valido)
    {
        public int Numero { get; } = numero;
        public int Linea { get; } = linea;
        public bool Valido { get; } = valido;
        public List<string> Accion { get; } = [];
        public List<LineaDialogoParseada> Dialogos { get; } = [];
        public string? TextoEnPantalla { get; set; }
        public string? TextoEnPantallaOriginal { get; set; }
        public bool TieneTextoEnPantalla { get; set; }
        public bool TieneContenido { get; set; }
    }

    private sealed class Analizador
    {
        private readonly List<ErrorGuion> _errores = [];
        private readonly List<EscenaParseada> _escenas = [];
        private Estado _estado = Estado.AntesDeEscena;
        private EscenaEnCurso? _escena;
        private int _encabezados;
        private int _ultimoNumeroEscena;
        private bool _textoAntesReportado;

        public void Procesar(string linea, int numero)
        {
            if (EmpiezaConPalabra(linea, "ESCENA"))
            {
                ProcesarEncabezado(linea, numero);
                return;
            }

            if (_estado == Estado.AntesDeEscena)
            {
                if (!_textoAntesReportado)
                {
                    Error(numero, MotivoTextoAntes);
                    _textoAntesReportado = true;
                }

                return;
            }

            if (EmpiezaConPalabra(linea, "CLIP"))
            {
                ProcesarClip(linea, numero);
                return;
            }

            if (linea.StartsWith("TEXTO EN PANTALLA", StringComparison.OrdinalIgnoreCase))
            {
                if (_estado == Estado.EnCampos)
                {
                    Error(numero, MotivoFueraDeLugar);
                }
                else
                {
                    ProcesarTextoEnPantalla(linea, numero);
                }

                return;
            }

            var campo = CampoEscena.Match(linea);
            if (campo.Success)
            {
                ProcesarCampo(Array.IndexOf(NombresCampos, campo.Groups[1].Value), linea[campo.Length..].Trim(), numero);
                return;
            }

            if (_estado == Estado.EnCampos)
            {
                Error(numero, MotivoFueraDeLugar);
                return;
            }

            var clip = _escena!.Clip!;
            clip.TieneContenido = true;
            if (MarcadorDialogo.IsMatch(linea))
            {
                ProcesarDialogo(clip, linea, numero);
            }
            else
            {
                clip.Accion.Add(linea);
            }
        }

        /// <summary>
        /// Línea que no está en NFC (p. ej., tildes como letra + marca combinante, típico al pegar
        /// desde macOS). Se procesa su forma NFC solo para que la estructura avance (escena,
        /// campos, clip, contenido) y no aparezcan errores en cascada; los errores que esa línea
        /// genere en sí misma se reemplazan por un único <see cref="MotivoNoNfc"/>. El guion
        /// queda inválido, así que <see cref="GuionParseado"/> es null y ningún valor normalizado
        /// sale del parser: el texto nunca se corrige.
        /// </summary>
        public void ProcesarNoNormalizada(string linea, int numero)
        {
            var marca = _errores.Count;
            Procesar(linea.Normalize(NormalizationForm.FormC), numero);

            // Solo se descartan los errores de esta línea: al abrir una escena se cierra la
            // anterior, y sus errores (en otras líneas) son legítimos.
            for (var i = _errores.Count - 1; i >= marca; i--)
            {
                if (_errores[i].Linea == numero)
                {
                    _errores.RemoveAt(i);
                }
            }

            Error(numero, MotivoNoNfc);
        }

        public ResultadoParseo Terminar()
        {
            CerrarEscena();

            if (_encabezados == 0)
            {
                return ResultadoParseo.Invalido([new ErrorGuion(1, MotivoSinEscena)]);
            }

            if (_errores.Count > 0)
            {
                return ResultadoParseo.Invalido(_errores.OrderBy(e => e.Linea));
            }

            return ResultadoParseo.Valido(new GuionParseado(_escenas.AsReadOnly()));
        }

        private void ProcesarEncabezado(string linea, int numero)
        {
            CerrarEscena();

            _encabezados++;
            if (_encabezados == MaximoEscenas + 1)
            {
                Error(numero, MotivoLimiteEscenas);
            }

            var esperado = _ultimoNumeroEscena + 1;
            var valido = TryLeerEncabezado(linea, out var numeroLeido, out var intExt, out var lugar, out var momento);
            if (!valido)
            {
                Error(numero, MotivoEncabezadoInvalido);
                numeroLeido = esperado;
            }
            else if (numeroLeido != esperado)
            {
                Error(numero, $"Se esperaba ESCENA {esperado}");
                if (numeroLeido <= 0)
                {
                    numeroLeido = esperado;
                }
            }

            _ultimoNumeroEscena = numeroLeido;
            _escena = new EscenaEnCurso(numeroLeido, numero, valido, intExt, lugar, momento);
            _estado = Estado.EnCampos;
        }

        private void ProcesarClip(string linea, int numero)
        {
            var escena = _escena!;
            CerrarClip(escena);

            escena.ClipsContados++;
            if (escena.ClipsContados == MaximoClipsPorEscena + 1)
            {
                Error(numero, MotivoLimiteClips);
            }

            var esperado = escena.UltimoNumeroClip + 1;
            var marcador = MarcadorClip.Match(linea);
            var valido = marcador.Success;
            int numeroClip;
            if (!valido)
            {
                Error(numero, MotivoClipInvalido);
                numeroClip = esperado;
            }
            else
            {
                numeroClip = LeerNumero(marcador.Groups[1].Value);
                if (numeroClip != esperado)
                {
                    Error(numero, $"Se esperaba CLIP {esperado}");
                    if (numeroClip <= 0)
                    {
                        numeroClip = esperado;
                    }
                }
            }

            escena.UltimoNumeroClip = numeroClip;
            escena.Clip = new ClipEnCurso(numeroClip, numero, valido);
            _estado = Estado.EnClip;
        }

        private void ProcesarCampo(int indice, string valor, int numero)
        {
            var escena = _escena!;
            var nombre = NombresCampos[indice];

            if (_estado == Estado.EnClip)
            {
                Error(numero, $"El campo {nombre} va antes del primer CLIP");
                return;
            }

            if (escena.Presentes[indice])
            {
                Error(numero, $"Campo repetido: {nombre}");
                return;
            }

            var faltante = -1;
            for (var j = indice - 1; j >= 0; j--)
            {
                if (!escena.Presentes[j])
                {
                    faltante = j;
                    break;
                }
            }

            if (faltante >= 0)
            {
                Error(numero, $"Campo fuera de orden: va después de {NombresCampos[faltante]}");
            }

            if (valor.Length == 0)
            {
                Error(numero, $"Campo sin valor: {nombre}");
            }

            escena.Presentes[indice] = true;
            escena.Campos[indice] = valor;
        }

        private void ProcesarTextoEnPantalla(string linea, int numero)
        {
            var clip = _escena!.Clip!;
            clip.TieneContenido = true;

            var repetido = clip.TieneTextoEnPantalla;
            clip.TieneTextoEnPantalla = true;
            if (repetido)
            {
                Error(numero, MotivoTextoEnPantallaRepetido);
            }

            var coincidencia = TextoEnPantalla.Match(linea);
            if (!coincidencia.Success)
            {
                Error(numero, MotivoTextoEnPantallaSinComillas);
                return;
            }

            var valor = coincidencia.Groups[1].Value.Trim();
            if (ValidarComillas(valor, out var interior) is not null)
            {
                Error(numero, MotivoTextoEnPantallaSinComillas);
                return;
            }

            if (!repetido)
            {
                clip.TextoEnPantalla = interior;
                clip.TextoEnPantallaOriginal = valor;
            }
        }

        private void ProcesarDialogo(ClipEnCurso clip, string linea, int numero)
        {
            // La detección garantiza que el primer '(' o ':' de la línea cierra el nombre.
            var abre = linea.IndexOf('(');
            var dosPuntos = linea.IndexOf(':');
            string personaje;
            string? acotacion = null;
            int inicioTexto;

            if (abre >= 0 && (dosPuntos < 0 || abre < dosPuntos))
            {
                personaje = linea[..abre].Trim();

                // Como el (.*) codicioso de la regex del spec: la acotación cierra en el ÚLTIMO
                // ')' seguido de espacios opcionales y ':'. Búsqueda lineal hacia atrás.
                var cierra = -1;
                var dosPuntosTrasCierre = -1;
                for (var i = linea.Length - 1; i > abre; i--)
                {
                    if (linea[i] != ')')
                    {
                        continue;
                    }

                    var k = i + 1;
                    while (k < linea.Length && char.IsWhiteSpace(linea[k]))
                    {
                        k++;
                    }

                    if (k < linea.Length && linea[k] == ':')
                    {
                        cierra = i;
                        dosPuntosTrasCierre = k;
                        break;
                    }
                }

                if (cierra < 0)
                {
                    // Con ':' tras el '(' falta cerrar la acotación; sin ningún ':' la línea no
                    // tiene la forma PERSONAJE (acotación): "texto".
                    Error(numero, linea.IndexOf(':', abre) >= 0 ? MotivoAcotacionSinCerrar : MotivoDialogoSinComillas);
                    return;
                }

                acotacion = linea[(abre + 1)..cierra].Trim();
                if (acotacion.Length == 0)
                {
                    Error(numero, MotivoAcotacionVacia);
                    return;
                }

                inicioTexto = dosPuntosTrasCierre + 1;
            }
            else
            {
                personaje = linea[..dosPuntos].Trim();
                inicioTexto = dosPuntos + 1;
            }

            var motivo = ValidarComillas(linea[inicioTexto..].Trim(), out var texto);
            if (motivo is not null)
            {
                Error(numero, motivo);
                return;
            }

            var orden = clip.Dialogos.Count + 1;
            clip.Dialogos.Add(new LineaDialogoParseada($"c{clip.Numero}-d{orden}", orden, personaje, acotacion, texto, linea));
        }

        private void CerrarEscena()
        {
            var escena = _escena;
            if (escena is null)
            {
                return;
            }

            CerrarClip(escena);
            _escena = null;

            if (!escena.Valida)
            {
                return;
            }

            for (var i = 0; i < NombresCampos.Length; i++)
            {
                if (!escena.Presentes[i])
                {
                    Error(escena.Linea, $"Falta el campo {NombresCampos[i]}");
                }
            }

            if (escena.ClipsContados == 0)
            {
                Error(escena.Linea, MotivoSinClips);
            }

            _escenas.Add(new EscenaParseada(
                escena.Numero,
                escena.Linea,
                escena.IntExt,
                escena.Lugar,
                escena.Momento,
                escena.Campos[0] ?? string.Empty,
                escena.Campos[1] ?? string.Empty,
                escena.Campos[2] ?? string.Empty,
                escena.Campos[3] ?? string.Empty,
                escena.Clips.AsReadOnly()));
        }

        private void CerrarClip(EscenaEnCurso escena)
        {
            var clip = escena.Clip;
            if (clip is null)
            {
                return;
            }

            escena.Clip = null;
            if (clip.Valido && !clip.TieneContenido)
            {
                Error(clip.Linea, MotivoClipSinContenido);
            }

            escena.Clips.Add(new ClipParseado(
                clip.Numero,
                clip.Linea,
                clip.Accion.AsReadOnly(),
                clip.Dialogos.AsReadOnly(),
                clip.TextoEnPantalla,
                clip.TextoEnPantallaOriginal));
        }

        private void Error(int linea, string motivo) => _errores.Add(new ErrorGuion(linea, motivo));
    }

    /// <summary>
    /// Valida la forma del encabezado. El prefijo (número e INT./EXT.) va por regex; LUGAR y
    /// MOMENTO se separan con el último separador (<c>—</c> o <c>-</c> rodeado de espacios),
    /// buscado de atrás hacia adelante sin regex.
    /// </summary>
    private static bool TryLeerEncabezado(string linea, out int numero, out IntExt intExt, out string lugar, out string momento)
    {
        numero = 0;
        intExt = IntExt.Int;
        lugar = string.Empty;
        momento = string.Empty;

        var prefijo = PrefijoEncabezado.Match(linea);
        if (!prefijo.Success)
        {
            return false;
        }

        var resto = linea.AsSpan(prefijo.Length);
        var separador = -1;
        for (var i = resto.Length - 2; i >= 0; i--)
        {
            if ((resto[i] == '—' || resto[i] == '-')
                && char.IsWhiteSpace(resto[i + 1])
                && (i == 0 || char.IsWhiteSpace(resto[i - 1])))
            {
                separador = i;
                break;
            }
        }

        if (separador < 0)
        {
            return false;
        }

        lugar = resto[..separador].Trim().ToString();
        momento = resto[(separador + 1)..].Trim().ToString();
        if (lugar.Length == 0 || momento.Length == 0)
        {
            return false;
        }

        numero = LeerNumero(prefijo.Groups[1].Value);
        intExt = prefijo.Groups[2].Value == "INT." ? IntExt.Int : IntExt.Ext;
        return true;
    }

    /// <summary>Lee un número de marcador; si no cabe en un <c>int</c>, devuelve -1.</summary>
    private static int LeerNumero(string digitos) => int.TryParse(digitos, out var n) ? n : -1;

    /// <summary>
    /// <c>true</c> si la línea empieza con la palabra (sin distinguir mayúsculas) seguida de
    /// espacio, dígito o fin de línea. Es la detección permisiva de <c>ESCENA</c> y <c>CLIP</c>.
    /// </summary>
    private static bool EmpiezaConPalabra(string linea, string palabra)
    {
        if (!linea.StartsWith(palabra, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (linea.Length == palabra.Length)
        {
            return true;
        }

        var siguiente = linea[palabra.Length];
        return char.IsWhiteSpace(siguiente) || char.IsAsciiDigit(siguiente);
    }

    /// <summary>
    /// Valida un texto entre comillas rectas o tipográficas del mismo tipo. Devuelve null si es
    /// válido (con el interior en <paramref name="interior"/>), o el motivo del error.
    /// </summary>
    private static string? ValidarComillas(string valor, out string interior)
    {
        interior = string.Empty;
        if (valor.Length == 0)
        {
            return MotivoDialogoSinComillas;
        }

        char cierre;
        if (valor[0] == '"')
        {
            cierre = '"';
        }
        else if (valor[0] == '“')
        {
            cierre = '”';
        }
        else
        {
            return MotivoDialogoSinComillas;
        }

        if (valor.IndexOf(cierre, 1) < 0)
        {
            return MotivoDialogoSinComillas;
        }

        if (valor[^1] != cierre)
        {
            return MotivoTextoTrasComilla;
        }

        interior = valor[1..^1];
        return interior.Trim().Length == 0 ? MotivoComillasVacias : null;
    }
}
