namespace RodajeIA.Web.Datos;

public class LineaDialogo
{
    public int Id { get; set; }

    public int ClipId { get; set; }

    public Clip? Clip { get; set; }

    /// <summary>Id de la línea con formato <c>c{clip}-d{n}</c>, único dentro del clip (y por tanto de la escena).</summary>
    public string Codigo { get; set; } = "";

    /// <summary>Orden dentro del clip.</summary>
    public int Orden { get; set; }

    /// <summary>Marcador del personaje tal como aparece en el guion. Texto, no FK: puede no tener hoja.</summary>
    public string Personaje { get; set; } = "";

    public string? Acotacion { get; set; }

    /// <summary>Texto del diálogo sin comillas.</summary>
    public string Texto { get; set; } = "";

    /// <summary>La línea tal como está en el guion.</summary>
    public string Original { get; set; } = "";
}
