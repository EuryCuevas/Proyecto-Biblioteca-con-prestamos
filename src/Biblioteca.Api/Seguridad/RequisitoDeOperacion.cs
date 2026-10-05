using Microsoft.AspNetCore.Authorization;

namespace Biblioteca.Api.Seguridad;

/// <summary>
/// Requisito de autorización que dispara <see cref="RequisitoDeOperacionHandler"/>.
///
/// No lleva información de rol propia, y tampoco de operación: el handler la busca en
/// la acción que se está ejecutando y la contrasta con
/// <see cref="Biblioteca.Identidad.PoliticaDeOperaciones"/>, que es el punto único
/// del Core donde vive la exigencia de rol de cada operación (RF-CA-05).
///
/// Va en la política de repliego, no en cada acción. Eso es lo que hace que el handler
/// se ejecute en TODAS las acciones que no son <c>[AllowAnonymous]</c>, sin que cada
/// controlador tenga que acordarse de pedirlo.
///
/// No se instancia en ningún otro sitio. Un requisito que nadie pide es un requisito
/// que el framework nunca evalúa, por muy bien escrito que esté el handler.
/// </summary>
public sealed class RequisitoDeOperacion : IAuthorizationRequirement
{
}