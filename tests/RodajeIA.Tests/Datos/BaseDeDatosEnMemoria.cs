using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RodajeIA.Web.Datos;

namespace RodajeIA.Tests.Datos;

/// <summary>
/// Base SQLite en memoria real para tests: la conexión queda abierta mientras vive la instancia
/// (al cerrarla, la base desaparece). Cada test crea la suya y la descarta con <c>Dispose</c>.
/// </summary>
public sealed class BaseDeDatosEnMemoria : IDisposable
{
    private readonly SqliteConnection _conexion;

    public BaseDeDatosEnMemoria()
    {
        _conexion = new SqliteConnection("DataSource=:memory:");
        _conexion.Open();

        var opciones = new DbContextOptionsBuilder<RodajeDbContext>()
            .UseSqlite(_conexion)
            .Options;
        Fabrica = new FabricaDeContextos(opciones);

        using var contexto = Fabrica.CreateDbContext();
        contexto.Database.EnsureCreated();
    }

    public IDbContextFactory<RodajeDbContext> Fabrica { get; }

    /// <summary>La conexión abierta, para consultar las columnas tal como quedaron en SQLite.</summary>
    public SqliteConnection Conexion => _conexion;

    public RodajeDbContext CrearContexto() => Fabrica.CreateDbContext();

    public void Dispose() => _conexion.Dispose();

    private sealed class FabricaDeContextos(DbContextOptions<RodajeDbContext> opciones)
        : IDbContextFactory<RodajeDbContext>
    {
        public RodajeDbContext CreateDbContext() => new(opciones);
    }
}
