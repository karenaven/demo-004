namespace RodajeIA.Web.Datos;

/// <summary>Hoja de personaje de una serie.</summary>
public class Personaje
{
    public int Id { get; set; }

    public int SerieId { get; set; }

    public Serie? Serie { get; set; }

    public string Nombre { get; set; } = "";

    public string Edad { get; set; } = "";

    /// <summary>Descripción física, incluida la voz.</summary>
    public string DescripcionFisica { get; set; } = "";

    public string Peinado { get; set; } = "";

    public string Vestuario { get; set; } = "";

    public string? HeridasMarcas { get; set; }

    public string Personalidad { get; set; } = "";

    public string Rol { get; set; } = "";

    public List<Escena> Escenas { get; set; } = [];

    public List<Clip> Clips { get; set; } = [];
}
