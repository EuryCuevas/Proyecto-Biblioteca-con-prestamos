using Microsoft.AspNetCore.Authorization;

namespace Biblioteca.Api.Seguridad;

/// <summary>
/// Requisito de autorización resuelto a partir de la operación declarada por la
/// acción. No lleva información de rol propia: la busca en
/// <see cref="Biblioteca.Identidad.PoliticaDeOperaciones"/>, que es el punto único
/// del Core donde vive la exigencia de rol de cada operación (RF-CA-05).
/// </summary>
public sealed class RequisitoDeOperacion : IAuthorizationRequirement
{
    public RequisitoDeOperacion(string operacion) => Operacion = operacion;

    public string Operacion { get; }
}