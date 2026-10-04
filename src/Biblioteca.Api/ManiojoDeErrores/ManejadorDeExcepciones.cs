using Biblioteca.Nucleo.Errores;
using Microsoft.AspNetCore.Mvc;

namespace Biblioteca.Api.ManiojoDeErrores;

/// <summary>
/// Traduce los rechazos controlados de la capa de dominio a respuestas HTTP.
///
/// Es la barrera de RD-08: sólo sale a la vista el el <c>Message</c> de la excepción,
/// que está escrito para el usuario. El tipo de excepción, la traza de pila, las rutas
/// de archivo y las consultas SQL se registran en el log del servidor y nunca en el
/// cuerpo de la respuesta.
///
/// La lógica de negocio NO vive aquí: este componente sólo traduce. Si el mensaje
/// necesita cambiar, se cambia el dominio, no el middleware (RD-02).
/// </summary>
public sealed class ManejadorDeExcepciones(
    RequestDelegate siguiente,
    ILogger<ManejadorDeExcepciones> logger)
{
    public async Task InvokeAsync(HttpContext contexto)
    {
        try
        {
            await siguiente(contexto);
        }
        catch (ExcepcionDominio excepcion)
        {
            await EscribirAsync(contexto, excepcion);
        }
        catch (BadHttpRequestException excepcion)
        {
            // Cuerpo mal formado o tipo de contenido inesperado (RD-07).
            logger.LogWarning(excepcion, "Petición mal formada en {Ruta}", contexto.Request.Path);

            await EscribirAsync(contexto, new ExcepcionDominio(
                TipoError.Validacion, "peticion.invalida",
                "La petición no es válida."));
        }
    }

    private static async Task EscribirAsync(HttpContext contexto, ExcepcionDominio excepcion)
    {
        var estado = excepcion.Tipo switch
        {
            TipoError.Validacion => StatusCodes.Status400BadRequest,
            TipoError.NoAutorizado => StatusCodes.Status403Forbidden,
            TipoError.NoEncontrado => StatusCodes.Status404NotFound,
            TipoError.Conflicto => StatusCodes.Status409Conflict,
            TipoError.ReglaDeNegocio => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status500InternalServerError
        };

        if (estado >= StatusCodes.Status500InternalServerError)
        {
            // Nunca se responde con el detalle de un fallo interno.
            return;
        }

        if (contexto.Response.HasStarted)
        {
            return;
        }

        contexto.Response.Clear();
        contexto.Response.StatusCode = estado;
        contexto.Response.ContentType = "application/problem+json";

        await contexto.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Title = excepcion.Message,
            Detail = null,
            Status = estado,
            Extensions = { ["codigo"] = excepcion.Codigo }
        });
    }
}