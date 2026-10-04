using System.Reflection;
using Biblioteca.Api.Seguridad;
using Biblioteca.Identidad;
using Biblioteca.Identidad.Autorizacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Biblioteca.Api.Tests;

/// <summary>
/// Controladores de mentira, usados SÓLO para comprobar el guard de RF-CA-05.
/// No se registran en la aplicación real: el guard debe rechazarlos antes de que
/// lleguen a ejecutarse.
/// </summary>
[ApiController]
[Route("__prueba")]
public sealed class ControladorSinOperacionDeclarada : ControllerBase
{
    [HttpGet("listar")]
    public IActionResult Listar() => Ok();               // sin [RequiereOperacion]: debe fallar
}

[ApiController]
[Route("__prueba")]
public sealed class ControladorConOperacionInvalida : ControllerBase
{
    [HttpGet("listar")]
    [RequiereOperacion("operacion.que.no.existe")]
    public IActionResult Listar() => Ok();               // operación no registrada: debe fallar
}

[ApiController]
[Route("__prueba")]
public sealed class ControladorCorrecto : ControllerBase
{
    [HttpGet("listar")]
    [RequiereOperacion(Operaciones.UsuariosListar)]
    [Authorize]
    public IActionResult Listar() => Ok();
}

[ApiController]
[Route("__prueba")]
public sealed class ControladorPublico : ControllerBase
{
    [HttpGet("salud")]
    [AllowAnonymous]
    public IActionResult Salud() => Ok();                // exento: debe pasar
}

/// <summary>
/// RF-CA-05 no puede ser una recomendación: una acción sin operación declarada tiene
/// que impedir que la aplicación arranque.
///
/// Estas pruebas construyen el modelo de acción y aplican la convención, que es
/// exactamente lo que ASP.NET Core hace al construir los controladores. No hace falta
/// levantar la API, de modo que el requisito es comprobable en milisegundos (RD-12).
/// </summary>
public class GuardDeOperacionesDeclaradasTests
{
    private static ActionModel ConstruirModeloDeAccion(MethodInfo metodo) =>
        new(metodo, metodo.GetCustomAttributes(inherit: true).Cast<object>().ToList());

    private static ActionModel ConstruirModeloDeAccion(Type controlador, string metodo)
    {
        var info = controlador.GetMethod(metodo)
            ?? throw new InvalidOperationException($"No se encontró {controlador.Name}.{metodo}.");

        return ConstruirModeloDeAccion(info);
    }

    [Fact]
    public void Una_accion_sin_operacion_declarada_impide_arrancar()
    {
        var modelo = ConstruirModeloDeAccion(
            typeof(ControladorSinOperacionDeclarada),
            nameof(ControladorSinOperacionDeclarada.Listar));

        var excepcion = Assert.Throws<InvalidOperationException>(
            () => new ValidarOperacionesDeclaradasConvention().Apply(modelo));

        Assert.Contains("RF-CA-05", excepcion.Message);
    }

    [Fact]
    public void Una_accion_con_operacion_inexistente_impide_arrancar()
    {
        var modelo = ConstruirModeloDeAccion(
            typeof(ControladorConOperacionInvalida),
            nameof(ControladorConOperacionInvalida.Listar));

        var excepcion = Assert.Throws<InvalidOperationException>(
            () => new ValidarOperacionesDeclaradasConvention().Apply(modelo));

        Assert.Contains("PoliticaDeOperaciones", excepcion.Message);
    }

    [Fact]
    public void Una_accion_correctamente_declarada_pasa_el_guard()
    {
        var modelo = ConstruirModeloDeAccion(
            typeof(ControladorCorrecto),
            nameof(ControladorCorrecto.Listar));

        new ValidarOperacionesDeclaradasConvention().Apply(modelo);   // no debe lanzar
    }

    [Fact]
    public void Una_accion_anonima_esta_exenta_del_guard()
    {
        var modelo = ConstruirModeloDeAccion(
            typeof(ControladorPublico),
            nameof(ControladorPublico.Salud));

        new ValidarOperacionesDeclaradasConvention().Apply(modelo);   // no debe lanzar
    }

    [Fact]
    public void El_guard_aplica_a_los_controladores_de_la_aplicacion_real()
    {
        // Comprobación de barrido: TODO controlador propio, salvo los exentos por
        // [AllowAnonymous], debe declarar su operación. Si alguien añade un endpoint
        // nuevo sin declararla, esta prueba falla antes de que la aplicación arranque.
        var propios = typeof(Program).Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsPublic: true }
                        && typeof(ControllerBase).IsAssignableFrom(t)
                        && t.Namespace == "Biblioteca.Api.Controllers")
            .ToList();

        Assert.NotEmpty(propios);

        var infracciones = new List<string>();

        foreach (var controlador in propios)
        {
            foreach (var metodo in controlador
                         .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                         .Where(m => m.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any()))
            {
                if (metodo.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
                {
                    continue;
                }

                try
                {
                    new ValidarOperacionesDeclaradasConvention()
                        .Apply(ConstruirModeloDeAccion(metodo));
                }
                catch (InvalidOperationException ex)
                {
                    infracciones.Add(ex.Message);
                }
            }
        }

        Assert.Empty(infracciones);
    }
}