using Biblioteca.Biblioteca.Prestamos;
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
    public DbSet<Prestamo> Prestamos => Set<Prestamo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
        });
    }
}