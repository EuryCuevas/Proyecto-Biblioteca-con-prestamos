using System.Net;
using System.Net.Http.Json;
using Biblioteca.Correo.Persistencia;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biblioteca.Api.Tests;

/// <summary>
/// La base pública de los enlaces del correo sale de la configuración
/// (<c>Correo__UrlBasePublica</c>), no del esquema ni del host de la petición.
///
/// El motivo: detrás de un proxy inverso el esquema y el host que ve el proceso son los
/// internos, y el enlace del correo tiene que funcionar fuera de la petición que lo
/// originó. El cliente de pruebas habla contra <c>http://localhost</c>; estas pruebas
/// arrancan la aplicación con OTRA base, de modo que un enlace deducido de la petición no
/// superaría las aserciones.
/// </summary>
public class BasePublicaDelEnlaceTests
{
    private const string ContrasenaValida = "Biblioteca2026";
    private const string BaseConfigurada = "https://biblioteca.ejemplo.test";

    [Fact]
    public async Task El_enlace_de_activacion_usa_la_base_configurada()
    {
        using var app = new FabricaConBasePublica(BaseConfigurada);
        var cliente = app.CreateClient();
        var correo = CorreoUnico();

        var respuesta = await cliente.PostAsJsonAsync("/usuarios/registro", new
        {
            nombre = "Ana Ruiz",
            correo,
            contrasena = ContrasenaValida
        });

        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);

        var cuerpo = await CuerpoEncoladoAsync(app, correo);

        Assert.Contains($"{BaseConfigurada}/usuarios/activar?token=", cuerpo);
        Assert.DoesNotContain("http://localhost", cuerpo);
    }

    [Fact]
    public async Task El_enlace_de_recuperacion_usa_la_base_configurada()
    {
        using var app = new FabricaConBasePublica(BaseConfigurada);
        var cliente = app.CreateClient();
        var correo = await RegistrarYActivarAsync(app, cliente);

        var respuesta = await cliente.PostAsJsonAsync(
            "/contrasenas/recuperacion", new { correo });

        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);

        var cuerpo = await CuerpoEncoladoAsync(app, correo);

        Assert.Contains($"{BaseConfigurada}/contrasenas/recuperacion?codigo=", cuerpo);
        Assert.DoesNotContain("http://localhost", cuerpo);
    }

    [Fact]
    public void Sin_base_publica_la_aplicacion_no_arranca()
    {
        using var app = new FabricaConBasePublica(string.Empty);

        var excepcion = Assert.ThrowsAny<Exception>(() => app.CreateClient());

        // El mensaje nombra la variable que falta, que es donde se arregla (RD-10).
        Assert.Contains("Correo__UrlBasePublica", excepcion.ToString());
    }

    private static string CorreoUnico() => $"base-publica-{Guid.NewGuid():N}@ejemplo.com";

    private static async Task<string> RegistrarYActivarAsync(
        WebApplicationFactory<Program> app, HttpClient cliente)
    {
        var correo = CorreoUnico();

        await cliente.PostAsJsonAsync("/usuarios/registro", new
        {
            nombre = "Ana Ruiz",
            correo,
            contrasena = ContrasenaValida
        });

        var cuerpo = await CuerpoEncoladoAsync(app, correo);
        var token = ValorTras(cuerpo, "token=");

        var activacion = await cliente.GetAsync($"/usuarios/activar?token={token}");
        Assert.Equal(HttpStatusCode.OK, activacion.StatusCode);

        return correo;
    }

    private static async Task<string> CuerpoEncoladoAsync(
        WebApplicationFactory<Program> app, string correo)
    {
        var fabrica = app.Services.GetRequiredService<IDbContextFactory<CorreoDbContext>>();
        await using var db = await fabrica.CreateDbContextAsync();

        var cuerpo = await db.CorreosEnCola
            .AsNoTracking()
            .Where(c => c.Destinatario == correo)
            .OrderByDescending(c => c.CreadoEn)
            .Select(c => c.Cuerpo)
            .FirstOrDefaultAsync();

        Assert.NotNull(cuerpo);
        return cuerpo!;
    }

    private static string ValorTras(string cuerpo, string marca)
    {
        var inicio = cuerpo.IndexOf(marca, StringComparison.Ordinal) + marca.Length;
        var fin = cuerpo.IndexOfAny(['\r', '\n', ' ', '&'], inicio);

        return Uri.UnescapeDataString(cuerpo[inicio..(fin < 0 ? cuerpo.Length : fin)]);
    }

    /// <summary>Arranca la aplicación con otra base pública, para ver de dónde sale el enlace.</summary>
    private sealed class FabricaConBasePublica(string urlBase) : FabricaDeAplicacionDePrueba
    {
        protected override string UrlBasePublica => urlBase;
    }
}
