namespace RodajeIA.Web.Datos;

public class Episodio
{
    public int Id { get; set; }

    public int SerieId { get; set; }

    public Serie? Serie { get; set; }

    /// <summary>Número del episodio, único dentro de la serie.</summary>
    public int Numero { get; set; }

    public string Titulo { get; set; } = "";

    /// <summary>Texto del último guion cargado; null hasta la primera carga.</summary>
    public string? Guion { get; set; }

    public List<Escena> Escenas { get; set; } = [];
}
