using Biblioteca.Identidad.Autorizacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Biblioteca.Api.Seguridad;

/// <summary>
/// Convención MVC que hace inevitable cumplir RF-CA-05.
///
/// Se ejecuta al construir el modelo de controladores, es decir, al arrancar la
/// aplicación. Si alguna acción no declara
/// <see cref="RequiereOperacionAttribute"/>, o declara una operación inexistente, la
/// excepción impide que la API llegue a escuchar. No es una convención de "buenas
/// prácticas": es un guard de ejecución.
///
/// Se exceptúan las acciones marcadas con <see cref="AllowAnonymousAttribute"/>, que
/// por definición no ejecutan una operación del sistema (salud, login, recuperación
/// de contraseña).
///
/// La decisión —qué roles exige cada operación— no está aquí: se lee en
/// <see cref="Biblioteca.Identidad.PoliticaDeOperaciones"/>, dentro de la pieza de
/// dominio. Esta clase sólo se encarga de que toda acción quede declarada.
/// </summary>
public sealed class ValidarOperacionesDeclaradasConvention : IActionModelConvention
{
    public void Apply(ActionModel accion)
    {
        if (accion.Attributes.Any(a => a is AllowAnonymousAttribute))
        {
            return;
        }

        var metodo = accion.ActionMethod;
        var nombre = $"{metodo.DeclaringType?.Name}.{metodo.Name}";

        var declarada = accion.Attributes
            .OfType<RequiereOperacionAttribute>()
            .Select(a => a.Operacion)
            .SingleOrDefault();

        CoherenciaDeOperaciones.Validar(nombre, declarada);
    }
}