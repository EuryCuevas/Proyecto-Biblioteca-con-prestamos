using Biblioteca.Correo.Entidades;
using Microsoft.EntityFrameworkCore;

namespace Biblioteca.Correo.Persistencia;

/// <summary>
/// Contexto de la cola de correo. Contexto propio, independiente del de Identidad:
/// la pieza Correo no conoce al usuario y la pieza Identidad sólo conoce el puerto
/// <see cref="IEncolaCorreo"/>. Así una prueba de recuperación de contraseña puede
/// verificar el correo encolado sin levantar SMTP (RF-CA-10, RD-12).
/// </summary>
public sealed class CorreoDbContext(DbContextOptions<CorreoDbContext> opciones)
    : DbContext(opciones)
{
    public DbSet<CorreoEnCola> CorreosEnCola => Set<CorreoEnCola>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<CorreoEnCola>(entidad =>
        {
            entidad.ToTable("CorreoEnCola");
            entidad.HasKey(c => c.Id);

            entidad.Property(c => c.Destinatario).HasMaxLength(256).IsRequired();
            entidad.Property(c => c.Asunto).HasMaxLength(200).IsRequired();
            entidad.Property(c => c.Cuerpo).IsRequired();
            entidad.Property(c => c.Estado)
                  .HasConversion<string>()
                  .HasMaxLength(20)
                  .IsRequired();
            entidad.Property(c => c.UltimoError).HasMaxLength(400);
            entidad.Property(c => c.Plantilla).HasMaxLength(60);
            entidad.Property(c => c.DatoPlantilla).HasMaxLength(500);

            // El emisor consulta constantemente lo pendiente: índice de apoyo.
            entidad.HasIndex(c => new { c.Estado, c.CreadoEn });
        });
    }
}