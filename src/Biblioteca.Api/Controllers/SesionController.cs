using System.Security.Claims;
using Biblioteca.Identidad;
using Biblioteca.Identidad.Acceso;
using Biblioteca.Identidad.Autorizacion;
using Biblioteca.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Biblioteca.Api.Controllers;

/// <summary>
/// Inicio y cierre de sesión, y consulta de la identidad propia
/// (RF-CA-03, RF-CA-07, RF-CA-18, RF-CA-19).
///
/// La credencial viaja en la cabecera <c>Authorization: Bearer</c>, que es la que ya
/// resuelve <c>AutenticacionPorSesion</c> en cada petición. No es un JWT: una
/// credencial guardada en el servidor es lo que permite revocarla (RF-CA-18) y
/// cerrarla en cascada cuando cambia la contraseña (RF-CA-12).
/// </summary>
[ApiController]
[Route("sesion")]
public sealed class SesionController(IServicioDeAcceso acceso) : ControllerBase
{
    /// <summary>
    /// Abre sesión y devuelve la credencial (RF-CA-03). Responde 401 ante cualquier
    /// credencial incorrecta, siempre con el mismo mensaje.
    /// </summary>
    [HttpPost("iniciar")]
    [AllowAnonymous]
    [RequiereOperacion(Operaciones.Autenticarse)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Iniciar(
        [FromBody] SolicitudInicioSesion peticion,
        CancellationToken cancelacion)
    {
        var credencial = await acceso.AutenticarAsync(
            peticion.Correo,
            peticion.Contrasena,
            cancelacion);

        // La credencial viaja en la respuesta Y en la cabecera, para que el cliente
        // pueda empezar aauthenticarse sin tener que montar la petición a mano.
        Response.Headers.Authorization = $"{AutenticacionPorSesion.Prefijo}{credencial.Token}";

        return Ok(new
        {
            token = credencial.Token,
            usuario = new
            {
                id = credencial.UsuarioId,
                nombre = credencial.Nombre,
                rol = credencial.Rol.ToString()
            },
            expiraEn = credencial.ExpiraEn
        });
    }

    /// <summary>
    /// Devuelve el usuario de la sesión vigente y su rol (RF-CA-07). Sin sesión
    /// válida, la política de repliego responde 401 antes de llegar aquí.
    /// </summary>
    [HttpGet("yo")]
    [RequiereOperacion(Operaciones.ConsultarYO)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Yo(CancellationToken cancelacion)
    {
        var usuario = await acceso.ConsultarYoAsync(UsuarioId(), cancelacion);

        return Ok(new
        {
            id = usuario.Id,
            nombre = usuario.Nombre,
            correo = usuario.Correo,
            rol = usuario.Rol.ToString(),
            activo = usuario.Activo
        });
    }

    /// <summary>
    /// Cierra la sesión de la petición (RF-CA-18). La credencial queda invalidada: al
    /// volver a usarla, <c>AutenticacionPorSesion</c> la resuelve como cerrada y
    /// responde 401.
    /// </summary>
    [HttpPost("cerrar")]
    [RequiereOperacion(Operaciones.CerrarSesion)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Cerrar(CancellationToken cancelacion)
    {
        await acceso.CerrarSesionAsync(TokenDeLaPeticion(), cancelacion);

        return NoContent();
    }

    private Guid UsuarioId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? throw new InvalidOperationException(
                       "La petición llegó autenticada sin ClaimTypes.NameIdentifier."));

    /// <summary>
    /// Extrae el token de la cabecera. Se relee de la petición en vez de sacarlo del
    /// principal porque el valor en claro no se guarda en ningún claims: en la base
    /// sólo está su hash, y es este valor el que hay que cerrar (RF-CA-18).
    /// </summary>
    private string TokenDeLaPeticion()
    {
        var cabecera = Request.Headers.Authorization.ToString();

        if (!cabecera.StartsWith(AutenticacionPorSesion.Prefijo, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "La petición está autenticada pero su cabecera Authorization no lleva el " +
                $"prefijo '{AutenticacionPorSesion.Prefijo}'.");
        }

        var token = cabecera[AutenticacionPorSesion.Prefijo.Length..].Trim();

        return token.Length > 0
            ? token
            : throw new InvalidOperationException("La cabecera Authorization no trae credencial.");
    }
}

/// <summary>Cuerpo de <c>POST /sesion/iniciar</c>.</summary>
public sealed record SolicitudInicioSesion(string Correo, string Contrasena);