using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Biblioteca.Correo.Entidades;
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
/// Recuperación de contraseña, restablecimiento con código, restablecimiento forzado
/// por Administrador y cambio de la contraseña propia
/// (RF-CA-09, 10, 11, 12, 13 y 22).
///
/// Igual que en el registro y en la sesión, no se simula nada: se habla HTTP contra la
/// aplicación real y se mira la base de datos de verdad. El correo se comprueba en la
/// COLA, no en un buzón falso, porque RF-CA-10 exige precisamente que el correo salga
/// por la cola y no dentro de la operación que lo origina (RF-NOT-08). El servidor SMTP
/// está apagado en estas pruebas, que es justo la condición en la que la operación de
/// negocio tiene que terminar bien.
///
/// Los usuarios se insertan directamente en la base en lugar de pasar por
/// <c>POST /usuarios/registro</c>, para que estas pruebas no dependan de un pull
/// request anterior.
/// </summary>
public class RecuperacionDeContrasenaTests : IClassFixture<FabricaDeAplicacionDePrueba>
{
    private const string ContrasenaValida = "Biblioteca2026";
    private const string ContrasenaNueva = "NuevaClave2026";

    private readonly WebApplicationFactory<Program> _app;

    public RecuperacionDeContrasenaTests(FabricaDeAplicacionDePrueba app) => _app = app;

    // ---------------------------------------------------------------------------------------
    // RF-CA-09 — la respuesta no revela qué correos están registrados.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task La_respuesta_es_identica_exista_o_no_el_correo()
    {
        var cliente = _app.CreateClient();
        var correo = (await CrearUsuarioAsync()).Correo;

        var conCorreo = await cliente.PostAsJsonAsync("/contrasenas/recuperacion", new { correo });
        var sinCorreo = await cliente.PostAsJsonAsync("/contrasenas/recuperacion", new
        {
            correo = CorreoUnico("ausente")
        });

        Assert.Equal(HttpStatusCode.Accepted, conCorreo.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, sinCorreo.StatusCode);

        // Byte a byte idénticos. Es el criterio literal de RF-CA-09.
        Assert.Equal(
            await conCorreo.Content.ReadAsStringAsync(),
            await sinCorreo.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Un_correo_mal_formado_da_la_misma_respuesta_que_los_demas()
    {
        var cliente = _app.CreateClient();

        var referencia = await cliente.PostAsJsonAsync("/contrasenas/recuperacion", new
        {
            correo = CorreoUnico("ausente")
        });
        var malFormado = await cliente.PostAsJsonAsync("/contrasenas/recuperacion", new
        {
            correo = "no-es-un-correo"
        });
        var vacio = await cliente.PostAsJsonAsync("/contrasenas/recuperacion", new { correo = "" });

        Assert.Equal(HttpStatusCode.Accepted, malFormado.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, vacio.StatusCode);

        Assert.Equal(
            await referencia.Content.ReadAsStringAsync(),
            await malFormado.Content.ReadAsStringAsync());
        Assert.Equal(
            await referencia.Content.ReadAsStringAsync(),
            await vacio.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Un_correo_inexistente_no_encola_ningun_correo()
    {
        var cliente = _app.CreateClient();
        var correo = CorreoUnico("nadie");

        await cliente.PostAsJsonAsync("/contrasenas/recuperacion", new { correo });

        // Encolar a una dirección que no está registrada sería una fuga en sí mismo:
        // confirmaría a quien lo pidiera que ese correo existe en el sistema.
        Assert.Null(await UltimoCorreoAsync(correo));
    }

    [Fact]
    public async Task Una_cuenta_inactiva_no_recibe_codigo_y_no_se_distingue()
    {
        var cliente = _app.CreateClient();
        var inactivo = await CrearUsuarioAsync(activo: false);

        var respuesta = await cliente.PostAsJsonAsync("/contrasenas/recuperacion", new
        {
            correo = inactivo.Correo
        });

        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);

        // Un código de recuperación no le serviría de nada: la cuenta no puede iniciar
        // sesión (RF-CA-15), así que no se le manda.
        Assert.Null(await UltimoCorreoAsync(inactivo.Correo));
        Assert.Empty(await CodigosAsync(inactivo.Id));
    }

    // ---------------------------------------------------------------------------------------
    // RF-CA-10 — código de un solo uso, con vencimiento, encolado en la cola de correo.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task La_recuperacion_encola_el_correo_con_el_codigo()
    {
        var cliente = _app.CreateClient();
        var usuario = await CrearUsuarioAsync();

        await cliente.PostAsJsonAsync("/contrasenas/recuperacion", new { correo = usuario.Correo });

        var correo = await UltimoCorreoAsync(usuario.Correo);

        Assert.NotNull(correo);

        // Sale por la COLA, no por SMTP dentro de la operación (RF-NOT-08).
        Assert.Equal(EstadoCorreo.Pendiente, correo.Estado);
        Assert.Equal("recuperacion", correo.Plantilla);
        Assert.Contains("contrasenas/recuperacion?codigo=", correo.Cuerpo);
        Assert.Equal(0, correo.Intentos);
        Assert.Null(correo.FechaEnvio);
    }

    [Fact]
    public async Task El_codigo_no_se_guarda_en_claro_en_la_base()
    {
        var cliente = _app.CreateClient();
        var usuario = await CrearUsuarioAsync();

        await cliente.PostAsJsonAsync("/contrasenas/recuperacion", new { correo = usuario.Correo });

        var codigo = await CodigoEnviadoAsync(usuario.Correo);
        var hashes = await HashesDeCodigoAsync(usuario.Id);

        Assert.NotEmpty(hashes);

        // Lo que se guarda es exactamente el SHA-256 del código emitido, no el código.
        var enClaro = Encoding.UTF8.GetBytes(codigo);
        Assert.Contains(GeneradorDeSecretos.Hash(codigo), hashes);
        Assert.All(hashes, h => Assert.False(h.AsSpan().SequenceEqual(enClaro)));
    }

    [Fact]
    public async Task Usar_el_codigo_dos_veces_se_rechaza_y_no_cambia_la_contrasena()
    {
        var usuario = await CrearUsuarioAsync();
        var codigo = await PedirYCodigoAsync(usuario.Correo);

        var primera = await RestablecerAsync(codigo, ContrasenaNueva);
        var segunda = await RestablecerAsync(codigo, "OtraClave999");

        Assert.Equal(HttpStatusCode.OK, primera.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, segunda.StatusCode);

        // La segunda vez no cambió nada: la contraseña sigue siendo la de la primera.
        Assert.Null(await IntentarIniciarSesionAsync(usuario.Correo, "OtraClave999"));
        Assert.NotNull(await IntentarIniciarSesionAsync(usuario.Correo, ContrasenaNueva));
    }

    [Fact]
    public async Task Un_codigo_vencido_se_rechaza_y_la_contrasena_no_cambia()
    {
        var usuario = await CrearUsuarioAsync();
        var codigo = await PedirYCodigoAsync(usuario.Correo);

        await VencerCodigoAsync(usuario.Id);

        var respuesta = await RestablecerAsync(codigo, ContrasenaNueva);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);

        // La contraseña es la de siempre, no la nueva.
        Assert.NotNull(await IntentarIniciarSesionAsync(usuario.Correo, ContrasenaValida));
        Assert.Null(await IntentarIniciarSesionAsync(usuario.Correo, ContrasenaNueva));
    }

    [Fact]
    public async Task Un_codigo_inventado_no_sirve()
    {
        var respuesta = await RestablecerAsync(
            GeneradorDeSecretos.NuevoToken(), ContrasenaNueva);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Pedir_la_recuperacion_dos_veces_invalida_el_codigo_anterior()
    {
        var cliente = _app.CreateClient();
        var usuario = await CrearUsuarioAsync();

        await cliente.PostAsJsonAsync("/contrasenas/recuperacion", new { correo = usuario.Correo });
        var primerCodigo = await CodigoEnviadoAsync(usuario.Correo);

        await cliente.PostAsJsonAsync("/contrasenas/recuperacion", new { correo = usuario.Correo });
        var segundoCodigo = await CodigoEnviadoAsync(usuario.Correo);

        Assert.NotEqual(primerCodigo, segundoCodigo);

        // Sólo el último emitido sirve.
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await RestablecerAsync(primerCodigo, ContrasenaNueva)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await RestablecerAsync(segundoCodigo, ContrasenaNueva)).StatusCode);
    }

    [Fact]
    public async Task Comprobar_el_codigo_no_lo_gasta()
    {
        var cliente = _app.CreateClient();
        var usuario = await CrearUsuarioAsync();
        var codigo = await PedirYCodigoAsync(usuario.Correo);

        // Es lo que responde al enlace del correo: puede decir que sirve sin gastarlo.
        var primera = await cliente.GetAsync($"/contrasenas/recuperacion?codigo={Uri.EscapeDataString(codigo)}");
        var segunda = await cliente.GetAsync($"/contrasenas/recuperacion?codigo={Uri.EscapeDataString(codigo)}");

        Assert.Equal(HttpStatusCode.OK, primera.StatusCode);
        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);

        // Y sigue sirviendo para lo único para lo que sirve: establecer la contraseña.
        Assert.Equal(HttpStatusCode.OK, (await RestablecerAsync(codigo, ContrasenaNueva)).StatusCode);
    }

    [Fact]
    public async Task Comprobar_un_codigo_vencido_responde_que_hay_que_pedir_otro()
    {
        var cliente = _app.CreateClient();
        var usuario = await CrearUsuarioAsync();
        var codigo = await PedirYCodigoAsync(usuario.Correo);

        await VencerCodigoAsync(usuario.Id);

        var respuesta = await cliente.GetAsync(
            $"/contrasenas/recuperacion?codigo={Uri.EscapeDataString(codigo)}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
    }

    // ---------------------------------------------------------------------------------------
    // RF-CA-11 — la contraseña nueva se guarda hasheada y la anterior deja de servir.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task La_contrasena_nueva_sirve_y_la_anterior_deja_de_servir()
    {
        var usuario = await CrearUsuarioAsync();
        var codigo = await PedirYCodigoAsync(usuario.Correo);

        var respuesta = await RestablecerAsync(codigo, ContrasenaNueva);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        Assert.Null(await IntentarIniciarSesionAsync(usuario.Correo, ContrasenaValida));
        Assert.NotNull(await IntentarIniciarSesionAsync(usuario.Correo, ContrasenaNueva));
    }

    [Fact]
    public async Task La_contrasena_nueva_se_guarda_hasheada()
    {
        var usuario = await CrearUsuarioAsync();
        var codigo = await PedirYCodigoAsync(usuario.Correo);

        await RestablecerAsync(codigo, ContrasenaNueva);

        var guardado = await UsuarioAsync(usuario.Id);

        Assert.NotEqual(ContrasenaNueva, guardado.PasswordHash);
        Assert.DoesNotContain(ContrasenaNueva, guardado.PasswordHash);
        Assert.Equal(
            PasswordVerificationResult.Success,
            new PasswordHasher<Usuario>().VerifyHashedPassword(guardado, guardado.PasswordHash, ContrasenaNueva));
    }

    [Fact]
    public async Task Una_contrasena_nueva_que_no_cumple_la_politica_no_gasta_el_codigo()
    {
        var usuario = await CrearUsuarioAsync();
        var codigo = await PedirYCodigoAsync(usuario.Correo);

        var rechazo = await RestablecerAsync(codigo, "corta");
        Assert.Equal(HttpStatusCode.BadRequest, rechazo.StatusCode);

        // El usuario puede corregirla sin pedir otro código: una tilde de más no puede
        // obligar a perder el código entero.
        Assert.Equal(HttpStatusCode.OK, (await RestablecerAsync(codigo, ContrasenaNueva)).StatusCode);
    }

    // ---------------------------------------------------------------------------------------
    // RF-CA-12 — las sesiones abiertas antes del cambio dejan de servir.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Las_sesiones_abiertas_antes_de_recuperar_dejan_de_servir()
    {
        var usuario = await CrearUsuarioAsync();
        var token = await IniciarSesionAsync(usuario.Correo, ContrasenaValida);
        var codigo = await PedirYCodigoAsync(usuario.Correo);

        var clienteConCredencial = ClienteConCredencial(token);
        Assert.Equal(HttpStatusCode.OK, (await clienteConCredencial.GetAsync("/sesion/yo")).StatusCode);

        await RestablecerAsync(codigo, ContrasenaNueva);

        // La credencial que tenía antes de la recuperación ya no vale.
        Assert.Equal(HttpStatusCode.Unauthorized, (await clienteConCredencial.GetAsync("/sesion/yo")).StatusCode);
    }

    [Fact]
    public async Task Tras_recuperar_la_contrasena_se_puede_iniciar_sesion_de_nuevo()
    {
        var usuario = await CrearUsuarioAsync();
        var codigo = await PedirYCodigoAsync(usuario.Correo);

        await RestablecerAsync(codigo, ContrasenaNueva);

        var token = await IniciarSesionAsync(usuario.Correo, ContrasenaNueva);

        Assert.NotNull(token);
        Assert.Equal(HttpStatusCode.OK, (await ClienteConCredencial(token).GetAsync("/sesion/yo")).StatusCode);
    }

    [Fact]
    public async Task Recuperar_la_contrasena_levanta_el_bloqueo_por_intentos()
    {
        var usuario = await CrearUsuarioAsync(bloqueadoHasta: DateTimeOffset.UtcNow.AddHours(1));

        // Bloqueado: ni siquiera con la contraseña correcta.
        Assert.Null(await IntentarIniciarSesionAsync(usuario.Correo, ContrasenaValida));

        var codigo = await PedirYCodigoAsync(usuario.Correo);
        await RestablecerAsync(codigo, ContrasenaNueva);

        // Haber leído el correo prueba que se controla el buzón, y eso es más fuerte que
        // un intento de acceso. Si no se levantara el bloqueo, el usuario se quedaría
        // fuera hasta que venciera.
        var guardado = await UsuarioAsync(usuario.Id);
        Assert.Null(guardado.BloqueadoHasta);
        Assert.Equal(0, guardado.IntentosFallidos);
        Assert.NotNull(await IntentarIniciarSesionAsync(usuario.Correo, ContrasenaNueva));
    }

    // ---------------------------------------------------------------------------------------
    // RF-CA-13 — restablecimiento forzado por Administrador.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Un_administrador_fuerza_el_restablecimiento_y_la_contrasena_anterior_muere()
    {
        var victima = await CrearUsuarioAsync();
        var tokenAdmin = await TokenDeAdministradorAsync();

        var respuesta = await ClienteConCredencial(tokenAdmin)
            .PostAsJsonAsync("/usuarios/restablecer-contrasena", new { usuarioId = victima.Id });

        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);

        // La contraseña anterior deja de servir en el acto.
        Assert.Null(await IntentarIniciarSesionAsync(victima.Correo, ContrasenaValida));
    }

    [Fact]
    public async Task El_restablecimiento_forzado_cierra_las_sesiones_de_la_victima()
    {
        var victima = await CrearUsuarioAsync();
        var tokenVictima = await IniciarSesionAsync(victima.Correo, ContrasenaValida);
        var tokenAdmin = await TokenDeAdministradorAsync();

        await ClienteConCredencial(tokenAdmin)
            .PostAsJsonAsync("/usuarios/restablecer-contrasena", new { usuarioId = victima.Id });

        Assert.Equal(HttpStatusCode.Unauthorized, (await ClienteConCredencial(tokenVictima).GetAsync("/sesion/yo")).StatusCode);
    }

    [Fact]
    public async Task El_restablecimiento_forzado_encola_el_codigo_por_el_correo_registrado()
    {
        var victima = await CrearUsuarioAsync();
        var tokenAdmin = await TokenDeAdministradorAsync();

        await ClienteConCredencial(tokenAdmin)
            .PostAsJsonAsync("/usuarios/restablecer-contrasena", new { usuarioId = victima.Id });

        var correo = await UltimoCorreoAsync(victima.Correo);

        Assert.NotNull(correo);
        Assert.Equal(EstadoCorreo.Pendiente, correo.Estado);
        Assert.Equal("recuperacion", correo.Plantilla);

        // Y con ese código la víctima puede dejar su contraseña.
        var codigo = await CodigoEnviadoAsync(victima.Correo);
        Assert.Equal(HttpStatusCode.OK, (await RestablecerAsync(codigo, ContrasenaNueva)).StatusCode);
        Assert.NotNull(await IntentarIniciarSesionAsync(victima.Correo, ContrasenaNueva));
    }

    [Fact]
    public async Task Un_estandar_recibe_403_al_intentar_restablecer_otra_contrasena()
    {
        var victima = await CrearUsuarioAsync();
        var tokenEstandar = await IniciarSesionAsync((await CrearUsuarioAsync()).Correo, ContrasenaValida);

        var respuesta = await ClienteConCredencial(tokenEstandar)
            .PostAsJsonAsync("/usuarios/restablecer-contrasena", new { usuarioId = victima.Id });

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);

        // Y la contraseña de la víctima sigue en pie: el rechazo no cambió nada.
        Assert.NotNull(await IntentarIniciarSesionAsync(victima.Correo, ContrasenaValida));
    }

    [Fact]
    public async Task Sin_sesion_no_se_puede_forzar_un_restablecimiento()
    {
        var victima = await CrearUsuarioAsync();

        var respuesta = await _app.CreateClient()
            .PostAsJsonAsync("/usuarios/restablecer-contrasena", new { usuarioId = victima.Id });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_usuario_inexistente_da_404_en_el_restablecimiento_forzado()
    {
        var tokenAdmin = await TokenDeAdministradorAsync();

        var respuesta = await ClienteConCredencial(tokenAdmin)
            .PostAsJsonAsync("/usuarios/restablecer-contrasena", new { usuarioId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    // ---------------------------------------------------------------------------------------
    // RF-CA-22 — cambio de la contraseña propia con la actual.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Cambiar_la_contrasena_propia_cambia_el_hash_y_cierra_las_sesiones()
    {
        var usuario = await CrearUsuarioAsync();
        var token = await IniciarSesionAsync(usuario.Correo, ContrasenaValida);
        var hashAntes = (await UsuarioAsync(usuario.Id)).PasswordHash;

        var clienteConCredencial = ClienteConCredencial(token);
        var respuesta = await clienteConCredencial.PostAsJsonAsync("/contrasenas/propia", new
        {
            contrasenaActual = ContrasenaValida,
            contrasenaNueva = ContrasenaNueva
        });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var guardado = await UsuarioAsync(usuario.Id);
        Assert.NotEqual(hashAntes, guardado.PasswordHash);

        // RF-CA-12 también cierra la sesión que hizo la petición.
        Assert.Equal(HttpStatusCode.Unauthorized, (await clienteConCredencial.GetAsync("/sesion/yo")).StatusCode);
        Assert.Null(await IntentarIniciarSesionAsync(usuario.Correo, ContrasenaValida));
        Assert.NotNull(await IntentarIniciarSesionAsync(usuario.Correo, ContrasenaNueva));
    }

    [Fact]
    public async Task Con_la_contrasena_actual_incorrecta_el_cambio_se_rechaza_y_no_toca_nada()
    {
        var usuario = await CrearUsuarioAsync();
        var token = await IniciarSesionAsync(usuario.Correo, ContrasenaValida);
        var hashAntes = (await UsuarioAsync(usuario.Id)).PasswordHash;

        var clienteConCredencial = ClienteConCredencial(token);
        var respuesta = await clienteConCredencial.PostAsJsonAsync("/contrasenas/propia", new
        {
            contrasenaActual = "OtraClave9",
            contrasenaNueva = ContrasenaNueva
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        Assert.Equal(hashAntes, (await UsuarioAsync(usuario.Id)).PasswordHash);

        // Ni siquiera se cierra la sesión: el cambio ni se intentó.
        Assert.Equal(HttpStatusCode.OK, (await clienteConCredencial.GetAsync("/sesion/yo")).StatusCode);
    }

    [Fact]
    public async Task La_contrasena_actual_equivocada_no_es_401_sino_422()
    {
        var token = await IniciarSesionAsync((await CrearUsuarioAsync()).Correo, ContrasenaValida);

        var respuesta = await ClienteConCredencial(token).PostAsJsonAsync("/contrasenas/propia", new
        {
            contrasenaActual = "OtraClave9",
            contrasenaNueva = ContrasenaNueva
        });

        // La identidad ya está establecida: un 401 haría creer al cliente que su
        // credencial caducó, cuando lo que se ha equivocado es la contraseña actual.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
    }

    [Fact]
    public async Task Cambiar_la_contrasena_propia_sin_sesion_es_401()
    {
        var respuesta = await _app.CreateClient().PostAsJsonAsync("/contrasenas/propia", new
        {
            contrasenaActual = ContrasenaValida,
            contrasenaNueva = ContrasenaNueva
        });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task La_contrasena_nueva_del_cambio_propio_tambien_cumple_la_politica()
    {
        var usuario = await CrearUsuarioAsync();
        var token = await IniciarSesionAsync(usuario.Correo, ContrasenaValida);

        var clienteConCredencial = ClienteConCredencial(token);
        var respuesta = await clienteConCredencial.PostAsJsonAsync("/contrasenas/propia", new
        {
            contrasenaActual = ContrasenaValida,
            contrasenaNueva = "sinnumeros"
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await clienteConCredencial.GetAsync("/sesion/yo")).StatusCode);
    }

    // ---------------------------------------------------------------------------------------
    // Superficie pública: qué de este flujo se puede llamar sin identidad.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task El_flujo_de_recuperacion_es_publico_y_el_cambio_propio_no()
    {
        var cliente = _app.CreateClient();
        var codigo = GeneradorDeSecretos.NuevoToken();

        // Públicos: responden por la regla de negocio, no con 401.
        Assert.Equal(HttpStatusCode.Accepted,
            (await cliente.PostAsJsonAsync("/contrasenas/recuperacion", new
            {
                correo = CorreoUnico("nadie")
            })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await cliente.GetAsync($"/contrasenas/recuperacion?codigo={Uri.EscapeDataString(codigo)}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await cliente.PostAsJsonAsync("/contrasenas/restablecer", new
            {
                codigo,
                contrasenaNueva = ContrasenaNueva
            })).StatusCode);

        // Éste sí exige identidad.
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await cliente.PostAsJsonAsync("/contrasenas/propia", new
            {
                contrasenaActual = ContrasenaValida,
                contrasenaNueva = ContrasenaNueva
            })).StatusCode);
    }

    [Fact]
    public async Task Ningun_rechazo_de_este_flujo_filtra_trazas_ni_consultas()
    {
        var usuario = await CrearUsuarioAsync();
        var codigo = await PedirYCodigoAsync(usuario.Correo);
        await VencerCodigoAsync(usuario.Id);

        var rechazo = await RestablecerAsync(codigo, ContrasenaNueva);
        var cuerpo = await rechazo.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, rechazo.StatusCode);

        foreach (var prohibido in new[] { "SELECT", "INSERT", "UPDATE", "Biblioteca.Identidad", ".cs:", "   at " })
        {
            Assert.DoesNotContain(prohibido, cuerpo);
        }
    }

    // ---------------------------------------------------------------------------------------
    // Utilidades
    // ---------------------------------------------------------------------------------------

    private static string CorreoUnico(string prefijo) => $"{prefijo}-{Guid.NewGuid():N}@ejemplo.com";

    /// <summary>
    /// Inserta un usuario directamente. La contraseña se hashea con el mismo hasher que
    /// usa la aplicación, para que la prueba no dependa del endpoint de registro.
    /// </summary>
    private async Task<Usuario> CrearUsuarioAsync(
        bool activo = true,
        Rol rol = Rol.Estandar,
        DateTimeOffset? bloqueadoHasta = null)
    {
        var usuario = new Usuario
        {
            Nombre = "Ana Ruiz",
            Correo = CorreoUnico(rol == Rol.Administrador ? "admin" : "socio"),
            Activo = activo,
            Rol = rol,
            BloqueadoHasta = bloqueadoHasta,
            CreadoEn = DateTimeOffset.UtcNow
        };

        usuario.PasswordHash = new PasswordHasher<Usuario>().HashPassword(usuario, ContrasenaValida);

        var fabrica = _app.Services.GetRequiredService<IDbContextFactory<IdentidadDbContext>>();
        await using var db = await fabrica.CreateDbContextAsync();

        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();

        return usuario;
    }

    /// <summary>Alta de un Administrador y apertura de su sesión.</summary>
    private async Task<string> TokenDeAdministradorAsync()
    {
        var admin = await CrearUsuarioAsync(rol: Rol.Administrador);

        return await IniciarSesionAsync(admin.Correo, ContrasenaValida);
    }

    /// <summary>
    /// Pide la recuperación y devuelve el código que salió en el correo. Es la forma más
    /// fiel de seguir el flujo: el código no se inventa en la prueba, se lee de la cola,
    /// que es donde lo dejó el sistema.
    /// </summary>
    private async Task<string> PedirYCodigoAsync(string correo)
    {
        var respuesta = await _app.CreateClient()
            .PostAsJsonAsync("/contrasenas/recuperacion", new { correo });

        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);

        return await CodigoEnviadoAsync(correo);
    }

    private Task<HttpResponseMessage> RestablecerAsync(string codigo, string contrasenaNueva) =>
        _app.CreateClient().PostAsJsonAsync("/contrasenas/restablecer", new
        {
            codigo,
            contrasenaNueva
        });

    private async Task<string?> IntentarIniciarSesionAsync(string correo, string contrasena)
    {
        var respuesta = await _app.CreateClient().PostAsJsonAsync("/sesion/iniciar", new
        {
            correo,
            contrasena
        });

        if (respuesta.StatusCode != HttpStatusCode.OK)
        {
            return null;
        }

        using var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        return cuerpo.RootElement.GetProperty("token").GetString();
    }

    private async Task<string> IniciarSesionAsync(string correo, string contrasena)
    {
        var token = await IntentarIniciarSesionAsync(correo, contrasena);

        return token ?? throw new InvalidOperationException(
            $"No se pudo iniciar sesión con {correo}. Las pruebas requieren un usuario activo.");
    }

    private HttpClient ClienteConCredencial(string token)
    {
        var cliente = _app.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return cliente;
    }

    private async Task<Usuario> UsuarioAsync(Guid id)
    {
        var fabrica = _app.Services.GetRequiredService<IDbContextFactory<IdentidadDbContext>>();
        await using var db = await fabrica.CreateDbContextAsync();

        return await db.Usuarios.AsNoTracking().FirstAsync(u => u.Id == id);
    }

    /// <summary>Último correo encolado a un destinatario, o null si no hay ninguno.</summary>
    private async Task<CorreoEnCola?> UltimoCorreoAsync(string destinatario)
    {
        var fabrica = _app.Services.GetRequiredService<IDbContextFactory<CorreoDbContext>>();
        await using var db = await fabrica.CreateDbContextAsync();

        return await db.CorreosEnCola
            .AsNoTracking()
            .Where(c => c.Destinatario == destinatario)
            .OrderByDescending(c => c.CreadoEn)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Saca el código del cuerpo del correo, del propio enlace. Leerlo de ahí y no
    /// inventarlo en la prueba comprueba además que el enlace está bien formado.
    /// </summary>
    private async Task<string> CodigoEnviadoAsync(string correo)
    {
        var mensaje = await UltimoCorreoAsync(correo);

        Assert.NotNull(mensaje);

        var enlace = Regex.Match(
            mensaje.Cuerpo,
            @"contrasenas/recuperacion\?codigo=(?<codigo>[A-Za-z0-9_\-]+)");

        Assert.True(enlace.Success, "El correo no lleva el enlace de recuperación con el código.");

        return Uri.UnescapeDataString(enlace.Groups["codigo"].Value);
    }

    private async Task<List<byte[]>> HashesDeCodigoAsync(Guid usuarioId)
    {
        var fabrica = _app.Services.GetRequiredService<IDbContextFactory<IdentidadDbContext>>();
        await using var db = await fabrica.CreateDbContextAsync();

        return await db.CodigosRecuperacion
            .AsNoTracking()
            .Where(c => c.UsuarioId == usuarioId)
            .Select(c => c.CodigoHash)
            .ToListAsync();
    }

    private async Task<List<CodigoRecuperacion>> CodigosAsync(Guid usuarioId)
    {
        var fabrica = _app.Services.GetRequiredService<IDbContextFactory<IdentidadDbContext>>();
        await using var db = await fabrica.CreateDbContextAsync();

        return await db.CodigosRecuperacion
            .AsNoTracking()
            .Where(c => c.UsuarioId == usuarioId)
            .ToListAsync();
    }

    /// <summary>
    /// Pone el vencimiento de los códigos del usuario en el pasado.
    ///
    /// Se toca la tabla en lugar de esperar: el reloj de la aplicación es
    /// <see cref="TimeProvider.System"/> real y las pruebas no pueden pararlo.
    /// Adelantarlo es exactamente lo mismo que dejar pasar el tiempo.
    /// </summary>
    private async Task VencerCodigoAsync(Guid usuarioId)
    {
        var fabrica = _app.Services.GetRequiredService<IDbContextFactory<IdentidadDbContext>>();
        await using var db = await fabrica.CreateDbContextAsync();

        await db.CodigosRecuperacion
            .Where(c => c.UsuarioId == usuarioId)
            .ExecuteUpdateAsync(
                c => c.SetProperty(x => x.VenceEn, DateTimeOffset.UtcNow.AddMinutes(-1)));
    }
}