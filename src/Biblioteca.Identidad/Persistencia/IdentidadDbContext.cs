using Biblioteca.Identidad.Entidades;
using Microsoft.EntityFrameworkCore;

namespace Biblioteca.Identidad.Persistencia;

/// <summary>
/// Contexto de persistencia de la pieza 1 (Control de acceso). La pieza es dueña
/// de sus propias tablas: no comparte contexto con Correo ni con el módulo de
/// negocio, lo que mantiene cada componente autónomo (RD-01).
///
/// Convenciones aplicadas en todo el sistema:
/// - Fechas en UTC y <c>DATETIME2(3)</c>, con un único criterio de reloj (RD-11).
/// - El proveedor SQL Server se registra en el proyecto host, no aquí.
/// </summary>
public sealed class IdentidadDbContext(DbContextOptions<IdentidadDbContext> opciones)
    : DbContext(opciones)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<Sesion> Sesiones => Set<Sesion>();

    public DbSet<TokenActivacion> TokensActivacion => Set<TokenActivacion>();

    public DbSet<CodigoRecuperacion> CodigosRecuperacion => Set<CodigoRecuperacion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Usuario>(entidad =>
        {
            entidad.ToTable("Usuario");
            entidad.HasKey(u => u.Id);

            entidad.Property(u => u.Correo).HasMaxLength(256).IsRequired();

            // Correo único: la regla la impone la base, no sólo el código (RF-CA-01).
            entidad.HasIndex(u => u.Correo).IsUnique().HasDatabaseName("UQ_Usuario_Correo");

            // Se persiste como texto para que el rol sea legible en SSMS y en los reportes.
            entidad.Property(u => u.Rol).HasConversion<string>().HasMaxLength(20).IsRequired();

            entidad.Property(u => u.PasswordHash).HasMaxLength(400).IsRequired();
            entidad.Property(u => u.Nombre).HasMaxLength(120).IsRequired();
            entidad.Property(u => u.Activo).IsRequired();

            entidad.HasIndex(u => u.BloqueadoHasta);

            entidad.HasMany(u => u.Sesiones)
                  .WithOne(s => s.Usuario)
                  .HasForeignKey(s => s.UsuarioId)
                  .OnDelete(DeleteBehavior.Cascade);

            entidad.HasMany(u => u.TokensActivacion)
                  .WithOne(t => t.Usuario)
                  .HasForeignKey(t => t.UsuarioId)
                  .OnDelete(DeleteBehavior.Cascade);

            entidad.HasMany(u => u.CodigosRecuperacion)
                  .WithOne(c => c.Usuario)
                  .HasForeignKey(c => c.UsuarioId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Sesion>(entidad =>
        {
            entidad.ToTable("Sesion");
            entidad.HasKey(s => s.Id);

            entidad.Property(s => s.TokenHash).HasMaxLength(32).IsFixedLength().IsRequired();
            entidad.HasIndex(s => s.TokenHash).IsUnique().HasDatabaseName("UQ_Sesion_TokenHash");

            entidad.Property(s => s.MotivoCierre).HasMaxLength(80);

            // Índice de apoyo para cerrar todas las sesiones de un usuario (RF-CA-12).
            entidad.HasIndex(s => new { s.UsuarioId, s.CerradaEn });
        });

        modelBuilder.Entity<TokenActivacion>(entidad =>
        {
            entidad.ToTable("TokenActivacion");
            entidad.HasKey(t => t.Id);

            entidad.Property(t => t.TokenHash).HasMaxLength(32).IsFixedLength().IsRequired();
            entidad.HasIndex(t => t.TokenHash).IsUnique().HasDatabaseName("UQ_TokenActivacion_TokenHash");

            entidad.HasIndex(t => new { t.UsuarioId, t.UsadoEn });
        });

        modelBuilder.Entity<CodigoRecuperacion>(entidad =>
        {
            entidad.ToTable("CodigoRecuperacion");
            entidad.HasKey(c => c.Id);

            entidad.Property(c => c.CodigoHash).HasMaxLength(32).IsFixedLength().IsRequired();
            entidad.HasIndex(c => c.CodigoHash).IsUnique().HasDatabaseName("UQ_CodigoRecuperacion_CodigoHash");

            entidad.HasIndex(c => new { c.UsuarioId, c.UsadoEn });
        });
    }
}