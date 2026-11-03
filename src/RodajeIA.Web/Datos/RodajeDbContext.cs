using Microsoft.EntityFrameworkCore;

namespace RodajeIA.Web.Datos;

/// <summary>
/// Contexto de EF Core sobre SQLite. Los servicios lo crean por operación con
/// <see cref="IDbContextFactory{TContext}"/>; los componentes Blazor nunca lo usan.
/// Microsoft.Data.Sqlite activa <c>foreign_keys=ON</c>, necesario para las cascadas.
/// </summary>
public class RodajeDbContext(DbContextOptions<RodajeDbContext> options) : DbContext(options)
{
    public DbSet<Serie> Series => Set<Serie>();

    public DbSet<Personaje> Personajes => Set<Personaje>();

    public DbSet<Episodio> Episodios => Set<Episodio>();

    public DbSet<Escena> Escenas => Set<Escena>();

    public DbSet<Clip> Clips => Set<Clip>();

    public DbSet<LineaDialogo> LineasDialogo => Set<LineaDialogo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Serie>(serie =>
        {
            serie.Property(s => s.Nombre).IsRequired().HasMaxLength(100);
        });

        modelBuilder.Entity<Personaje>(personaje =>
        {
            personaje.Property(p => p.Nombre).IsRequired().HasMaxLength(60);
            personaje.Property(p => p.Edad).IsRequired().HasMaxLength(40);
            personaje.Property(p => p.DescripcionFisica).IsRequired().HasMaxLength(2000);
            personaje.Property(p => p.Peinado).IsRequired().HasMaxLength(300);
            personaje.Property(p => p.Vestuario).IsRequired().HasMaxLength(500);
            personaje.Property(p => p.HeridasMarcas).HasMaxLength(500);
            personaje.Property(p => p.Personalidad).IsRequired().HasMaxLength(1000);
            personaje.Property(p => p.Rol).IsRequired().HasMaxLength(200);

            personaje.HasOne(p => p.Serie)
                .WithMany(s => s.Personajes)
                .HasForeignKey(p => p.SerieId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
            personaje.HasIndex(p => p.SerieId);
        });

        modelBuilder.Entity<Episodio>(episodio =>
        {
            episodio.Property(e => e.Titulo).IsRequired().HasMaxLength(150);

            episodio.HasOne(e => e.Serie)
                .WithMany(s => s.Episodios)
                .HasForeignKey(e => e.SerieId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
            episodio.HasIndex(e => new { e.SerieId, e.Numero }).IsUnique();
        });

        modelBuilder.Entity<Escena>(escena =>
        {
            escena.Property(e => e.IntExt).IsRequired().HasConversion<string>();
            escena.Property(e => e.Lugar).IsRequired().HasMaxLength(200);
            escena.Property(e => e.Momento).IsRequired().HasMaxLength(100);
            escena.Property(e => e.Locacion).IsRequired();
            escena.Property(e => e.Iluminacion).IsRequired();
            escena.Property(e => e.PuestaEnEscena).IsRequired();
            escena.Property(e => e.Audio).IsRequired();
            escena.Property(e => e.Estado)
                .IsRequired()
                .HasConversion<string>()
                .HasDefaultValue(EstadoGeneracion.Pendiente);

            escena.HasOne(e => e.Episodio)
                .WithMany(e => e.Escenas)
                .HasForeignKey(e => e.EpisodioId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
            escena.HasIndex(e => new { e.EpisodioId, e.Numero }).IsUnique();

            escena.HasMany(e => e.Personajes)
                .WithMany(p => p.Escenas)
                .UsingEntity<Dictionary<string, object>>(
                    "EscenaPersonaje",
                    union => union.HasOne<Personaje>().WithMany().HasForeignKey("PersonajeId").OnDelete(DeleteBehavior.Cascade),
                    union => union.HasOne<Escena>().WithMany().HasForeignKey("EscenaId").OnDelete(DeleteBehavior.Cascade),
                    union => union.HasKey("EscenaId", "PersonajeId"));
        });

        modelBuilder.Entity<Clip>(clip =>
        {
            clip.Property(c => c.Accion).IsRequired();

            clip.HasOne(c => c.Escena)
                .WithMany(e => e.Clips)
                .HasForeignKey(c => c.EscenaId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
            clip.HasIndex(c => new { c.EscenaId, c.Numero }).IsUnique();

            clip.HasMany(c => c.Personajes)
                .WithMany(p => p.Clips)
                .UsingEntity<Dictionary<string, object>>(
                    "ClipPersonaje",
                    union => union.HasOne<Personaje>().WithMany().HasForeignKey("PersonajeId").OnDelete(DeleteBehavior.Cascade),
                    union => union.HasOne<Clip>().WithMany().HasForeignKey("ClipId").OnDelete(DeleteBehavior.Cascade),
                    union => union.HasKey("ClipId", "PersonajeId"));
        });

        modelBuilder.Entity<LineaDialogo>(linea =>
        {
            linea.Property(l => l.Codigo).IsRequired().HasMaxLength(20);
            linea.Property(l => l.Personaje).IsRequired().HasMaxLength(100);
            linea.Property(l => l.Acotacion).HasMaxLength(200);
            linea.Property(l => l.Texto).IsRequired();
            linea.Property(l => l.Original).IsRequired();

            linea.HasOne(l => l.Clip)
                .WithMany(c => c.LineasDialogo)
                .HasForeignKey(l => l.ClipId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
            linea.HasIndex(l => new { l.ClipId, l.Codigo }).IsUnique();
        });
    }
}
