using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using RodajeIA.Web.Datos;

namespace RodajeIA.Tests.Servicios;

/// <summary>Registro en memoria: nivel y mensaje ya formateado.</summary>
public sealed class RegistroDePrueba<T> : ILogger<T>
{
    public List<(LogLevel Nivel, string Mensaje)> Entradas { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entradas.Add((logLevel, formatter(state, exception)));
}

/// <summary>Fábrica que simula una base caída: no puede crear ningún contexto.</summary>
public sealed class FabricaSinBase : IDbContextFactory<RodajeDbContext>
{
    public RodajeDbContext CreateDbContext() => throw new SqliteException("Base de datos no disponible (simulado)", 14);
}

/// <summary>Fábrica sobre una conexión existente cuyos contextos llevan los interceptores dados.</summary>
public sealed class FabricaConInterceptores(SqliteConnection conexion, params IInterceptor[] interceptores)
    : IDbContextFactory<RodajeDbContext>
{
    private readonly DbContextOptions<RodajeDbContext> _opciones = new DbContextOptionsBuilder<RodajeDbContext>()
        .UseSqlite(conexion)
        .AddInterceptors(interceptores)
        .Options;

    public RodajeDbContext CreateDbContext() => new(_opciones);
}

/// <summary>Interceptor que lanza <see cref="DbUpdateException"/> al guardar cambios.</summary>
public sealed class InterceptorQueFallaAlGuardar : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) =>
        throw new DbUpdateException("Fallo simulado al guardar");

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
        throw new DbUpdateException("Fallo simulado al guardar");
}
