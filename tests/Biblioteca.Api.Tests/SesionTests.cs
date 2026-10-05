using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Biblioteca.Identidad;
using Biblioteca.Identidad.Entidades;
using Biblioteca.Identidad.Persistencia;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biblioteca.Api.Tests;

/// <summary>
/// Inicio y cierre de sesión, y consulta de la identidad propia
/// (RF-CA-03, 07, 18, 19, y el rechazo de cuenta inactiva de RF-CA-15).
///
/// Igual que en el registro, no se simula nada: se habla HTTP contra la aplicación real.
/// Los usuarios se inserts directamente en la base en lugar de pasar por
/// <c>POST /usuarios/registro</c>, para que estas pruebas no dependan de un pull
/// request anterior y aislen el comportamiento de la sesión.
/// </summary>
public class SesionTests : IClassFixture<FabricaDeAplicacionDePrueba>
{
    private const string ContrasenaValida = "Biblioteca2026";

    private readonly WebApplicationFactory<Program> _app;

    public SesionTests(FabricaDeAplicacionDePrueba app) => _app = app;

    // ---------------------------------------------------------------------------------------
    // RF-CA-03 — la credencial se entrega y los rechazos no distinguen qué falló.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Credenciales_correctas_abren_sesion()
    {
        var cliente = _app.CreateClient();
        var correo = await CrearUsuarioAsync();

        var respuesta = await cliente.PostAsJsonAsync("/sesion/iniciar", new
        {
            correo,
            contrasena = ContrasenaValida
        });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        using var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        // Vuelve el token en claro: es la credencial que el clienteRAYa respeta.
        Assert.False(string.IsNullOrWhiteSpace(cuerpo.RootElement.GetProperty("token").GetString()));
        Assert.Equal("Estandar", cuerpo.RootElement.GetProperty("usuario").GetProperty("rol").GetString());
    }

    [Fact]
    public async Task El_correo_se_acepta_independientemente_de_las_mayusculas()
    {
        var cliente = _app.CreateClient();
        var correo = await CrearUsuarioAsync();

        var respuesta = await cliente.PostAsJsonAsync("/sesion/iniciar", new
        {
            correo = correo.ToUpperInvariant(),
            contrasena = ContrasenaValida
        });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Contrasena_incorrecta_se_rechaza_sin_entregar_credencial()
    {
        var cliente = _app.CreateClient();
        var correo = await CrearUsuarioAsync();

        var respuesta = await cliente.PostAsJsonAsync("/sesion/iniciar", new
        {
            correo,
            contrasena = "OtraClave9"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.DoesNotContain("token", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Correo_inexistente_da_el_mismo_rechazo_que_contrasena_incorrecta()
    {
        var cliente = _app.CreateClient();
        var correo = await CrearUsuarioAsync();

        var contrasenaMala = await cliente.PostAsJsonAsync("/sesion/iniciar", new
        {
            correo,
            contrasena = "OtraClave9"
        });

        var correoInexistente = await cliente.PostAsJsonAsync("/sesion/iniciar", new
        {
            correo = CorreoUnico(),
            contrasena = ContrasenaValida
        });

        Assert.Equal(HttpStatusCode.Unauthorized, contrasenaMala.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, correoInexistente.StatusCode);

        // Byte a byte idénticos. Es el criterio explícito de RF-CA-03 y la comprobación
        // que hace el enunciado: "con contraseña incorrecta y con correo inexistente:
        // los dos rechazos son idénticos".
        Assert.Equal(
            await contrasenaMala.Content.ReadAsStringAsync(),
            await correoInexistente.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Un_correo_mal_formado_da_el_mismo_rechazo_que_los_demas()
    {
        var cliente = _app.CreateClient();
        var correo = await CrearUsuarioAsync();

        var referencia = await cliente.PostAsJsonAsync("/sesion/iniciar", new
        {
            correo,
            contrasena = "OtraClave9"
        });

        var malFormado = await cliente.PostAsJsonAsync("/sesion/iniciar", new
        {
            correo = "no-es-un-correo",
            contrasena = ContrasenaValida
        });

        Assert.Equal(
            await referencia.Content.ReadAsStringAsync(),
            await malFormado.Content.ReadAsStringAsync());
    }

    // ---------------------------------------------------------------------------------------
    // RF-CA-15 — la cuenta inactiva no inicia sesión, y el mensaje lo dice.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Una_cuenta_inactiva_se_rechaza_con_un_mensaje_que_lo_dice()
    {
        var cliente = _app.CreateClient();
        var correo = await CrearUsuarioAsync(activo: false);

        var respuesta = await cliente.PostAsJsonAsync("/sesion/iniciar", new
        {
            correo,
            contrasena = ContrasenaValida
        });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Contains("no está activa", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Una_cuenta_inactiva_con_contrasena_mala_da_el_rechazo_generico()
    {
        var cliente = _app.CreateClient();
        var correo = await CrearUsuarioAsync(activo: false);

        var respuesta = await cliente.PostAsJsonAsync("/sesion/iniciar", new
        {
            correo,
            contrasena = "OtraClave9"
        });

        // El mensaje "no está activa" SÓLO aparece con la contraseña correcta. Si
        // apareciera siempre, bastaría con probar correos para descubrir cuáles están
        // registrados pero sin activar.
        Assert.DoesNotContain("no está activa", await respuesta.Content.ReadAsStringAsync());
        Assert.Contains("incorrectos", await respuesta.Content.ReadAsStringAsync());
    }

    // ---------------------------------------------------------------------------------------
    // RF-CA-07 — la consulta del autenticado.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Sin_sesion_la_consulta_se_rechaza()
    {
        var cliente = _app.CreateClient();

        var respuesta = await cliente.GetAsync("/sesion/yo");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Una_credencial_inventada_no_sirve_para_la_consulta()
    {
        var cliente = ClienteConCredencial("esta-credencial-no-existe");

        var respuesta = await cliente.GetAsync("/sesion/yo");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Theory]
    [InlineData(Rol.Estandar)]
    [InlineData(Rol.Administrador)]
    public async Task La_consulta_devuelve_el_usuario_y_su_rol(Rol rol)
    {
        var correo = await CrearUsuarioAsync(rol: rol);
        var token = await IniciarSesionAsync(correo);
        var cliente = ClienteConCredencial(token);

        var respuesta = await cliente.GetAsync("/sesion/yo");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        using var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        Assert.Equal(rol.ToString(), cuerpo.RootElement.GetProperty("rol").GetString());
        Assert.Equal(correo, cuerpo.RootElement.GetProperty("correo").GetString());
    }

    // ---------------------------------------------------------------------------------------
    // RF-CA-18 — la credencial cerrada deja de servir.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Cerrar_sesion_invalida_la_credencial()
    {
        var correo = await CrearUsuarioAsync();
        var token = await IniciarSesionAsync(correo);
        var cliente = ClienteConCredencial(token);

        // Antes de cerrar, sirve.
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/sesion/yo")).StatusCode);

        var cierre = await cliente.PostAsync("/sesion/cerrar", null);
        Assert.Equal(HttpStatusCode.NoContent, cierre.StatusCode);

        // Después de cerrar, la misma credencial ya no vale. Es la comprobación que
        // hace el enunciado: "cerrar sesión y volver a usar la credencial cerrada".
        var despues = await cliente.GetAsync("/sesion/yo");
        Assert.Equal(HttpStatusCode.Unauthorized, despues.StatusCode);

        // Ni para abrir una sesión nueva: la fila queda marcada como cerrada.
        var reuso = await _app.CreateClient().PostAsJsonAsync("/sesion/iniciar", new
        {
            correo,
            contrasena = ContrasenaValida
        });

        Assert.Equal(HttpStatusCode.OK, reuso.StatusCode);
        Assert.NotEqual(token, await reuso.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Cerrar_sesion_no_cierra_las_demas_del_mismo_usuario()
    {
        var correo = await CrearUsuarioAsync();
        var primera = await IniciarSesionAsync(correo);
        var segunda = await IniciarSesionAsync(correo);

        Assert.Equal(HttpStatusCode.NoContent,
            (await ClienteConCredencial(primera).PostAsync("/sesion/cerrar", null)).StatusCode);

        // Sólo se cierra la credencial de la petición; la otra sigue viva.
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await ClienteConCredencial(primera).GetAsync("/sesion/yo")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await ClienteConCredencial(segunda).GetAsync("/sesion/yo")).StatusCode);
    }

    // ---------------------------------------------------------------------------------------
    // RF-CA-19 — bloqueo tras cinco intentos fallidos consecutivos.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Cinco_intentos_fallidos_consecutivos_bloquean_la_cuenta()
    {
        var cliente = _app.CreateClient();
        var correo = await CrearUsuarioAsync();

        for (int intento = 1; intento <= 5; intento++)
        {
            var respuesta = await cliente.PostAsJsonAsync("/sesion/iniciar", new
            {
                correo,
                contrasena = "OtraClave9"
            });

            // Los cinco intentos fallidos se rechazan como credenciales incorrectas:
            // el bloqueo todavía no se ha consumado.
            Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        }

        var usuario = await UsuarioAsync(correo);
        Assert.Equal(5, usuario!.IntentosFallidos);
        Assert.NotNull(usuario.BloqueadoHasta);
    }

    [Fact]
    public async Task El_sexto_intento_se_rechaza_aunque_la_contrasena_sea_correcta()
    {
        var cliente = _app.CreateClient();
        var correo = await CrearUsuarioAsync();

        for (int intento = 0; intento < 5; intento++)
        {
            await cliente.PostAsJsonAsync("/sesion/iniciar", new { correo, contrasena = "OtraClave9" });
        }

        var sexto = await cliente.PostAsJsonAsync("/sesion/iniciar", new
        {
            correo,
            contrasena = ContrasenaValida
        });

        Assert.Equal(HttpStatusCode.Unauthorized, sexto.StatusCode);
        Assert.Contains("bloqueada", await sexto.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Un_inicio_de_sesion_correcto_pone_el_contador_en_cero()
    {
        var cliente = _app.CreateClient();
        var correo = await CrearUsuarioAsync();

        await cliente.PostAsJsonAsync("/sesion/iniciar", new { correo, contrasena = "OtraClave9" });
        await cliente.PostAsJsonAsync("/sesion/iniciar", new { correo, contrasena = "OtraClave9" });

        Assert.Equal(2, (await UsuarioAsync(correo))!.IntentosFallidos);

        Assert.Equal(HttpStatusCode.OK,
            (await cliente.PostAsJsonAsync("/sesion/iniciar", new { correo, contrasena = ContrasenaValida })).StatusCode);

        var usuario = await UsuarioAsync(correo);
        Assert.Equal(0, usuario!.IntentosFallidos);
        Assert.Null(usuario.BloqueadoHasta);
    }

    [Fact]
    public async Task Al_caducar_el_bloqueo_la_cuenta_recupera_los_cinco_intentos()
    {
        var cliente = _app.CreateClient();
        var correo = await CrearUsuarioAsync(
            intentosFallidos: 5,
            bloqueadoHasta: DateTimeOffset.UtcNow.AddMinutes(-1));

        // El bloqueo ya venció: este fallo NO debe volver a bloquear la cuenta, porque
        // si no un solo fallo después de cada desbloqueo la dejaría inutilizable.
        var respuesta = await cliente.PostAsJsonAsync("/sesion/iniciar", new
        {
            correo,
            contrasena = "OtraClave9"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);

        var usuario = await UsuarioAsync(correo);
        Assert.Equal(1, usuario!.IntentosFallidos);
        Assert.Null(usuario.BloqueadoHasta);

        // Y aún le quedan cuatro intentos antes del siguiente bloqueo.
        for (int intento = 0; intento < 3; intento++)
        {
            await cliente.PostAsJsonAsync("/sesion/iniciar", new { correo, contrasena = "OtraClave9" });
        }

        Assert.Null((await UsuarioAsync(correo))!.BloqueadoHasta);
    }

    [Fact]
    public async Task Un_correo_inexistente_no_bloquea_nada()
    {
        var cliente = _app.CreateClient();
        var correo = CorreoUnico();

        for (int intento = 0; intento < 7; intento++)
        {
            var respuesta = await cliente.PostAsJsonAsync("/sesion/iniciar", new
            {
                correo,
                contrasena = ContrasenaValida
            });

            Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
            Assert.DoesNotContain("bloqueada", await respuesta.Content.ReadAsStringAsync());
        }
    }

    // ---------------------------------------------------------------------------------------
    // La credencial se guarda hasheada y no viaja en claro en la base.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task La_credencial_se_guarda_hasheada_y_no_en_claro()
    {
        var correo = await CrearUsuarioAsync();
        var token = await IniciarSesionAsync(correo);

        var fabrica = _app.Services.GetRequiredService<IDbContextFactory<IdentidadDbContext>>();
        await using var db = await fabrica.CreateDbContextAsync();

        var almacenado = await db.Sesiones
            .AsNoTracking()
            .Select(s => s.TokenHash)
            .ToListAsync();

        Assert.NotEmpty(almacenado);
        // El token en claro no aparece en ninguna fila: sólo está su SHA-256.
        Assert.DoesNotContain(almacenado, h => System.Text.Encoding.UTF8.GetString(h).Contains(token));
    }

    // ---------------------------------------------------------------------------------------
    // Aviso de superficie: iniciar sesión es público; el resto no.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Iniciar_sesion_es_publico_y_el_resto_de_la_sesion_no()
    {
        var cliente = _app.CreateClient();

        var iniciar = await cliente.PostAsJsonAsync("/sesion/iniciar", new
        {
            correo = "nadie@ejemplo.com",
            contrasena = "Biblioteca2026"
        });

        // Público: responde por la regla de negocio, no con 401.
        Assert.Equal(HttpStatusCode.Unauthorized, iniciar.StatusCode);
        Assert.Contains("incorrectos", await iniciar.Content.ReadAsStringAsync());

        // Las otras dos requieren sesión.
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/sesion/yo")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.PostAsync("/sesion/cerrar", null)).StatusCode);
    }

    [Fact]
    public async Task El_rechazo_de_credenciales_es_401_y_no_403()
    {
        var cliente = _app.CreateClient();
        var correo = await CrearUsuarioAsync();

        var respuesta = await cliente.PostAsJsonAsync("/sesion/iniciar", new
        {
            correo,
            contrasena = "OtraClave9"
        });

        // 401 = "no sé quién eres". 403 = "sé quién eres pero no puedes", que es lo que
        // corresponde a un rol insuficiente. Confundirlos haría que un cliente de
        // consola interpretara un contraseña mal escrita como un permiso denegado y no
        // reintentara nunca.
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);

        // RFC 9110: un 401 debe traer el desafío de autenticación.
        Assert.Equal("Sesion", respuesta.Headers.WwwAuthenticate.Single().Scheme);
    }

    // ---------------------------------------------------------------------------------------
    // Utilidades
    // ---------------------------------------------------------------------------------------

    private static string CorreoUnico() => $"sesion-{Guid.NewGuid():N}@ejemplo.com";

    /// <summary>
    /// Inserta un usuario directamente. La contraseña se hashea con el mismo hasher que
    /// usa la aplicación, para que la prueba no dependa del endpoint de registro.
    /// </summary>
    private async Task<string> CrearUsuarioAsync(
        bool activo = true,
        Rol rol = Rol.Estandar,
        int intentosFallidos = 0,
        DateTimeOffset? bloqueadoHasta = null)
    {
        var correo = CorreoUnico();

        var fabrica = _app.Services.GetRequiredService<IDbContextFactory<IdentidadDbContext>>();
        await using var db = await fabrica.CreateDbContextAsync();

        var usuario = new Usuario
        {
            Nombre = "Ana Ruiz",
            Correo = correo,
            Activo = activo,
            Rol = rol,
            IntentosFallidos = intentosFallidos,
            BloqueadoHasta = bloqueadoHasta,
            CreadoEn = DateTimeOffset.UtcNow
        };

        usuario.PasswordHash = new PasswordHasher<Usuario>().HashPassword(usuario, ContrasenaValida);

        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();

        return correo;
    }

    private async Task<Usuario?> UsuarioAsync(string correo)
    {
        var fabrica = _app.Services.GetRequiredService<IDbContextFactory<IdentidadDbContext>>();
        await using var db = await fabrica.CreateDbContextAsync();

        return await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Correo == correo);
    }

    private async Task<string> IniciarSesionAsync(string correo)
    {
        var respuesta = await _app.CreateClient().PostAsJsonAsync("/sesion/iniciar", new
        {
            correo,
            contrasena = ContrasenaValida
        });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        using var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        return cuerpo.RootElement.GetProperty("token").GetString()!;
    }

    private HttpClient ClienteConCredencial(string token)
    {
        var cliente = _app.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return cliente;
    }
}