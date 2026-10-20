using Biblioteca.Nucleo.Errores;
using Biblioteca.Permisos.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Biblioteca.Permisos.Solicitudes;

public interface IServicioDeSolicitudes
{
    Task<SolicitudPermiso> CrearAsync(Guid solicitanteId, string operacion, CancellationToken cancelacion = default);
    Task<IReadOnlyList<SolicitudPermiso>> ListarPendientesAsync(CancellationToken cancelacion = default);
    Task<IReadOnlyList<SolicitudPermiso>> ListarMiasAsync(Guid solicitanteId, CancellationToken cancelacion = default);
    Task<SolicitudPermiso> AprobarAsync(Guid solicitudId, Guid adminId, CancellationToken cancelacion = default);
    Task<SolicitudPermiso> RechazarAsync(Guid solicitudId, Guid adminId, string motivo, CancellationToken cancelacion = default);
}

public sealed class ServicioDeSolicitudes(
    IDbContextFactory<PermisosDbContext> fabrica,
    IServicioDePermisos permisos,
    TimeProvider reloj) : IServicioDeSolicitudes
{
    public async Task<SolicitudPermiso> CrearAsync(Guid solicitanteId, string operacion, CancellationToken cancelacion = default)
    {
        if (string.IsNullOrWhiteSpace(operacion))
            throw ExcepcionDominio.Validacion("solicitud.operacionRequerida", "La operación es obligatoria.");

        await using var db = await fabrica.CreateDbContextAsync(cancelacion);
        var solicitud = new SolicitudPermiso
        {
            SolicitanteId = solicitanteId,
            Operacion = operacion.Trim(),
            Estado = EstadoSolicitud.Pendiente,
            CreadaEn = reloj.GetUtcNow()
        };
        db.SolicitudesPermiso.Add(solicitud);
        await db.SaveChangesAsync(cancelacion);
        return solicitud;
    }

    public async Task<IReadOnlyList<SolicitudPermiso>> ListarPendientesAsync(CancellationToken cancelacion = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancelacion);
        return await db.SolicitudesPermiso
            .AsNoTracking()
            .Where(s => s.Estado == EstadoSolicitud.Pendiente)
            .OrderBy(s => s.CreadaEn)
            .ToListAsync(cancelacion);
    }

    public async Task<IReadOnlyList<SolicitudPermiso>> ListarMiasAsync(Guid solicitanteId, CancellationToken cancelacion = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancelacion);
        return await db.SolicitudesPermiso
            .AsNoTracking()
            .Where(s => s.SolicitanteId == solicitanteId)
            .OrderByDescending(s => s.CreadaEn)
            .ToListAsync(cancelacion);
    }

    public async Task<SolicitudPermiso> AprobarAsync(Guid solicitudId, Guid adminId, CancellationToken cancelacion = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancelacion);
        var solicitud = await db.SolicitudesPermiso.FirstOrDefaultAsync(s => s.Id == solicitudId, cancelacion);
        if (solicitud is null)
            throw ExcepcionDominio.NoEncontrado("solicitud.noEncontrada", "La solicitud no existe.");

        var ahora = reloj.GetUtcNow();
        solicitud.Aprobar(adminId, ahora);
        solicitud.Aplicar(ahora);
        await permisos.OtorgarAsync(solicitud.SolicitanteId, solicitud.Operacion, solicitud.Id, ahora, cancelacion);
        await db.SaveChangesAsync(cancelacion);
        return solicitud;
    }

    public async Task<SolicitudPermiso> RechazarAsync(Guid solicitudId, Guid adminId, string motivo, CancellationToken cancelacion = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancelacion);
        var solicitud = await db.SolicitudesPermiso.FirstOrDefaultAsync(s => s.Id == solicitudId, cancelacion);
        if (solicitud is null)
            throw ExcepcionDominio.NoEncontrado("solicitud.noEncontrada", "La solicitud no existe.");

        var ahora = reloj.GetUtcNow();
        solicitud.Rechazar(adminId, motivo, ahora);
        await db.SaveChangesAsync(cancelacion);
        return solicitud;
    }
}
