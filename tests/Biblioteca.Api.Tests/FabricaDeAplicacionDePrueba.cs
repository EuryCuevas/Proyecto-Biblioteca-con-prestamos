using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Biblioteca.Api.Tests;

/// <summary>
/// Ajustes de la configuración durante las pruebas de integración.
///
/// La cadena de conexión se lee de la variable de entorno <c>ConnectionStrings__Biblioteca</c>,
/// igual que en producción (RD-10). Si no está definida, se usa una de desarrollo que
/// apunta a la instancia local y así la prueba de arranque sigue siendoútil:
///
/// La base pública de los enlaces del correo también se fija aquí: sin ella la aplicación
/// no arranca (RD-10), y con ella el enlace deja de depender del host de la petición.
/// </summary>
public class FabricaDeAplicacionDePrueba : WebApplicationFactory<Program>
{
    /// <summary>
    /// Base pública de los enlaces del correo en las pruebas (RD-10). Es virtual para que
    /// una prueba pueda arrancar la aplicación con otra base y comprobar de dónde sale el
    /// enlace. El valor por defecto coincide con el host del cliente de pruebas, de modo
    /// que el enlace no depende de quién haga la petición.
    /// </summary>
    protected virtual string UrlBasePublica => "http://localhost";

    private const string ConexionDeDesarrollo =
        "Server=localhost;Database=Biblioteca;Trusted_Connection=True;TrustServerCertificate=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Correo:UrlBasePublica", UrlBasePublica);

        builder.UseSetting("ConnectionStrings:Biblioteca",
            Environment.GetEnvironmentVariable("ConnectionStrings__Biblioteca")
            ?? ConexionDeDesarrollo);

        // El emisor de correo queda registrado pero NO se usa SMTP en estas pruebas:
        // RF-NOT-08 exige que la aplicación funcione con el servidor de correo apagado.
        builder.UseSetting("Correo:Host", "");
        builder.UseEnvironment("Testing");
    }
}