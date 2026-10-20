using System.Security.Claims;
using Biblioteca.Identidad;
using Biblioteca.Identidad.Autorizacion;
using Biblioteca.Permisos.Solicitudes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Biblioteca.Api.Seguridad;

/// <summary>
/// Aplica la política de la operación nombrada por la acción. La comprobación ocurre
/// SIEMPRE en el servidor (RD-06): si el rol no está entre los autorizados, o no hay
/// permiso otorgado, la petición se rechaza con 403 aunque el cliente se haya construido a mano.
///
/// Este componente es *glue* de ASP.NET, por eso vive en el host y no en la pieza de
/// dominio. La decisión —qué roles exige cada operación— no está aquí: se lee en
/// <see cref="PoliticaDeOperaciones"/>, dentro del Core (RF-CA-05).
///
/// Falla cerrado: si la acción no declara operación, o la política no la conoce, no
/// se concede el requisito y el framework responde 403. Nunca «si no sé, dejo pasar».
/// Para RF-GP-05 se consultan permisos otorgados en vivo.
/// </summary>
public sealed class RequisitoDeOperacionHandler : AuthorizationHandler<RequisitoDeOperacion>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext contexto,
        RequisitoDeOperacion requisito)
    {
        if (contexto.Resource is not HttpContext http)
        {
            // Sin contexto HTTP no hay operación que resolver: se rechaza.
            return;
        }

        var operacion = http.GetEndpoint()?.Metadata
            .GetMetadata<RequiereOperacionAttribute>()?.Operacion;

        if (string.IsNullOrWhiteSpace(operacion))
        {
            // Acción sin operación declarada. La convención de arranque impide que
            // esto llegue a producción; si ocurre, se rechaza por si acaso.
            return;
        }

        var roles = PoliticaDeOperaciones.RolesDe(operacion);

        if (roles.Count == 0)
        {
            // Operación declarada en el atributo pero ausente de la política: se
            // rechaza. Declarar la operación y exigirle roles son la misma cosa, y si
            // la segunda falla la primera no sirve de nada.
            return;
        }

        if (roles.Any(rol => contexto.User.IsInRole(rol.ToString())))
        {
            contexto.Succeed(requisito);
            return;
        }

        var idClaim = contexto.User.FindFirst(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(idClaim?.Value, out var usuarioId))
        {
            var permisos = http.RequestServices.GetService<IServicioDePermisos>();
            if (permisos is not null)
            {
                var otorgados = await permisos.OperacionesDeAsync(usuarioId);
                if (otorgados.Contains(operacion))
                {
                    contexto.Succeed(requisito);
                    return;
                }
            }
        }

        // Si no se cumple, no se hace nada: el framework responde 403 Forbidden.
    }
}