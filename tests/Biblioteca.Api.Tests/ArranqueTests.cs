using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Biblioteca.Api.Tests;

/// <summary>
/// Comprueba que la aplicación arranca de verdad: lee la configuración, abre la
/// conexión, aplica las migraciones y responde.
///
/// Es la prueba que detecta un cableado roto en <c>Program.cs</c>, que es donde se
/// concentran los errores que no se ven en las pruebas unitarias.
/// </summary>
public class ArranqueTests : IClassFixture<FabricaDeAplicacionDePrueba>
{
    private readonly WebApplicationFactory<Program> _app;

    public ArranqueTests(FabricaDeAplicacionDePrueba app) => _app = app;

    [Fact]
    public async Task La_aplicacion_arranca_responde_y_no_requiere_smtp()
    {
        // RF-NOT-08 / RF-NOT-13: la aplicación debe levantar con el SMTP apagado o sin
        // configurar. Sólo se define la cadena de conexión: ninguna variable de correo.
        var cliente = _app.CreateClient();

        var respuesta = await cliente.GetAsync("/salud");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_mensaje_de_salud_no_filtra_nada_del_servidor()
    {
        var cliente = _app.CreateClient();

        var cuerpo = await cliente.GetStringAsync("/salud");

        Assert.Contains("ok", cuerpo);
        // RD-08: ni ruta de archivo, ni versión de SQL Server, ni cadena de conexión.
        Assert.DoesNotContain("Server=", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\", cuerpo, StringComparison.OrdinalIgnoreCase);
    }
}