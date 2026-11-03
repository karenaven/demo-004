namespace RodajeIA.Web.Datos;

public class Clip
{
    public int Id { get; set; }

    public int EscenaId { get; set; }

    public Escena? Escena { get; set; }

    /// <summary>Número del clip, único dentro de la escena.</summary>
    public int Numero { get; set; }

    /// <summary>Líneas de acción unidas con <c>\n</c>; vacía si el clip solo tiene diálogo o texto en pantalla.</summary>
    public string Accion { get; set; } = "";

    /// <summary>Texto en pantalla sin comillas.</summary>
    public string? TextoEnPantalla { get; set; }

    /// <summary>Texto en pantalla con sus comillas originales.</summary>
    public string? TextoEnPantallaOriginal { get; set; }

    public List<LineaDialogo> LineasDialogo { get; set; } = [];

    /// <summary>Personajes con hoja presentes en el clip (tabla de unión <c>ClipPersonaje</c>).</summary>
    public List<Personaje> Personajes { get; set; } = [];
}
