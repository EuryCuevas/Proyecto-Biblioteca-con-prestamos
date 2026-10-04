using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Biblioteca.Api.Tests;

/// <summary>
/// Ajustes de la configuración durante las pruebas de integración.
///
/// La cadena de conexión se lee de la variable de entorno <c>ConnectionStrings__Biblioteca</c>,
/// igual que en producción (RD-10). Si no está definida, se usa una de desarrollo que
/// apunta a la instancia local y así la prueba de arranque sigue siendoútil:
/// </summary>
public class FabricaDeAplicacionDePrueba : WebApplicationFactory<Program>
{
    private const string ConexionDeDesarrollo =
        "Server=localhost;Database=Biblioteca;Trusted_Connection=True;TrustServerCertificate=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Biblioteca",
            Environment.GetEnvironmentVariable("ConnectionStrings__Biblioteca")
            ?? ConexionDeDesarrollo);

        // El emisor de correo queda registrado pero NO se usa SMTP en estas pruebas:
        // RF-NOT-08 exige que la aplicación funcione con el servidor de correo apagado.
        builder.UseSetting("Correo:Host", "");
        builder.UseEnvironment("Testing");
    }
}