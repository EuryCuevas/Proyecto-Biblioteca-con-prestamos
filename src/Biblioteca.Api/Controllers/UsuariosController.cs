using Biblioteca.Identidad.Registro;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Biblioteca.Api.Controllers;

/// <summary>
/// Alta de cuentas y activación por enlace.
///
/// Las tres acciones son <c>[AllowAnonymous]</c>: todavía no hay usuario ni sesión. No es
/// una excepción a RF-CA-05, es su caso honesto — una operación a la que llega alguien sin
/// identidad no puede exigir un rol. La convención
/// <c>ValidarOperacionesDeclaradasConvention</c> exceptúa justamente las acciones
/// anónimas, y el resto del sistema sigue declarando su operación.
/// </summary>
[ApiController]
[Route("usuarios")]
public sealed class UsuariosController(IServicioDeRegistro registro) : ControllerBase
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
    /// Base pública donde vive el enlace del correo. La decide el host porque es quien
    /// conoce el esquema, el host y el puerto reales de esta petición.
    /// </summary>
    private string UrlBase() => $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
}

/// <summary>Cuerpo de <c>POST /usuarios/registro</c>.</summary>
public sealed record SolicitudRegistro(string Nombre, string Correo, string Contrasena);

/// <summary>Cuerpo de <c>POST /usuarios/reenviar-activacion</c>.</summary>
public sealed record SolicitudReenvio(string Correo);