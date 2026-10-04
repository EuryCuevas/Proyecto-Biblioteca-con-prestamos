using Biblioteca.Biblioteca.Catalogo;
using Biblioteca.Biblioteca.Prestamos;
using Biblioteca.Biblioteca.Socios;
using Microsoft.EntityFrameworkCore;

namespace Biblioteca.Biblioteca.Persistencia;

/// <summary>
/// Contexto del módulo de negocio. Deliberadamente separado del contexto de
/// Identidad y del de Correo: el Core no depende de este módulo (RD-03) y borrar
/// este proyecto deja el Core compilando y ejecutándose.
/// </summary>
public sealed class BibliotecaDbContext(DbContextOptions<BibliotecaDbContext> opciones)
    : DbContext(opciones)
{
    public DbSet<Recurso> Recursos => Set<Recurso>();

    public DbSet<Ejemplar> Ejemplares => Set<Ejemplar>();

    public DbSet<Socio> Socios => Set<Socio>();

    public DbSet<Prestamo> Prestamos => Set<Prestamo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ---- Catálogo: Recurso 1 --- N Ejemplar -------------------------------
        modelBuilder.Entity<Recurso>(entidad =>
        {
            entidad.ToTable("Recurso");
            entidad.HasKey(r => r.Id);

            entidad.Property(r => r.Titulo).HasMaxLength(300).IsRequired();
            entidad.Property(r => r.Autor).HasMaxLength(200).IsRequired();
            entidad.Property(r => r.Isbn).HasMaxLength(20);
            entidad.Property(r => r.Editorial).HasMaxLength(120);
            entidad.Property(r => r.Genero).HasMaxLength(60);

            // El ISBN identifica la obra. El índice es PARCIAL para no obligar a que
            // todos los recursos lo tengan: muchos materiales no lo llevan.
            entidad.HasIndex(r => r.Isbn)
                  .IsUnique()
                  .HasFilter("[Isbn] IS NOT NULL")
                  .HasDatabaseName("UQ_Recurso_Isbn");

            entidad.HasMany(r => r.Ejemplares)
                  .WithOne(e => e.Recurso!)
                  .HasForeignKey(e => e.RecursoId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Ejemplar>(entidad =>
        {
            entidad.ToTable("Ejemplar");
            entidad.HasKey(e => e.Id);

            entidad.Property(e => e.Codigo).HasMaxLength(30).IsRequired();

            entidad.HasIndex(e => e.Codigo)
                  .IsUnique()
                  .HasDatabaseName("UQ_Ejemplar_Codigo");

            // Sin columna «Disponible»: se deriva de los préstamos vivos. Ver <see cref="Ejemplar"/>.
            entidad.HasOne(e => e.Recurso)
                  .WithMany(r => r.Ejemplares)
                  .HasForeignKey(e => e.RecursoId)
                  .OnDelete(DeleteBehavior.Restrict);

            entidad.HasMany(e => e.Prestamos)
                  .WithOne(p => p.Ejemplar!)
                  .HasForeignKey(p => p.EjemplarId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- Socio 1 --- N Préstamo ------------------------------------------
        modelBuilder.Entity<Socio>(entidad =>
        {
            entidad.ToTable("Socio");
            entidad.HasKey(s => s.Id);

            entidad.Property(s => s.NumeroSocio).HasMaxLength(20).IsRequired();

            entidad.HasIndex(s => s.NumeroSocio)
                  .IsUnique()
                  .HasDatabaseName("UQ_Socio_NumeroSocio");

            // Un usuario del Core es, como mucho, un socio de esta biblioteca.
            // El índice es único SIN llave foránea a propósito: la unicidad la impone
            // la base sin que este esquema dependa de las tablas del Core (RD-03).
            entidad.HasIndex(s => s.UsuarioId)
                  .IsUnique()
                  .HasDatabaseName("UQ_Socio_UsuarioId");

            entidad.HasMany(s => s.Prestamos)
                  .WithOne(p => p.Socio!)
                  .HasForeignKey(p => p.SocioId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- Préstamo --------------------------------------------------------
        modelBuilder.Entity<Prestamo>(entidad =>
        {
            entidad.ToTable("Prestamo");
            entidad.HasKey(p => p.Id);

            entidad.Property(p => p.Estado)
                  .HasConversion<string>()
                  .HasMaxLength(20)
                  .IsRequired();

            // La restricción a los 5 estados declarados vive en la base, para que
            // ningún camino pueda dejar un estado fuera de la máquina.
            entidad.ToTable(t => t.HasCheckConstraint(
                "CK_Prestamo_Estado",
                "Estado IN ('Solicitado','Activo','Devuelto','Rechazado','Perdido')"));

            entidad.Property(p => p.Motivo).HasMaxLength(400);

            entidad.HasIndex(p => new { p.SocioId, p.Estado });
            entidad.HasIndex(p => new { p.EjemplarId, p.Estado });

            // Un mismo ejemplar no puede tener dos préstamos no resueltos a la vez:
            // es la base de la disponibilidad.
            entidad.HasIndex(p => p.EjemplarId)
                  .IsUnique()
                  .HasFilter("[Estado] IN ('Solicitado','Activo')")
                  .HasDatabaseName("UX_Prestamo_EjemplarVivo");

            entidad.HasOne(p => p.Socio)
                  .WithMany(s => s.Prestamos)
                  .HasForeignKey(p => p.SocioId)
                  .OnDelete(DeleteBehavior.Restrict);

            entidad.HasOne(p => p.Ejemplar)
                  .WithMany(e => e.Prestamos)
                  .HasForeignKey(p => p.EjemplarId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}