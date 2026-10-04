using System.Security.Claims;
using System.Text.Encodings.Web;
using Biblioteca.Identidad.Sesiones;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Biblioteca.Api.Seguridad;

/// <summary>Nombre del esquema de autenticación por credencial de sesión.</summary>
public static class EsquemaAutenticacion
{
    public const string Nombre = "Sesion";
}

/// <summary>
/// Autentica por credencial de sesión opaca guardada en la base.
///
/// No se usa JWT: un token autocontenido no se puede revocar, y RF-CA-12, RF-CA-18 y
/// RF-CA-20 exigen revocación. Aquí el token es una cadena opaca que se resuelve
/// contra la tabla Sesión en cada petición.
///
/// Se revalida también <c>Usuario.Activo</c> en cada petición, de modo que desactivar
/// a un usuario invalida sus sesiones abiertas aunque nadie las cierre (RF-CA-20).
/// </summary>
public sealed class AutenticacionPorSesion(
    IOptionsMonitor<AuthenticationSchemeOptions> opciones,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IServicioDeSesiones sesiones)
    : AuthenticationHandler<AuthenticationSchemeOptions>(opciones, loggerFactory, encoder)
{
    public const string Encabezado = "Authorization";
    public const string Prefijo = "Bearer ";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Encabezado, out var cabecera))
        {
            return AuthenticateResult.NoResult();
        }

        var valor = cabecera.ToString();
        if (!valor.StartsWith(Prefijo, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = valor[Prefijo.Length..].Trim();
        if (token.Length == 0)
        {
            return AuthenticateResult.NoResult();
        }

        var sesion = await sesiones.ResolverAsync(token, Context.RequestAborted);
        if (sesion is null)
        {
            // Credencial cerrada, vencida o inexistente: se rechaza igual que si fuera
            // inválida, sin revelar por qué (RF-CA-03).
            return AuthenticateResult.Fail("Credencial de sesión no válida.");
        }

        var identidad = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, sesion.UsuarioId.ToString()),
            new Claim(ClaimTypes.Name, sesion.Usuario.Nombre),
            new Claim(ClaimTypes.Role, sesion.Usuario.Rol.ToString())
        ], EsquemaAutenticacion.Nombre);

        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identidad), EsquemaAutenticacion.Nombre));
    }
}