using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Biblioteca.Api.Controllers;

/// <summary>
/// Comprobación de vida del servicio. Va marcada con <see cref="AllowAnonymousAttribute"/>,
/// así que la convención de RF-CA-05 la exceptúa: no expone una operación del sistema.
/// </summary>
[ApiController]
[Route("salud")]
public sealed class SaludController : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Get() => Ok(new
    {
        Estado = "ok",
        Hora = DateTimeOffset.UtcNow
    });
}