using Biblioteca.Identidad;
using Biblioteca.Identidad.Administracion;
using Biblioteca.Identidad.Autorizacion;
using Biblioteca.Identidad.Contrasenas;
using Biblioteca.Identidad.Registro;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Biblioteca.Api.Controllers;

/// <summary>
/// Alta de cuentas y activación por enlace, y la administración de usuarios: listado,
/// cambio de rol, desactivación, reactivación y restablecimiento forzado de contraseña.
/// </summary>
/// <remarks>
/// Las tres primeras acciones son <c>[AllowAnonymous]</c>: todavía no hay usuario ni sesión. No es
/// una excepción a RF-CA-05, es su caso honesto — una operación a la que llega alguien sin
/// identidad no puede exigir un rol. La convención
/// <c>ValidarOperacionesDeclaradasConvention</c> exceptúa justamente las acciones
/// anónimas, y el resto del sistema sigue declarando su operación.
///
/// A partir de <c>GET /usuarios</c>, todas las acciones declaran una operación que
/// <c>PoliticaDeOperaciones</c> reserva al Administrador, y todas se evalúan en cada
/// petición (RF-CA-06, RD-06).
/// </remarks>
[ApiController]
[Route("usuarios")]
public sealed class UsuariosController(
    IServicioDeRegistro registro,
    IServicioDeContrasenas contrasenas,
    IServicioDeAdministracion administracion) : ControllerBase
{
    /// <summary>
    /// Registra una cuenta (RF-CA-01, RF-CA-02, RF-CA-14, RF-CA-15).
    /// Responde 202: la cuenta existe pero está inactiva hasta abrir el enlace.
    /// </summary>
    [HttpPost("registro")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Registrar(
        [FromBody] SolicitudRegistro peticion,
        CancellationToken cancelacion)
    {
        await registro.RegistrarAsync(
            peticion.Nombre,
            peticion.Correo,
            peticion.Contrasena,
            UrlBase(),
            cancelacion);

        return Accepted(new
        {
            mensaje = "Cuenta registrada. Revisa tu correo para activarla."
        });
    }

    /// <summary>
    /// Abre el enlace de activación (RF-CA-16). Es GET porque el enlace llega por correo
    /// y tiene que poder pulsarse tal cual.
    /// </summary>
    [HttpGet("activar")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Activar(
        [FromQuery] string? token,
        CancellationToken cancelacion)
    {
        await registro.ActivarAsync(token ?? string.Empty, cancelacion);

        return Ok(new { mensaje = "Cuenta activada. Ya puedes iniciar sesión." });
    }

    /// <summary>
    /// Reenvía el enlace de activación (RF-CA-17). Responde 202 siempre, exista o no el
    /// correo: la respuesta no revela qué correos están registrados.
    /// </summary>
    [HttpPost("reenviar-activacion")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReenviarActivacion(
        [FromBody] SolicitudReenvio peticion,
        CancellationToken cancelacion)
    {
        await registro.ReenviarActivacionAsync(peticion.Correo, UrlBase(), cancelacion);

        return Accepted(new
        {
            mensaje = "Si el correo está registrado y la cuenta está inactiva, " +
                      "te enviamos un enlace nuevo."
        });
    }

    /// <summary>
    /// Restablece la contraseña de un usuario desde la administración (RF-CA-13).
    /// La contraseña anterior deja de servir en el acto, sus sesiones abiertas se
    /// cierran y se encola un código nuevo al correo registrado.
    /// Responde 202: el restablecimiento ya está hecho, y lo que sigue es que el
    /// usuario abra su correo.
    /// </summary>
    /// <remarks>
    /// El usuario al que se restablece va en el cuerpo, no en la ruta, para que las
    /// cuatro rutas de este controlador se parezca entre sí: el repositorio no tiene
    /// ningún segmento variable en el Routing.
    ///
    /// La operación es <c>UsuariosForzarRestablecimiento</c>, que
    /// <c>PoliticaDeOperaciones</c> reserva al Administrador (RF-CA-05). Un Estándar
    /// recibe 403, y también si construye la petición a mano sin pasar por la
    /// interfaz (RD-06).
    /// </remarks>
    [HttpPost("restablecer-contrasena")]
    [RequiereOperacion(Operaciones.UsuariosForzarRestablecimiento)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RestablecerContrasena(
        [FromBody] SolicitudRestablecimientoAdmin peticion,
        CancellationToken cancelacion)
    {
        await contrasenas.ForzarRestablecimientoAsync(peticion.UsuarioId, UrlBase(), cancelacion);

        return Accepted(new
        {
            mensaje = "Contraseña restablecida y sesiones cerradas. Se ha encolado un " +
                      "código de recuperación al correo registrado."
        });
    }

    /// <summary>
    /// Lista los usuarios con su rol y su estado (RF-CA-21).
    ///
    /// Nunca incluye hashes ni tokens: la proyección es a
    /// <see cref="ResumenDeUsuario"/>, que no tiene ningún campo de ese tipo, y la
    /// proyección ocurre en SQL, de modo que <c>PasswordHash</c> ni siquiera llega a
    /// salir de la base.
    /// </summary>
    /// <remarks>
    /// La operación es <c>UsuariosListar</c>, que <c>PoliticaDeOperaciones</c> reserva al
    /// Administrador (RF-CA-05). Un Estándar recibe 403, y también si construye la
    /// petición a mano sin pasar por la interfaz (RF-CA-06, RD-06).
    /// </remarks>
    [HttpGet]
    [RequiereOperacion(Operaciones.UsuariosListar)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Listar(CancellationToken cancelacion)
    {
        var usuarios = await administracion.ListarAsync(cancelacion);

        return Ok(usuarios);
    }

    /// <summary>
    /// Base pública donde vive el enlace del correo. La decide el host porque es quien
    /// conoce el esquema, el host y el puerto reales de esta petición.
    /// </summary>
    private string UrlBase() => $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
}

/// <summary>Cuerpo de <c>POST /usuarios/registro</c>.</summary>
public sealed record SolicitudRegistro(string Nombre, string Correo, string Contrasena);

/// <summary>Cuerpo de <c>POST /usuarios/reenviar-activacion</c>.</summary>
public sealed record SolicitudReenvio(string Correo);

/// <summary>Cuerpo de <c>POST /usuarios/restablecer-contrasena</c>.</summary>
public sealed record SolicitudRestablecimientoAdmin(Guid UsuarioId);