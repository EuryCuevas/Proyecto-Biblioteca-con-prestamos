using System.Security.Claims;
using Biblioteca.Identidad;
using Biblioteca.Identidad.Autorizacion;
using Biblioteca.Identidad.Contrasenas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Biblioteca.Api.Controllers;

/// <summary>
/// Recuperación de contraseña, restablecimiento con código y cambio de la contraseña
/// propia (RF-CA-09, 10, 11, 12 y 22).
///
/// Las tres primeras acciones son <c>[AllowAnonymous]</c>, igual que el alta de cuentas:
/// quien Todavía no recuperó su contraseña no puede autenticarse, y una operación a la
/// que llega alguien sin identidad no puede exigir un rol. No es una excepción a
/// RF-CA-05, es su caso honesto. El cambio de la contraseña propia sí exige sesión.
/// </summary>
[ApiController]
[Route("contrasenas")]
public sealed class ContrasenasController(IServicioDeContrasenas contrasenas) : ControllerBase
{
    /// <summary>
    /// Pide un código de recuperación (RF-CA-09, RF-CA-10). Responde 202 SIEMPRE, exista
    /// o no ese correo: el cuerpo de la respuesta es idéntico en los dos casos, y es lo
    /// que el enunciado pide comprobar.
    /// </summary>
    [HttpPost("recuperacion")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> IniciarRecuperacion(
        [FromBody] SolicitudRecuperacion peticion,
        CancellationToken cancelacion)
    {
        await contrasenas.IniciarRecuperacionAsync(peticion.Correo, UrlBase(), cancelacion);

        return Accepted(new
        {
            mensaje = "Si el correo está registrado y la cuenta está activa, " +
                      "te enviamos un código para recuperar la contraseña."
        });
    }

    /// <summary>
    /// Comprueba un código SIN consumirlo. Es lo que responde al enlace del correo:
    /// GET porque el enlace llega por correo y tiene que poder pulsarse tal cual.
    /// Un código usado o vencido responde 422 y el usuario debe pedir otro (RF-CA-10).
    /// </summary>
    [HttpGet("recuperacion")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ComprobarCodigo(
        [FromQuery] string? codigo,
        CancellationToken cancelacion)
    {
        await contrasenas.ComprobarCodigoAsync(codigo ?? string.Empty, cancelacion);

        return Ok(new
        {
            mensaje = "El código sirve. Define la contraseña nueva con " +
                      "POST /contrasenas/restablecer, enviando este código y la contraseña."
        });
    }

    /// <summary>
    /// Define la contraseña nueva con un código de recuperación (RF-CA-10, RF-CA-11).
    /// No hace falta sesión: el código es la prueba. Cierra las sesiones que el usuario
    /// tuviera abiertas (RF-CA-12), así que la respuesta lo dice.
    /// </summary>
    [HttpPost("restablecer")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Restablecer(
        [FromBody] SolicitudRestablecimiento peticion,
        CancellationToken cancelacion)
    {
        await contrasenas.RestablecerConCodigoAsync(
            peticion.Codigo,
            peticion.ContrasenaNueva,
            cancelacion);

        return Ok(new
        {
            mensaje = "Contraseña restablecida. Tus sesiones abiertas se han cerrado: " +
                      "ya puedes iniciar sesión con la nueva."
        });
    }

    /// <summary>
    /// Cambia la contraseña de quien hace la petición, indicando la actual (RF-CA-22).
    /// Exige sesión y declara su operación, de modo que la cubre el guard de arranque y
    /// el punto único de rol (RF-CA-05).
    /// </summary>
    /// <remarks>
    /// RF-CA-12 cierra también la sesión que hizo esta petición. Por eso la respuesta
    /// lo dice en voz alta: si no, el cliente se enteraría en la siguiente llamada con
    /// un 401 que no sabría explicar.
    /// </remarks>
    [HttpPost("propia")]
    [RequiereOperacion(Operaciones.CambiarPasswordPropia)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CambiarPropia(
        [FromBody] SolicitudCambioPropio peticion,
        CancellationToken cancelacion)
    {
        await contrasenas.CambiarPropiaAsync(
            UsuarioId(),
            peticion.ContrasenaActual,
            peticion.ContrasenaNueva,
            cancelacion);

        return Ok(new
        {
            mensaje = "Contraseña cambiada. Se han cerrado todas tus sesiones, " +
                      "incluida esta: inicia sesión otra vez."
        });
    }

    private Guid UsuarioId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? throw new InvalidOperationException(
                       "La petición llegó autenticada sin ClaimTypes.NameIdentifier."));

    /// <summary>
    /// Base pública donde vive el enlace del correo. La decide el host porque es quien
    /// conoce el esquema, el host y el puerto reales de esta petición.
    /// </summary>
    private string UrlBase() => $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
}

/// <summary>Cuerpo de <c>POST /contrasenas/recuperacion</c>.</summary>
public sealed record SolicitudRecuperacion(string Correo);

/// <summary>Cuerpo de <c>POST /contrasenas/restablecer</c>.</summary>
public sealed record SolicitudRestablecimiento(string Codigo, string ContrasenaNueva);

/// <summary>Cuerpo de <c>POST /contrasenas/propia</c>.</summary>
public sealed record SolicitudCambioPropio(string ContrasenaActual, string ContrasenaNueva);