using System.Net.Mail;
using Biblioteca.Identidad.Entidades;
using Biblioteca.Identidad.Password;
using Biblioteca.Identidad.Persistencia;
using Biblioteca.Identidad.Sesiones;
using Biblioteca.Nucleo.Configuracion;
using Biblioteca.Nucleo.Errores;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Biblioteca.Identidad.Acceso;

/// <summary>
/// Credencial emitida al iniciar sesión. El <c>Token</c> es el valor en claro y es lo
/// único que viaja al cliente; en la base sólo queda su hash.
/// </summary>
public sealed record CredencialDeSesion(
    string Token,
    Guid UsuarioId,
    string Nombre,
    Rol Rol,
    DateTimeOffset ExpiraEn);

/// <summary>
/// Inicio y cierre de sesión, y consulta de la identidad propia
/// (RF-CA-03, RF-CA-07, RF-CA-18, RF-CA-19, y la parte de RF-CA-15 que rechaza
/// iniciar sesión con una cuenta sin activar).
///
/// Es una pieza distinta de <see cref="IServicioDeSesiones"/>: aquel da de baja el
/// ciclo de vida de una sesión ya emitida; éste decide si una pareja de credenciales
/// abre sesión, y para eso tiene que conocer la contraseña, el bloqueo y el estado de
/// la cuenta, que no son asuntos de la sesión.
/// </summary>
public interface IServicioDeAcceso
{
    /// <summary>
    /// Abre sesión si las credenciales son válidas y devuelve la credencial (RF-CA-03).
    /// </summary>
    /// <exception cref="ExcepcionDominio">
    /// <see cref="TipoError.NoAutenticado"/> si las credenciales no sirven. El mensaje
    /// es el mismo exista o no el correo, y el mismo tanto si falla el correo como si
    /// falla la contraseña: no se revela cuál de los dos datos falló.
    /// </exception>
    Task<CredencialDeSesion> AutenticarAsync(
        string correo,
        string contrasena,
        CancellationToken cancelacion = default);

    /// <summary>Devuelve el usuario de la credencial vigente, con su rol (RF-CA-07).</summary>
    Task<Usuario> ConsultarYoAsync(Guid usuarioId, CancellationToken cancelacion = default);

    /// <summary>Cierra la sesión del token y la deja de servir (RF-CA-18).</summary>
    Task CerrarSesionAsync(string token, CancellationToken cancelacion = default);
}

public sealed class ServicioDeAcceso(
    IDbContextFactory<IdentidadDbContext> fabrica,
    IPasswordHasher<Usuario> hasher,
    IServicioDeSesiones sesiones,
    TimeProvider reloj,
    IOptions<OpcionesCorreo> opciones) : IServicioDeAcceso
{
    private readonly OpcionesCorreo _opciones = opciones.Value;

    /// <summary>
    /// Hash de una contraseña inventada. Sirve para que el rechazo de un correo
    /// inexistente cueste lo mismo que el de una contraseña incorrecta (RF-CA-03):
    /// sin esto, el tiempo de respuesta delataría qué correos están registrados.
    /// </summary>
    private string? _hashSenuelo;

    public async Task<CredencialDeSesion> AutenticarAsync(
        string correo,
        string contrasena,
        CancellationToken cancelacion = default)
    {
        var canonico = CanonicalizarCorreo(correo);
        var ahora = reloj.GetUtcNow();

        await using var db = await fabrica.CreateDbContextAsync(cancelacion);

        var usuario = canonico is null
            ? null
            : await db.Usuarios.FirstOrDefaultAsync(u => u.Correo == canonico, cancelacion);

        if (usuario is null)
        {
            // Se verifica igualmente contra el hash señuelo para no delatar por
            // temporización si el correo existe.
            hasher.VerifyHashedPassword(new Usuario(), HashSenuelo(), contrasena);

            throw CredencialesInvalidas();
        }

        // El bloqueo se comprueba ANTES que la contraseña: una cuenta bloqueada no
        // sirve ni para acertar (RF-CA-19), ni para seguir probando.
        if (usuario.BloqueadoHasta is DateTimeOffset hasta)
        {
            if (hasta > ahora)
            {
                throw new ExcepcionDominio(
                    TipoError.NoAutenticado, "sesion.bloqueada",
                    $"Cuenta bloqueada por intentos fallidos. Inténtalo de nuevo a las " +
                    $"{hasta:HH:mm} (UTC).");
            }

            // El bloqueo expiró: la cuenta vuelve a empezar con cinco intentos limpios.
            // Sin esto, un solo fallo posterior volvería a bloquearla.
            usuario.BloqueadoHasta = null;
            usuario.IntentosFallidos = 0;
            await db.SaveChangesAsync(cancelacion);
        }

        if (hasher.VerifyHashedPassword(usuario, usuario.PasswordHash, contrasena)
            == PasswordVerificationResult.Failed)
        {
            await ContarFalloAsync(db, usuario, ahora, cancelacion);

            throw CredencialesInvalidas();
        }

        // La contraseña era correcta, así que la cuenta existe y el mensaje no la
        // delata. Se comprueba DESPUÉS de verificarla por eso (RF-CA-15).
        if (!usuario.Activo)
        {
            throw new ExcepcionDominio(
                TipoError.NoAutenticado, "sesion.cuenta_inactiva",
                "La cuenta no está activa. Abre el enlace de activación que te enviamos " +
                "por correo, o pide que te lo reenviemos.");
        }

        // RF-CA-19: sólo un inicio de sesión COMPLETO pone el contador en cero.
        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;
        await db.SaveChangesAsync(cancelacion);

        var token = await sesiones.EmitirAsync(usuario.Id, cancelacion);

        return new CredencialDeSesion(
            token,
            usuario.Id,
            usuario.Nombre,
            usuario.Rol,
            ahora.AddHours(_opciones.HorasValidezSesion));
    }

    public async Task<Usuario> ConsultarYoAsync(
        Guid usuarioId,
        CancellationToken cancelacion = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancelacion);

        return await db.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == usuarioId, cancelacion)
            ?? throw ExcepcionDominio.NoEncontrado("usuario.no_encontrado", "Usuario no encontrado.");
    }

    public Task CerrarSesionAsync(string token, CancellationToken cancelacion = default) =>
        sesiones.CerrarAsync(token, "Cierre voluntario.", cancelacion);

    /// <summary>Suma un intento fallido y bloquea la cuenta al llegar al límite.</summary>
    private async Task ContarFalloAsync(
        IdentidadDbContext db,
        Usuario usuario,
        DateTimeOffset ahora,
        CancellationToken cancelacion)
    {
        usuario.IntentosFallidos += 1;

        if (usuario.IntentosFallidos >= _opciones.IntentosMaximos)
        {
            usuario.BloqueadoHasta = ahora.AddMinutes(_opciones.MinutosBloqueo);
        }

        await db.SaveChangesAsync(cancelacion);
    }

    /// <summary>
    /// Devuelve el correo en la misma forma en que se guardó, o <see langword="null"/>
    /// si no es un correo válido.
    ///
    /// Aquí NO se lanza un error de validación, a diferencia del registro. En un
    /// inicio de sesión todo se rechaza con el mismo mensaje (RF-CA-03): un correo mal
    /// formado no puede coincidir con ninguna cuenta, así que responder "correo
    /// inválido" en vez de "credenciales incorrectas" no aportaría nada al usuario y sí
    /// revelaría qué formato espera el sistema.
    /// </summary>
    private static string? CanonicalizarCorreo(string correo) =>
        !string.IsNullOrWhiteSpace(correo)
        && MailAddress.TryCreate(correo.Trim(), out var parsed)
            ? parsed.Address.ToLowerInvariant()
            : null;

    /// <summary>
    /// El mensaje es idéntico para correo inexistente, contraseña incorrecta y correo
    /// mal formado (RF-CA-03).
    /// </summary>
    private static ExcepcionDominio CredencialesInvalidas() =>
        ExcepcionDominio.NoAutenticado("sesion.credenciales_invalidas", "Correo o contraseña incorrectos.");

    private string HashSenuelo() =>
        _hashSenuelo ??= hasher.HashPassword(new Usuario(), "contrasena-que-no-existe");
}