using Biblioteca.Identidad;
using Biblioteca.Identidad.Autorizacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Biblioteca.Api.Seguridad;

/// <summary>
/// Aplica la política de la operación nombrada por la acción. La comprobación ocurre
/// SIEMPRE en el servidor (RD-06): si el rol no está entre los autorizados, la
/// petición se rechaza con 403 aunque el cliente se haya construido a mano.
///
/// Este componente es *glue* de ASP.NET, por eso vive en el host y no en la pieza de
/// dominio. La decisión —qué roles exige cada operación— no está aquí: se lee en
/// <see cref="PoliticaDeOperaciones"/>, dentro del Core (RF-CA-05).
/// </summary>
public sealed class RequisitoDeOperacionHandler : AuthorizationHandler<RequisitoDeOperacion>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext contexto,
        RequisitoDeOperacion requisito)
    {
        if (contexto.Resource is not HttpContext http)
        {
            // Sin contexto HTTP no hay operación que resolver: se rechaza.
            return Task.CompletedTask;
        }

        var operacion = http.GetEndpoint()?.Metadata
            .GetMetadata<RequiereOperacionAttribute>()?.Operacion;

        if (string.IsNullOrWhiteSpace(operacion))
        {
            // Acción sin operación declarada. La convención de arranque impide que
            // esto llegue a producción; si ocurre, se rechaza por si acaso.
            return Task.CompletedTask;
        }

        var roles = PoliticaDeOperaciones.RolesDe(operacion);

        if (roles.Any(rol => contexto.User.IsInRole(rol.ToString())))
        {
            contexto.Succeed(requisito);
        }

        // Si no se cumple, no se hace nada: el framework responde 403 Forbidden.
        return Task.CompletedTask;
    }
}