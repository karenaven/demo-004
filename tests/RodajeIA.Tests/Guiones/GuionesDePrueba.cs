using System.Text;

namespace RodajeIA.Tests.Guiones;

/// <summary>Guiones de prueba: la escena de ejemplo del Anexo A y generadores.</summary>
public static class GuionesDePrueba
{
    public const string Locacion =
        "Restaurante pequeño y acogedor; pared de ladrillo visto, mesas de madera oscura, vela en portavelas de cristal.";

    public const string Iluminacion =
        "Luz cálida ambiental (aprox. 2700 K), principal suave cenital de la lámpara sobre la mesa, reflejo de la vela en los rostros.";

    public const string PuestaEnEscena =
        "Sentados frente a frente en una mesa para dos. Ana siempre a la IZQUIERDA del encuadre, José Daniel siempre a la DERECHA.";

    public const string Audio =
        "Ambiente tranquilo de restaurante, murmullo lejano y cubiertos; SIN música. Diálogo en español, acento latinoamericano neutro.";

    /// <summary>La escena de ejemplo del Anexo A del PRD base, tal cual.</summary>
    public static readonly string EscenaDeEjemplo = Unir(
        "ESCENA 1 — INT. RESTAURANTE — NOCHE",
        "LOCACIÓN: " + Locacion,
        "ILUMINACIÓN: " + Iluminacion,
        "PUESTA EN ESCENA: " + PuestaEnEscena,
        "AUDIO: " + Audio,
        "CLIP 1",
        "Ana y José Daniel conversan en una mesa junto a la ventana. José Daniel sonríe pícaramente.",
        "ANA: \"Mentiroso.\"",
        "JOSÉ DANIEL (sonriendo): \"Bueno... pasó parecido.\"",
        "Ana niega con la cabeza, divertida, con una sonrisa amplia.",
        "CLIP 2",
        "Un mesero deja dos platos sobre la mesa. Ana y José Daniel agradecen con gestos breves.");

    /// <summary>
    /// La misma escena con <c>-</c> en lugar de <c>—</c> en el encabezado, líneas en blanco entre
    /// campos y entre clips, y finales de línea <c>\r\n</c>.
    /// </summary>
    public static readonly string EscenaDeEjemploConGuionComunYLineasEnBlanco = string.Join(
        "\r\n",
        "",
        "ESCENA 1 - INT. RESTAURANTE - NOCHE",
        "",
        "LOCACIÓN: " + Locacion,
        "   ",
        "ILUMINACIÓN: " + Iluminacion,
        "",
        "PUESTA EN ESCENA: " + PuestaEnEscena,
        "",
        "AUDIO: " + Audio,
        "",
        "CLIP 1",
        "Ana y José Daniel conversan en una mesa junto a la ventana. José Daniel sonríe pícaramente.",
        "",
        "ANA: \"Mentiroso.\"",
        "JOSÉ DANIEL (sonriendo): \"Bueno... pasó parecido.\"",
        "Ana niega con la cabeza, divertida, con una sonrisa amplia.",
        "",
        "",
        "CLIP 2",
        "Un mesero deja dos platos sobre la mesa. Ana y José Daniel agradecen con gestos breves.",
        "");

    /// <summary>Une líneas con <c>\n</c>; la línea i del arreglo es la línea i+1 del guion.</summary>
    public static string Unir(params string[] lineas) => string.Join("\n", lineas);

    /// <summary>Encabezado y los cuatro campos de una escena (5 líneas).</summary>
    public static string[] EncabezadoYCampos(int numero) =>
    [
        $"ESCENA {numero} — INT. CASA — DÍA",
        "LOCACIÓN: Sala con un sofá gris.",
        "ILUMINACIÓN: Luz de día por la ventana.",
        "PUESTA EN ESCENA: Ana de pie junto a la ventana.",
        "AUDIO: Silencio, tráfico lejano.",
    ];

    /// <summary>
    /// Escena válida mínima: encabezado, cuatro campos, <c>CLIP 1</c> y una línea de acción
    /// (7 líneas).
    /// </summary>
    public static string[] EscenaMinima(int numero) =>
        [.. EncabezadoYCampos(numero), "CLIP 1", "Ana mira por la ventana."];

    /// <summary>Genera un guion válido de <paramref name="escenas"/> × <paramref name="clips"/>.</summary>
    public static string Generar(int escenas, int clips)
    {
        var sb = new StringBuilder();
        for (var e = 1; e <= escenas; e++)
        {
            sb.Append($"ESCENA {e} — INT. LUGAR {e} - PISO {e} — NOCHE\n");
            sb.Append($"LOCACIÓN: Locación de la escena {e}, mesas de madera y una vela.\n");
            sb.Append("ILUMINACIÓN: Luz cálida ambiental, principal suave cenital.\n");
            sb.Append("PUESTA EN ESCENA: Ana a la izquierda, José Daniel a la derecha.\n");
            sb.Append("AUDIO: Ambiente tranquilo, sin música.\n");
            for (var c = 1; c <= clips; c++)
            {
                sb.Append($"CLIP {c}\n");
                sb.Append($"Ana y José Daniel conversan en el clip {c} de la escena {e}.\n");
                sb.Append($"ANA (sonriendo): \"Línea {c}.\"\n");
                sb.Append($"JOSÉ DANIEL: “Respuesta {c}.”\n");
                if (c % 5 == 0)
                {
                    sb.Append($"TEXTO EN PANTALLA: \"Rótulo {c}\"\n");
                }
            }

            sb.Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>Número de línea (desde 1) de la primera línea que empieza con el prefijo dado.</summary>
    public static int LineaDe(string texto, string prefijo)
    {
        var lineas = texto.Split('\n');
        for (var i = 0; i < lineas.Length; i++)
        {
            if (lineas[i].StartsWith(prefijo, StringComparison.Ordinal))
            {
                return i + 1;
            }
        }

        throw new InvalidOperationException($"No hay ninguna línea que empiece con «{prefijo}».");
    }
}
