namespace RodajeIA.Web.Datos;

public class Serie
{
    public int Id { get; set; }

    public string Nombre { get; set; } = "";

    public List<Personaje> Personajes { get; set; } = [];

    public List<Episodio> Episodios { get; set; } = [];
}
