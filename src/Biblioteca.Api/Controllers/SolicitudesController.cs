using System.Security.Claims;
using Biblioteca.Identidad;
using Biblioteca.Identidad.Autorizacion;
using Biblioteca.Permisos.Solicitudes;
using Microsoft.AspNetCore.Mvc;

namespace Biblioteca.Api.Controllers;

[ApiController]
[Route("solicitudes")]
public sealed class SolicitudesController(IServicioDeSolicitudes solicitudes) : ControllerBase
{
    [HttpPost]
    [RequiereOperacion(Operaciones.SolicitudesCrear)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Crear([FromBody] SolicitudCrear peticion, CancellationToken cancelacion)
    {
        var solicitud = await solicitudes.CrearAsync(UsuarioActual(), peticion.Operacion, cancelacion);
        return Accepted(new
        {
            id = solicitud.Id,
            estado = solicitud.Estado.ToString(),
            solicitanteId = solicitud.SolicitanteId,
            operacion = solicitud.Operacion,
            creadaEn = solicitud.CreadaEn
        });
    }

    [HttpGet("pendientes")]
    [RequiereOperacion(Operaciones.SolicitudesListarPendientes)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListarPendientes(CancellationToken cancelacion)
    {
        var datos = await solicitudes.ListarPendientesAsync(cancelacion);
        return Ok(datos.Select(s => new
        {
            id = s.Id,
            solicitanteId = s.SolicitanteId,
            operacion = s.Operacion,
            estado = s.Estado.ToString(),
            creadaEn = s.CreadaEn,
            aprobadaEn = s.AprobadaEn,
            aplicadaEn = s.AplicadaEn,
            rechazadaEn = s.RechazadaEn,
            motivoRechazo = s.MotivoRechazo
        }));
    }

    [HttpGet("mias")]
    [RequiereOperacion(Operaciones.SolicitudesListarMias)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListarMias(CancellationToken cancelacion)
    {
        var datos = await solicitudes.ListarMiasAsync(UsuarioActual(), cancelacion);
        return Ok(datos.Select(s => new
        {
            id = s.Id,
            solicitanteId = s.SolicitanteId,
            operacion = s.Operacion,
            estado = s.Estado.ToString(),
            creadaEn = s.CreadaEn,
            aprobadaEn = s.AprobadaEn,
            aplicadaEn = s.AplicadaEn,
            rechazadaEn = s.RechazadaEn,
            motivoRechazo = s.MotivoRechazo
        }));
    }

    [HttpPost("aprobar")]
    [RequiereOperacion(Operaciones.SolicitudesAprobar)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Aprobar([FromBody] SolicitudAprobar peticion, CancellationToken cancelacion)
    {
        var s = await solicitudes.AprobarAsync(peticion.SolicitudId, UsuarioActual(), cancelacion);
        return Ok(new
        {
            id = s.Id,
            estado = s.Estado.ToString(),
            aprobadaPorId = s.AprobadaPorId,
            aprobadaEn = s.AprobadaEn,
            aplicadaEn = s.AplicadaEn,
            operacion = s.Operacion,
            solicitanteId = s.SolicitanteId
        });
    }

    [HttpPost("rechazar")]
    [RequiereOperacion(Operaciones.SolicitudesRechazar)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Rechazar([FromBody] SolicitudRechazar peticion, CancellationToken cancelacion)
    {
        var s = await solicitudes.RechazarAsync(peticion.SolicitudId, UsuarioActual(), peticion.Motivo, cancelacion);
        return Ok(new
        {
            id = s.Id,
            estado = s.Estado.ToString(),
            rechazadaPorId = s.RechazadaPorId,
            rechazadaEn = s.RechazadaEn,
            motivoRechazo = s.MotivoRechazo,
            operacion = s.Operacion,
            solicitanteId = s.SolicitanteId
        });
    }

    private Guid UsuarioActual() => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
}

public sealed record SolicitudCrear(string Operacion);
public sealed record SolicitudAprobar(Guid SolicitudId);
public sealed record SolicitudRechazar(Guid SolicitudId, string Motivo);
