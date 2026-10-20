using Biblioteca.Permisos.Solicitudes;
using Microsoft.EntityFrameworkCore;

namespace Biblioteca.Permisos.Persistencia;

/// <summary>
/// Contexto de persistencia de la pieza 2 (Gestion de permisos).
/// Es dueño de sus propias tablas: SolicitudPermiso y Permiso.
/// No comparte contexto con las otras piezas (RD-01).
/// </summary>
public sealed class PermisosDbContext(DbContextOptions<PermisosDbContext> opciones)
    : DbContext(opciones)
{
    public DbSet<SolicitudPermiso> SolicitudesPermiso => Set<SolicitudPermiso>();

    public DbSet<Permiso> Permisos => Set<Permiso>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        base.OnModelCreating(modelo);

        modelo.Entity<SolicitudPermiso>(entidad =>
        {
            entidad.ToTable("SolicitudPermiso");
            entidad.HasKey(s => s.Id);

            entidad.Property(s => s.Operacion).HasMaxLength(100).IsRequired();
            entidad.Property(s => s.Estado).HasConversion<string>().HasMaxLength(20).IsRequired();
            entidad.Property(s => s.CreadaEn).HasColumnType("datetimeoffset").IsRequired();
            entidad.Property(s => s.AprobadaEn).HasColumnType("datetimeoffset");
            entidad.Property(s => s.AplicadaEn).HasColumnType("datetimeoffset");
            entidad.Property(s => s.RechazadaEn).HasColumnType("datetimeoffset");
            entidad.Property(s => s.MotivoRechazo).HasMaxLength(500);

            entidad.HasIndex(s => s.Estado);
            entidad.HasIndex(s => s.SolicitanteId);
        });

        modelo.Entity<Permiso>(entidad =>
        {
            entidad.ToTable("Permiso");
            entidad.HasKey(p => p.Id);

            entidad.Property(p => p.Operacion).HasMaxLength(100).IsRequired();
            entidad.Property(p => p.OtorgadoEn).HasColumnType("datetimeoffset").IsRequired();

            entidad.HasIndex(p => new { p.UsuarioId, p.Operacion }).IsUnique();
        });
    }
}
