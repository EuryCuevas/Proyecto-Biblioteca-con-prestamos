using System.Net;
using System.Net.Http.Json;
using Biblioteca.Correo.Persistencia;
using Biblioteca.Identidad;
using Biblioteca.Identidad.Entidades;
using Biblioteca.Identidad.Password;
using Biblioteca.Identidad.Persistencia;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biblioteca.Api.Tests;

/// <summary>
/// Pruebas de extremo a extremo del registro y la activación (RF-CA-01, 02, 14, 15, 16, 17).
///
/// No son pruebas de lógica aislada: hablan HTTP contra la aplicación real y comprueban
/// lo que queda en la base y en la cola de correo. Es lo que exige el enunciado
/// ("me registro con mi propio correo y abro el enlace"), y por eso se prueba por
/// fuera y no por dentro.
/// </summary>
public class RegistroYActivacionTests : IClassFixture<FabricaDeAplicacionDePrueba>
{
    private const string ContrasenaValida = "Biblioteca2026";

    private readonly WebApplicationFactory<Program> _app;

    public RegistroYActivacionTests(FabricaDeAplicacionDePrueba app) => _app = app;

    // ---------------------------------------------------------------------------------------
    // RF-CA-15 — la cuenta nace inactiva.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Registrar_devuelve_202_y_deja_la_cuenta_inactiva()
    {
        var cliente = _app.CreateClient();
        var correo = CorreoUnico();

        var respuesta = await cliente.PostAsJsonAsync("/usuarios/registro", new
        {
            nombre = "Ana Ruiz",
            correo,
            contrasena = ContrasenaValida
        });

        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);

        var usuario = await UsuarioAsync(correo);

        Assert.NotNull(usuario);
        // Nadie puede iniciar sesión hasta abrir el enlace, aunquiera que conozca la
        // contraseña.
        Assert.False(usuario!.Activo);
        Assert.Equal("Ana Ruiz", usuario.Nombre);
        // Un usuario registrado por sí mismo no es Administrador (RF-CA-04).
        Assert.Equal(Rol.Estandar, usuario.Rol);
    }

    // ---------------------------------------------------------------------------------------
    // RF-CA-01 — el correo es único.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Un_segundo_registro_con_el_mismo_correo_se_rechaza()
    {
        var cliente = _app.CreateClient();
        var correo = CorreoUnico();

        var primera = await cliente.PostAsJsonAsync("/usuarios/registro", new
        {
            nombre = "Ana Ruiz",
            correo,
            contrasena = ContrasenaValida
        });

        var segunda = await cliente.PostAsJsonAsync("/usuarios/registro", new
        {
            nombre = "Otra persona",
            correo,
            contrasena = ContrasenaValida
        });

        Assert.Equal(HttpStatusCode.Accepted, primera.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);

        // Y el nombre del primero no se ha pisado.
        Assert.Equal("Ana Ruiz", (await UsuarioAsync(correo))!.Nombre);
    }

    [Fact]
    public async Task El_correo_se_compara_sin_distinguir_mayusculas()
    {
        var cliente = _app.CreateClient();
        var correo = CorreoUnico();

        await cliente.PostAsJsonAsync("/usuarios/registro", new
        {
            nombre = "Ana Ruiz",
            correo,
            contrasena = ContrasenaValida
        });

        var segunda = await cliente.PostAsJsonAsync("/usuarios/registro", new
        {
            nombre = "Otra persona",
            correo = correo.ToUpperInvariant(),
            contrasena = ContrasenaValida
        });

        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
    }

    // ---------------------------------------------------------------------------------------
    // RF-CA-02 — la contraseña no se guarda en claro y lleva sal propia.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task La_contrasena_se_guarda_hasheada_con_sal_propia()
    {
        var cliente = _app.CreateClient();
        var correoA = CorreoUnico();
        var correoB = CorreoUnico();

        foreach (var correo in new[] { correoA, correoB })
        {
            var respuesta = await cliente.PostAsJsonAsync("/usuarios/registro", new
            {
                nombre = "Ana Ruiz",
                correo,
                contrasena = ContrasenaValida
            });

            Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);
        }

        var usuarioA = (await UsuarioAsync(correoA))!;
        var usuarioB = (await UsuarioAsync(correoB))!;

        // Lo guardado no es la contraseña ni sus trozos.
        Assert.NotEqual(ContrasenaValida, usuarioA.PasswordHash);
        Assert.DoesNotContain(ContrasenaValida, usuarioA.PasswordHash);

        // Misma contraseña, dos usuarios, dos hashes: eso es una sal por usuario.
        Assert.NotEqual(usuarioA.PasswordHash, usuarioB.PasswordHash);

        // Y el hash sirve para comprobar la contraseña original.
        var hasher = new PasswordHasher<Usuario>();
        Assert.Equal(PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(usuarioA, usuarioA.PasswordHash, ContrasenaValida));
        Assert.Equal(PasswordVerificationResult.Failed,
            hasher.VerifyHashedPassword(usuarioA, usuarioA.PasswordHash, "OtraClave9"));
    }

    // ---------------------------------------------------------------------------------------
    // RF-CA-14 — política de contraseña.
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData("corta1", "8 caracteres")]      // demasiado corta
    [InlineData("solosletras", "letras y números")]
    [InlineData("12345678", "letras y números")]
    public async Task Una_contrasena_que_no_cumple_la_politica_se_rechaza(string contrasena, string motivo)
    {
        var cliente = _app.CreateClient();
        var correo = CorreoUnico();

        var respuesta = await cliente.PostAsJsonAsync("/usuarios/registro", new
        {
            nombre = "Ana Ruiz",
            correo,
            contrasena
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.Contains(motivo, cuerpo);

        // Rechazo limpio: no queda un usuario a medias ni se encola correo.
        Assert.Null(await UsuarioAsync(correo));
    }

    // ---------------------------------------------------------------------------------------
    // RD-07 — entrada mal formada.
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData("no-es-un-correo")]
    [InlineData("")]
    public async Task Un_correo_mal_formado_se_rechaza(string correo)
    {
        var cliente = _app.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync("/usuarios/registro", new
        {
            nombre = "Ana Ruiz",
            correo,
            contrasena = ContrasenaValida
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("correo", await respuesta.Content.ReadAsStringAsync());
    }

    // ---------------------------------------------------------------------------------------
    // RF-NOT-08 — el registro encola el correo; no abre SMTP.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task El_registro_encola_el_correo_con_el_enlace()
    {
        var cliente = _app.CreateClient();
        var correo = CorreoUnico();

        await cliente.PostAsJsonAsync("/usuarios/registro", new
        {
            nombre = "Ana Ruiz",
            correo,
            contrasena = ContrasenaValida
        });

        var cuerpo = await UltimoCuerpoEncoladoAsync(correo);

        Assert.NotNull(cuerpo);
        Assert.Contains("/usuarios/activar?token=", cuerpo);
        Assert.Contains("Ana Ruiz", cuerpo);

        // El enlace lleva el servidor y el esquema de la petición que lo pidió: por eso
        // el correo sirve en cualquier entorno sin configurar una URL a mano.
        Assert.Contains("http://localhost/usuarios/activar", cuerpo);
    }

    // ---------------------------------------------------------------------------------------
    // RF-CA-16 — el enlace activa la cuenta y sólo sirve una vez.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Abrir_el_enlace_recibido_activa_la_cuenta()
    {
        var cliente = _app.CreateClient();
        var correo = await RegistrarAsync(cliente);

        Assert.False((await UsuarioAsync(correo))!.Activo);

        var respuesta = await cliente.GetAsync($"/usuarios/activar?token={await TokenEnviadoAsync(correo)}");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True((await UsuarioAsync(correo))!.Activo);
    }

    [Fact]
    public async Task El_enlace_no_sirve_una_segunda_vez_y_el_estado_no_cambia()
    {
        var cliente = _app.CreateClient();
        var correo = await RegistrarAsync(cliente);

        var enlace = $"/usuarios/activar?token={await TokenEnviadoAsync(correo)}";

        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync(enlace)).StatusCode);

        // Segundo uso del mismo enlace: se rechaza.
        var segundo = await cliente.GetAsync(enlace);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, segundo.StatusCode);

        // El estado no cambia respecto al primer uso: sigue activa, no "desactivada"
        // ni con el token reescrito.
        var usuario = (await UsuarioAsync(correo))!;
        Assert.True(usuario.Activo);

        var tokens = await TokensAsync(correo);
        Assert.All(tokens, t => Assert.NotNull(t.UsadoEn));
    }

    [Fact]
    public async Task Un_enlace_que_no_existe_se_rechaza_sin_revelar_nada()
    {
        var cliente = _app.CreateClient();

        var respuesta = await cliente.GetAsync("/usuarios/activar?token=inventado-inventado");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);

        // El cuerpo dice exactamente lo mismo ante otro token inventado: no permite
        // distinguir "nunca existió" de otro caso.
        var otro = await cliente.GetAsync("/usuarios/activar?token=otro-inventado");

        Assert.Equal(
            await otro.Content.ReadAsStringAsync(),
            await respuesta.Content.ReadAsStringAsync());
    }

    // ---------------------------------------------------------------------------------------
    // RF-CA-17 — el reenvío no revela qué correos existen.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Reenviar_responde_lo_mismo_exista_o_no_el_correo()
    {
        var cliente = _app.CreateClient();
        var correo = await RegistrarAsync(cliente);

        var existente = await cliente.PostAsJsonAsync("/usuarios/reenviar-activacion", new { correo });
        var inexistente = await cliente.PostAsJsonAsync("/usuarios/reenviar-activacion",
            new { correo = CorreoUnico() });

        Assert.Equal(HttpStatusCode.Accepted, existente.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, inexistente.StatusCode);

        // Respuesta byte a byte idéntica: no se puede usar para enumerar usuarios.
        Assert.Equal(await existente.Content.ReadAsStringAsync(),
                     await inexistente.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task El_reenvio_invalida_el_enlace_anterior()
    {
        var cliente = _app.CreateClient();
        var correo = await RegistrarAsync(cliente);
        var enlaceOriginal = $"/usuarios/activar?token={await TokenEnviadoAsync(correo)}";

        await cliente.PostAsJsonAsync("/usuarios/reenviar-activacion", new { correo });

        // El enlace viejo ya no sirve.
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await cliente.GetAsync(enlaceOriginal)).StatusCode);

        // El nuevo sí, y activa la cuenta.
        Assert.Equal(HttpStatusCode.OK,
            (await cliente.GetAsync($"/usuarios/activar?token={await TokenEnviadoAsync(correo)}")).StatusCode);

        Assert.True((await UsuarioAsync(correo))!.Activo);
    }

    [Fact]
    public async Task Reenviar_a_una_cuenta_ya_activada_no_encola_nada()
    {
        var cliente = _app.CreateClient();
        var correo = await RegistrarAsync(cliente);

        await cliente.GetAsync($"/usuarios/activar?token={await TokenEnviadoAsync(correo)}");

        var antes = await ConteoCorreosAsync(correo);

        await cliente.PostAsJsonAsync("/usuarios/reenviar-activacion", new { correo });

        // La cuenta ya está activa: no hay nada que reenviar.
        Assert.Equal(antes, await ConteoCorreosAsync(correo));
    }

    // ---------------------------------------------------------------------------------------
    // Aviso de superficie anónima: las tres acciones públicas son las de registro.
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData("/usuarios/activar?token=x", "GET")]
    [InlineData("/usuarios/registro", "POST")]
    [InlineData("/usuarios/reenviar-activacion", "POST")]
    public async Task Las_acciones_de_registro_son_publicas(string ruta, string metodo)
    {
        var cliente = _app.CreateClient();

        using var peticion = new HttpRequestMessage(new HttpMethod(metodo), ruta);

        var respuesta = await cliente.SendAsync(peticion);

        // Ni 401 ni 403: son públicas a propósito, porque todavía no hay identidad.
        Assert.NotEqual(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    // ---------------------------------------------------------------------------------------
    // Utilidades
    // ---------------------------------------------------------------------------------------

    /// <summary>Correo único por prueba, para que las pruebas no dependan del orden.</summary>
    private static string CorreoUnico() => $"prueba-{Guid.NewGuid():N}@ejemplo.com";

    private async Task<string> RegistrarAsync(HttpClient cliente)
    {
        var correo = CorreoUnico();

        var respuesta = await cliente.PostAsJsonAsync("/usuarios/registro", new
        {
            nombre = "Ana Ruiz",
            correo,
            contrasena = ContrasenaValida
        });

        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);

        return correo;
    }

    private async Task<Usuario?> UsuarioAsync(string correo)
    {
        var fabrica = _app.Services.GetRequiredService<IDbContextFactory<IdentidadDbContext>>();
        await using var db = await fabrica.CreateDbContextAsync();

        return await db.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Correo == correo);
    }

    private async Task<List<TokenActivacion>> TokensAsync(string correo)
    {
        var fabrica = _app.Services.GetRequiredService<IDbContextFactory<IdentidadDbContext>>();
        await using var db = await fabrica.CreateDbContextAsync();

        var id = await db.Usuarios
            .Where(u => u.Correo == correo)
            .Select(u => u.Id)
            .FirstOrDefaultAsync();

        return await db.TokensActivacion
            .AsNoTracking()
            .Where(t => t.UsuarioId == id)
            .ToListAsync();
    }

    private async Task<string?> UltimoCuerpoEncoladoAsync(string correo)
    {
        var fabrica = _app.Services.GetRequiredService<IDbContextFactory<CorreoDbContext>>();
        await using var db = await fabrica.CreateDbContextAsync();

        return await db.CorreosEnCola
            .AsNoTracking()
            .Where(c => c.Destinatario == correo)
            .OrderByDescending(c => c.CreadoEn)
            .Select(c => c.Cuerpo)
            .FirstOrDefaultAsync();
    }

    private async Task<int> ConteoCorreosAsync(string correo)
    {
        var fabrica = _app.Services.GetRequiredService<IDbContextFactory<CorreoDbContext>>();
        await using var db = await fabrica.CreateDbContextAsync();

        return await db.CorreosEnCola
            .AsNoTracking()
            .CountAsync(c => c.Destinatario == correo);
    }

    /// <summary>
    /// Extrae el token del último correo encolado, igual que haría quien abre el enlace
    /// desde su bandeja de entrada.
    /// </summary>
    private async Task<string> TokenEnviadoAsync(string correo)
    {
        var cuerpo = await UltimoCuerpoEncoladoAsync(correo);

        Assert.NotNull(cuerpo);

        const string marca = "token=";
        var inicio = cuerpo!.IndexOf(marca, StringComparison.Ordinal) + marca.Length;
        var fin = cuerpo.IndexOfAny(['\r', '\n', ' ', '&'], inicio);

        return Uri.UnescapeDataString(cuerpo[inicio..(fin < 0 ? cuerpo.Length : fin)]);
    }
}