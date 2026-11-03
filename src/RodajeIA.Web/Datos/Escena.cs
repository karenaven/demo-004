namespace RodajeIA.Web.Datos;

public class Escena
{
    public int Id { get; set; }

    public int EpisodioId { get; set; }

    public Episodio? Episodio { get; set; }

    /// <summary>Número de la escena, único dentro del episodio.</summary>
    public int Numero { get; set; }

    public IntExt IntExt { get; set; }

    public string Lugar { get; set; } = "";

    public string Momento { get; set; } = "";

    public string Locacion { get; set; } = "";

    public string Iluminacion { get; set; } = "";

    public string PuestaEnEscena { get; set; } = "";

    public string Audio { get; set; } = "";

    public EstadoGeneracion Estado { get; set; } = EstadoGeneracion.Pendiente;

    public List<Clip> Clips { get; set; } = [];

    /// <summary>Personajes con hoja presentes en la escena (tabla de unión <c>EscenaPersonaje</c>).</summary>
    public List<Personaje> Personajes { get; set; } = [];
}
