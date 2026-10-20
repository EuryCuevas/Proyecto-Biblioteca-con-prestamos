using Biblioteca.Permisos.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Biblioteca.Permisos.Solicitudes;

public interface IServicioDePermisos
{
    Task<HashSet<string>> OperacionesDeAsync(Guid usuarioId, CancellationToken cancelacion = default);
    Task OtorgarAsync(Guid usuarioId, string operacion, Guid solicitudId, DateTimeOffset momento, CancellationToken cancelacion = default);
}

public sealed class ServicioDePermisos(IDbContextFactory<PermisosDbContext> fabrica) : IServicioDePermisos
{
    public async Task<HashSet<string>> OperacionesDeAsync(Guid usuarioId, CancellationToken cancelacion = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancelacion);
        return (await db.Permisos
            .AsNoTracking()
            .Where(p => p.UsuarioId == usuarioId)
            .Select(p => p.Operacion)
            .ToListAsync(cancelacion))
            .ToHashSet(StringComparer.Ordinal);
    }

    public async Task OtorgarAsync(Guid usuarioId, string operacion, Guid solicitudId, DateTimeOffset momento, CancellationToken cancelacion = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancelacion);
        bool existe = await db.Permisos.AnyAsync(p => p.UsuarioId == usuarioId && p.Operacion == operacion, cancelacion);
        if (existe) return;

        db.Permisos.Add(new Permiso
        {
            UsuarioId = usuarioId,
            Operacion = operacion,
            OtorgadoEn = momento,
            SolicitudId = solicitudId
        });
        await db.SaveChangesAsync(cancelacion);
    }
}
