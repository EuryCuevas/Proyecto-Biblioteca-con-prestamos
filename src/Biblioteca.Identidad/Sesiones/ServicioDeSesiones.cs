using Biblioteca.Identidad;
using Biblioteca.Identidad.Entidades;
using Biblioteca.Identidad.Password;
using Biblioteca.Identidad.Persistencia;
using Biblioteca.Nucleo.Configuracion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Biblioteca.Identidad.Sesiones;

/// <summary>
/// Servicio de credenciales de sesiÃ³n. Es la pieza 1 quien decide si una credencial
/// es vÃ¡lida; el host sÃ³lo traduce HTTP (RD-02).
///
/// Las sesiones se guardan en la base, no en un token autocontenido. Eso es lo que
/// permite revocar credenciales ya emitidas, que exigen RF-CA-12 (cambio de
/// contraseÃ±a), RF-CA-18 (cierre de sesiÃ³n) y RF-CA-20 (desactivaciÃ³n del usuario).
/// </summary>
public interface IServicioDeSesiones
{
    /// <summary>Crea una sesiÃ³n y devuelve el token en claro, que es lo que viaja al cliente.</summary>
    Task<string> EmitirAsync(Guid usuarioId, CancellationToken cancelacion = default);

    /// <summary>Resuelve un token en claro. Devuelve null si no hay sesiÃ³n abierta vigente.</summary>
    Task<Sesion?> ResolverAsync(string token, CancellationToken cancelacion = default);

    /// <summary>Cierra la sesiÃ³n del token (RF-CA-18).</summary>
    Task CerrarAsync(string token, string motivo, CancellationToken cancelacion = default);

    /// <summary>
    /// Cierra todas las sesiones abiertas de un usuario. Es el mecanismo de RF-CA-12,
    /// y se reutiliza para RF-CA-20. Se invoca dentro de la misma transacciÃ³n del cambio.
    /// </summary>
    /// <param name="contexto">
    /// Contexto ya abierto por la operaciÃ³n que llama. Si se entrega, el servicio lo
    /// reutiliza para que el cierre de sesiones y el cambio de contraseÃ±a sean una sola
    /// transacciÃ³n. Si es <see langword="null"/>, el servicio abre el suyo.
    /// </param>
    Task<int> CerrarTodasAsync(
        Guid usuarioId,
        string motivo,
        IdentidadDbContext? contexto = null,
        CancellationToken cancelacion = default);
}

public sealed class ServicioDeSesiones(
    IDbContextFactory<IdentidadDbContext> fabrica,
    TimeProvider reloj,
    IOptions<OpcionesCorreo> opciones) : IServicioDeSesiones
{
    private readonly OpcionesCorreo _opciones = opciones.Value;

    public async Task<string> EmitirAsync(Guid usuarioId, CancellationToken cancelacion = default)
    {
        var token = GeneradorDeSecretos.NuevoToken();
        var ahora = reloj.GetUtcNow();

        await using var db = await fabrica.CreateDbContextAsync(cancelacion);

        db.Sesiones.Add(new Sesion
        {
            UsuarioId = usuarioId,
            TokenHash = GeneradorDeSecretos.Hash(token),
            CreadaEn = ahora,
            ExpiraEn = ahora.AddHours(_opciones.HorasValidezSesion)
        });

        await db.SaveChangesAsync(cancelacion);
        return token;
    }

    public async Task<Sesion?> ResolverAsync(string token, CancellationToken cancelacion = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var hash = GeneradorDeSecretos.Hash(token);
        var ahora = reloj.GetUtcNow();

        await using var db = await fabrica.CreateDbContextAsync(cancelacion);

        return await db.Sesiones
            .AsNoTracking()
            .Include(s => s.Usuario)
            .Where(s => s.TokenHash == hash && s.CerradaEn == null && s.ExpiraEn > ahora)
            .FirstOrDefaultAsync(cancelacion);
    }

    public async Task CerrarAsync(string token, string motivo, CancellationToken cancelacion = default)
    {
        var hash = GeneradorDeSecretos.Hash(token);

        await using var db = await fabrica.CreateDbContextAsync(cancelacion);

        await db.Sesiones
            .Where(s => s.TokenHash == hash && s.CerradaEn == null)
            .ExecuteUpdateAsync(c => c
                .SetProperty(s => s.CerradaEn, reloj.GetUtcNow())
                .SetProperty(s => s.MotivoCierre, motivo), cancelacion);
    }

    public async Task<int> CerrarTodasAsync(
        Guid usuarioId,
        string motivo,
        IdentidadDbContext? contexto = null,
        CancellationToken cancelacion = default)
    {
        var db = contexto ?? await fabrica.CreateDbContextAsync(cancelacion);
        var propio = contexto is null;

        try
        {
            return await db.Sesiones
                .Where(s => s.UsuarioId == usuarioId && s.CerradaEn == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.CerradaEn, reloj.GetUtcNow())
                    .SetProperty(x => x.MotivoCierre, motivo), cancelacion);
        }
        finally
        {
            if (propio)
            {
                await db.DisposeAsync();
            }
        }
    }
}