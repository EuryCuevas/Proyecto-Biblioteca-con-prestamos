using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Biblioteca.Identidad;
using Biblioteca.Identidad.Administracion;
using Biblioteca.Identidad.Entidades;
using Biblioteca.Identidad.Persistencia;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biblioteca.Api.Tests;

/// <summary>
/// Roles y administración de usuarios (RF-CA-04, 06, 08, 20 y 21).
///
/// Igual que en las demás pruebas de integración, no se simula nada: se habla HTTP
/// contra la aplicación real y se mira la base de datos de verdad. Los usuarios se
/// insertan directamente para que estas pruebas no dependan de un pull request
/// anterior.
///
/// Lo que aquí se prueba de verdad, y no por decoración, es el rechazo por rol. La
/// exigencia de rol se lee en <c>PoliticaDeOperaciones</c> y la evalúa un guard que
/// hasta el PR 3 no lo invocaba ninguna política, con lo que el rechazo no se
/// comprobaba en ninguna petición. Estas pruebas de 403 son las que lo destaparon y
/// son las que lo vigilarían si volviera a romperse.
/// </summary>
public class AdministracionDeUsuariosTests : IClassFixture<FabricaDeAplicacionDePrueba>
{
    private const string ContrasenaValida = "Biblioteca2026";

    private readonly WebApplicationFactory<Program> _app;

    public AdministracionDeUsuariosTests(FabricaDeAplicacionDePrueba app) => _app = app;

    /// <summary>
    /// Las cuatro operaciones de administración de usuarios, con su método y un cuerpo
    /// mínimo. Los cuerpos no importan para el resultado de las pruebas de rechazo: la
    /// petición tiene que morir en el guard, antes de tocar nada. El cuerpo vacío
    /// significa "sin cuerpo".
    /// </summary>
    private static readonly Caso[] CasosDeAdministracion =
    [
        new(HttpMethod.Get, "/usuarios", ""),
        new(HttpMethod.Post, "/usuarios/cambiar-rol",
            """{"usuarioId":"00000000-0000-0000-0000-000000000000","rol":"Administrador"}"""),
        new(HttpMethod.Post, "/usuarios/desactivar",
            """{"usuarioId":"00000000-0000-0000-0000-000000000000"}"""),
        new(HttpMethod.Post, "/usuarios/reactivar",
            """{"usuarioId":"00000000-0000-0000-0000-000000000000"}""")
    ];

    // ---------------------------------------------------------------------------------------
    // RF-CA-04 — dos roles, y todo usuario tiene exactamente uno.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Hay_exactamente_dos_roles_y_se_llaman_así()
    {
        // El enunciado dice "dos roles: Administrador y Estándar". Ni uno más ni uno
        // menos: un tercer rol sería un sistema de permisos que nadie pidió y cuya
        // exigencia en PoliticaDeOperaciones nadie ha revisado.
        var roles = Enum.GetValues<Rol>().ToList();

        Assert.Equal(2, roles.Count);
        Assert.Contains(Rol.Administrador, roles);
        Assert.Contains(Rol.Estandar, roles);
    }

    [Fact]
    public void Todo_rol_del_enum_aparece_en_la_politica_de_operaciones()
    {
        // Conecta RF-CA-04 con RF-CA-05. Si alguien añade un valor al enum Rol y
        // olvida darle un sitio en la tabla, ese usuario existiría y no podría ejecutar
        // nada. Aquí se detecta sin ni siquiera levantar la aplicación.
        var declarados = PoliticaDeOperaciones.Requisitos
            .SelectMany(par => par.Value)
            .Distinct()
            .ToList();

        var sinSitio = Enum.GetValues<Rol>().Where(rol => !declarados.Contains(rol)).ToList();

        Assert.Empty(sinSitio);
    }

    [Fact]
    public async Task Cambiar_el_rol_deja_al_usuario_exactamente_ese_rol()
    {
        // La marca lleva un Guid porque la base es compartida con las demás pruebas de
        // la clase y con las reejecuciones: una marca constante acumularía usuarios de
        // pruebas anteriores y "aparecer una sola vez" no significaría nada.
        var marca = $"unrolo-{Guid.NewGuid():N}";
        var cliente = Cliente(await TokenDeAdministradorAsync());
        var usuario = await CrearUsuarioAsync(marca: marca);

        var respuesta = await cliente.PostAsJsonAsync("/usuarios/cambiar-rol", new
        {
            usuarioId = usuario.Id,
            rol = "Administrador"
        });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        // En la base hay un rol, no una colección de roles: el campo es un enum, no una
        // tabla de asignaciones. Cambiar sustituye, y por eso "todo usuario tiene
        // exactamente un rol" no es una promesa sino una consecuencia de la forma de la
        // operación: no existe el camino que deje dos.
        Assert.Equal(Rol.Administrador, (await UsuarioAsync(usuario.Id)).Rol);

        // Y en el listado aparece una sola vez, con el rol nuevo.
        var fila = Assert.Single(Mios(await LeerListadoAsync(cliente), marca));
        Assert.Equal("Administrador", fila.Rol);
    }

    // ---------------------------------------------------------------------------------------
    // RF-CA-21 — el listado muestra rol y estado, y nunca hashes ni tokens.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task El_listado_muestra_el_rol_y_el_estado_de_cada_usuario()
    {
        var marca = $"listado-{Guid.NewGuid():N}";
        var cliente = Cliente(await TokenDeAdministradorAsync());
        await CrearUsuarioAsync(activo: true, marca: marca);
        await CrearUsuarioAsync(activo: false, marca: marca);

        // Se filtra a las cuentas creadas por esta prueba, porque la base es compartida
        // con las demás pruebas de la clase y con las reejecuciones.
        var mios = Mios(await LeerListadoAsync(cliente), marca).ToList();

        Assert.Equal(2, mios.Count);
        Assert.Equal(1, mios.Count(f => f.Activo));
        Assert.Equal(1, mios.Count(f => !f.Activo));
        Assert.All(mios, fila => Assert.Equal("Estandar", fila.Rol));
    }

    [Fact]
    public async Task El_listado_no_incluye_hashes_ni_tokens()
    {
        var marca = $"secreto-{Guid.NewGuid():N}";
        var cliente = Cliente(await TokenDeAdministradorAsync());
        var conHash = await CrearUsuarioAsync(marca: marca);

        var cuerpo = await (await cliente.GetAsync("/usuarios")).Content.ReadAsStringAsync();
        var guardado = await UsuarioAsync(conHash.Id);

        // La prueba fuerte: el valor real del hash no sale en ninguna fila, ni en la de
        // este usuario ni en la de nadie. El hash es único por usuario, así que si
        // apareciera, sería porque alguien proyectó la entidad.
        Assert.DoesNotContain(guardado.PasswordHash, cuerpo);

        // Y el contrato de campos es exacto. Se compara contra la lista en vez de
        // buscar palabras prohibidas en el JSON entero a propósito: los correos de los
        // usuarios los elige quien los crea, y cualquier nombre de correo que casara
        // con una de esas palabras haría fallar la prueba sin que hubiera nada mal en
        // el sistema. Esta lista no puede dar falsos positivos.
        using var listado = JsonDocument.Parse(cuerpo);
        var fila = listado.RootElement
            .EnumerateArray()
            .Single(elemento => elemento.GetProperty("id").GetGuid() == conHash.Id);

        var campos = fila.EnumerateObject()
            .Select(propiedad => propiedad.Name)
            .Order()
            .ToList();

        Assert.Equal(
            ["activo", "correo", "creadoEn", "id", "nombre", "passwordCambiadoEn", "rol"],
            campos);
    }

    [Fact]
    public void El_tipo_de_fila_del_listado_no_tiene_ningun_campo_de_secreto()
    {
        // La misma garantía sobre el tipo, no sólo sobre su serialización: si mañana se
        // añade una propiedad al record, esta prueba obliga a justificarla.
        var sospechosos = typeof(ResumenDeUsuario)
            .GetProperties()
            .Select(p => p.Name)
            .Where(nombre => nombre.Contains("Hash", StringComparison.OrdinalIgnoreCase)
                || nombre.Contains("Token", StringComparison.OrdinalIgnoreCase)
                || nombre.Contains("Codigo", StringComparison.OrdinalIgnoreCase)
                || nombre.Contains("Secreto", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(sospechosos);
    }

    // ---------------------------------------------------------------------------------------
    // RF-CA-08 — el cambio de rol es del Administrador, y un Estándar no cambia ni el suyo.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Un_administrador_cambia_el_rol_de_un_usuario()
    {
        var cliente = Cliente(await TokenDeAdministradorAsync());
        var usuario = await CrearUsuarioAsync();

        var respuesta = await cliente.PostAsJsonAsync("/usuarios/cambiar-rol", new
        {
            usuarioId = usuario.Id,
            rol = "Administrador"
        });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        using var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal("Estandar", cuerpo.RootElement.GetProperty("rolAnterior").GetString());
        Assert.Equal("Administrador", cuerpo.RootElement.GetProperty("rolNuevo").GetString());

        Assert.Equal(Rol.Administrador, (await UsuarioAsync(usuario.Id)).Rol);
    }

    [Fact]
    public async Task Un_estandar_ascendido_precisa_entrar_de_nuevo_para_usar_el_rol_nuevo()
    {
        var admin = Cliente(await TokenDeAdministradorAsync());
        var usuario = await CrearUsuarioAsync();
        var tokenViejo = await IniciarSesionAsync(usuario.Correo, ContrasenaValida);

        // Un Estándar todavía no puede ni listar; eso lo comprueba otra prueba.
        Assert.Equal(HttpStatusCode.Forbidden, (await Cliente(tokenViejo).GetAsync("/usuarios")).StatusCode);

        await admin.PostAsJsonAsync("/usuarios/cambiar-rol", new
        {
            usuarioId = usuario.Id,
            rol = "Administrador"
        });

        // El rol viaja congelado dentro de la credencial, así que la vieja no se entera
        // del ascenso por sí sola.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Cliente(tokenViejo).GetAsync("/usuarios")).StatusCode);

        var tokenNuevo = await IniciarSesionAsync(usuario.Correo, ContrasenaValida);
        Assert.Equal(HttpStatusCode.OK, (await Cliente(tokenNuevo).GetAsync("/usuarios")).StatusCode);
    }

    [Fact]
    public async Task Un_administrador_degradado_pierde_el_acceso_en_el_acto()
    {
        var admin = Cliente(await TokenDeAdministradorAsync());
        var usuario = await CrearUsuarioAsync(rol: Rol.Administrador);
        var token = await IniciarSesionAsync(usuario.Correo, ContrasenaValida);

        Assert.Equal(HttpStatusCode.OK, (await Cliente(token).GetAsync("/usuarios")).StatusCode);

        await admin.PostAsJsonAsync("/usuarios/cambiar-rol", new
        {
            usuarioId = usuario.Id,
            rol = "Estandar"
        });

        // Sin cerrarle las sesiones, seguiría administrando hasta que venciera su
        // credencial. Eso es un agujero de seguridad, no una molestia.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Cliente(token).GetAsync("/usuarios")).StatusCode);
    }

    [Fact]
    public async Task Un_estandar_no_puede_cambiar_el_rol_de_otro()
    {
        var objetivo = await CrearUsuarioAsync();
        var token = await TokenDeEstandarAsync();

        var respuesta = await PeticionManualAsync(token, HttpMethod.Post, "/usuarios/cambiar-rol",
            $$"""{"usuarioId":"{{objetivo.Id}}","rol":"Administrador"}""");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal(Rol.Estandar, (await UsuarioAsync(objetivo.Id)).Rol);
    }

    [Fact]
    public async Task Un_estandar_no_puede_cambiar_su_propio_rol()
    {
        // El caso que la revisión manual nombra: "con sesión de Estándar, invocar una
        // operación de Administrador construyendo la petición a mano; intentar cambiar
        // mi propio rol".
        var usuario = await CrearUsuarioAsync();
        var token = await IniciarSesionAsync(usuario.Correo, ContrasenaValida);

        var respuesta = await PeticionManualAsync(token, HttpMethod.Post, "/usuarios/cambiar-rol",
            $$"""{"usuarioId":"{{usuario.Id}}","rol":"Administrador"}""");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal(Rol.Estandar, (await UsuarioAsync(usuario.Id)).Rol);
    }

    [Fact]
    public async Task Cambiar_a_un_rol_que_no_existe_da_error_de_regla()
    {
        var token = await TokenDeAdministradorAsync();
        var usuario = await CrearUsuarioAsync();

        // Cuerpo escrito a mano porque PostAsJsonAsync no deja pasar un enum inválido.
        var respuesta = await PeticionManualAsync(token, HttpMethod.Post, "/usuarios/cambiar-rol",
            $$"""{"usuarioId":"{{usuario.Id}}","rol":7}""");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        Assert.Equal(Rol.Estandar, (await UsuarioAsync(usuario.Id)).Rol);
    }

    [Fact]
    public async Task Cambiar_al_rol_que_ya_tiene_da_error_de_regla_y_no_cierra_sesiones()
    {
        var cliente = Cliente(await TokenDeAdministradorAsync());
        var usuario = await CrearUsuarioAsync();
        var token = await IniciarSesionAsync(usuario.Correo, ContrasenaValida);

        var respuesta = await cliente.PostAsJsonAsync("/usuarios/cambiar-rol", new
        {
            usuarioId = usuario.Id,
            rol = "Estandar"
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);

        // Un cambio que no cambia nada no debe expulsar al usuario.
        Assert.Equal(HttpStatusCode.OK, (await Cliente(token).GetAsync("/sesion/yo")).StatusCode);
    }

    [Fact]
    public async Task Cambiar_el_rol_de_un_usuario_inexistente_da_404()
    {
        var cliente = Cliente(await TokenDeAdministradorAsync());

        var respuesta = await cliente.PostAsJsonAsync("/usuarios/cambiar-rol", new
        {
            usuarioId = Guid.NewGuid(),
            rol = "Administrador"
        });

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    // ---------------------------------------------------------------------------------------
    // RF-CA-20 — desactivar y reactivar.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Un_usuario_desactivado_no_inicia_sesion()
    {
        var admin = Cliente(await TokenDeAdministradorAsync());
        var usuario = await CrearUsuarioAsync();

        Assert.Equal(HttpStatusCode.OK,
            (await admin.PostAsJsonAsync("/usuarios/desactivar", new { usuarioId = usuario.Id })).StatusCode);

        Assert.Null(await IntentarIniciarSesionAsync(usuario.Correo, ContrasenaValida));
    }

    [Fact]
    public async Task Desactivar_cierra_las_sesiones_que_ya_estaban_abiertas()
    {
        var admin = Cliente(await TokenDeAdministradorAsync());
        var usuario = await CrearUsuarioAsync();
        var token = await IniciarSesionAsync(usuario.Correo, ContrasenaValida);

        Assert.Equal(HttpStatusCode.OK, (await Cliente(token).GetAsync("/sesion/yo")).StatusCode);

        var respuesta = await admin.PostAsJsonAsync("/usuarios/desactivar", new { usuarioId = usuario.Id });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        using var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal(1, cuerpo.RootElement.GetProperty("sesionesCerradas").GetInt32());

        Assert.Equal(HttpStatusCode.Unauthorized, (await Cliente(token).GetAsync("/sesion/yo")).StatusCode);
    }

    [Fact]
    public async Task Reactivar_vuelve_a_permitir_iniciar_sesion()
    {
        var admin = Cliente(await TokenDeAdministradorAsync());
        var usuario = await CrearUsuarioAsync();

        await admin.PostAsJsonAsync("/usuarios/desactivar", new { usuarioId = usuario.Id });
        Assert.Null(await IntentarIniciarSesionAsync(usuario.Correo, ContrasenaValida));

        Assert.Equal(HttpStatusCode.OK,
            (await admin.PostAsJsonAsync("/usuarios/reactivar", new { usuarioId = usuario.Id })).StatusCode);

        Assert.NotNull(await IntentarIniciarSesionAsync(usuario.Correo, ContrasenaValida));
    }

    [Fact]
    public async Task Reactivar_no_devuelve_las_sesiones_anteriores()
    {
        var admin = Cliente(await TokenDeAdministradorAsync());
        var usuario = await CrearUsuarioAsync();
        var token = await IniciarSesionAsync(usuario.Correo, ContrasenaValida);

        await admin.PostAsJsonAsync("/usuarios/desactivar", new { usuarioId = usuario.Id });
        await admin.PostAsJsonAsync("/usuarios/reactivar", new { usuarioId = usuario.Id });

        // La credencial cerrada no revive por el hecho de que la cuenta vuelva a estar
        // activa: reactivar devuelve el acceso, no la entrada.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Cliente(token).GetAsync("/sesion/yo")).StatusCode);
        Assert.NotNull(await IntentarIniciarSesionAsync(usuario.Correo, ContrasenaValida));
    }

    [Fact]
    public async Task Reactivar_no_levanta_el_bloqueo_por_intentos_fallidos()
    {
        // Desactivar y bloquear son dos cosas distintas (RF-CA-19 y RF-CA-20). Reactivar
        // no promete levantar el bloqueo: lo hace constar, y esta prueba lo fija.
        var admin = Cliente(await TokenDeAdministradorAsync());

        var bloqueado = await CrearUsuarioAsync(activo: false, marca: "bloqueo",
            bloqueadoHasta: DateTimeOffset.UtcNow.AddMinutes(15));
        var normal = await CrearUsuarioAsync(activo: false, marca: "bloqueo");

        await admin.PostAsJsonAsync("/usuarios/reactivar", new { usuarioId = bloqueado.Id });
        await admin.PostAsJsonAsync("/usuarios/reactivar", new { usuarioId = normal.Id });

        // El control: la reactivación sí funciona. Sin esta comprobación, la de abajo no
        // probaría nada, porque un fallo que devolviera "sigue bloqueado" a todos
        // también la satisfaría.
        Assert.NotNull(await IntentarIniciarSesionAsync(normal.Correo, ContrasenaValida));

        // Y el bloqueo sigue en pie hasta que vence solo.
        Assert.NotNull((await UsuarioAsync(bloqueado.Id)).BloqueadoHasta);
        Assert.Null(await IntentarIniciarSesionAsync(bloqueado.Correo, ContrasenaValida));
    }

    [Fact]
    public async Task Un_administrador_no_puede_desactivarse_a_si_mismo()
    {
        var admin = await CrearUsuarioAsync(rol: Rol.Administrador);
        var token = await IniciarSesionAsync(admin.Correo, ContrasenaValida);

        var respuesta = await Cliente(token).PostAsJsonAsync("/usuarios/desactivar", new { usuarioId = admin.Id });

        // 422 y no 403: el que llama está autorizado, y es el único que puede hacer esto
        // a cualquiera. Lo que no puede es quitárselo a sí mismo.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);

        Assert.True((await UsuarioAsync(admin.Id)).Activo);

        // El rechazo no lo dejó fuera de la administración.
        Assert.Equal(HttpStatusCode.OK, (await Cliente(token).GetAsync("/usuarios")).StatusCode);
    }

    [Fact]
    public async Task Desactivar_dos_veces_da_error_de_regla()
    {
        var admin = Cliente(await TokenDeAdministradorAsync());
        var usuario = await CrearUsuarioAsync();

        await admin.PostAsJsonAsync("/usuarios/desactivar", new { usuarioId = usuario.Id });
        var segunda = await admin.PostAsJsonAsync("/usuarios/desactivar", new { usuarioId = usuario.Id });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, segunda.StatusCode);
    }

    [Fact]
    public async Task Reactivar_un_usuario_que_ya_esta_activo_da_error_de_regla()
    {
        var admin = Cliente(await TokenDeAdministradorAsync());
        var usuario = await CrearUsuarioAsync();

        var respuesta = await admin.PostAsJsonAsync("/usuarios/reactivar", new { usuarioId = usuario.Id });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
    }

    [Fact]
    public async Task Desactivar_o_reactivar_a_un_usuario_inexistente_da_404()
    {
        var cliente = Cliente(await TokenDeAdministradorAsync());

        Assert.Equal(HttpStatusCode.NotFound,
            (await cliente.PostAsJsonAsync("/usuarios/desactivar", new { usuarioId = Guid.NewGuid() })).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound,
            (await cliente.PostAsJsonAsync("/usuarios/reactivar", new { usuarioId = Guid.NewGuid() })).StatusCode);
    }

    // ---------------------------------------------------------------------------------------
    // RF-CA-06 y RD-06 — un Estándar recibe un rechazo explícito, y también con la
    // petición construida a mano, sin pasar por la interfaz.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Un_estandar_recibe_403_en_cada_operacion_de_administracion()
    {
        var token = await TokenDeEstandarAsync();

        foreach (var caso in CasosDeAdministracion)
        {
            var respuesta = await PeticionManualAsync(token, caso.Metodo, caso.Ruta, caso.Cuerpo);

            Assert.True(
                respuesta.StatusCode == HttpStatusCode.Forbidden,
                $"{caso.Metodo} {caso.Ruta} devolvió {(int)respuesta.StatusCode} en vez de 403.");
        }
    }

    [Fact]
    public async Task Sin_credencial_recibe_401_en_cada_operacion_de_administracion()
    {
        foreach (var caso in CasosDeAdministracion)
        {
            var respuesta = await PeticionManualAsync(null, caso.Metodo, caso.Ruta, caso.Cuerpo);

            Assert.True(
                respuesta.StatusCode == HttpStatusCode.Unauthorized,
                $"{caso.Metodo} {caso.Ruta} devolvió {(int)respuesta.StatusCode} en vez de 401.");
        }
    }

    [Fact]
    public async Task Una_credencial_cerrada_recibe_401_en_cada_operacion_de_administracion()
    {
        var usuario = await CrearUsuarioAsync(rol: Rol.Administrador);
        var token = await IniciarSesionAsync(usuario.Correo, ContrasenaValida);

        // El cierre de sesión responde 204: no hay nada que devolver.
        Assert.Equal(HttpStatusCode.NoContent,
            (await Cliente(token).PostAsJsonAsync("/sesion/cerrar", new { })).StatusCode);

        foreach (var caso in CasosDeAdministracion)
        {
            var respuesta = await PeticionManualAsync(token, caso.Metodo, caso.Ruta, caso.Cuerpo);

            Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        }
    }

    [Fact]
    public async Task Un_estandar_tampoco_puede_desactivar_ni_reactivar_a_otros()
    {
        var objetivo = await CrearUsuarioAsync(activo: false);
        var token = await TokenDeEstandarAsync();

        var desactivar = await PeticionManualAsync(token, HttpMethod.Post, "/usuarios/desactivar",
            $$"""{"usuarioId":"{{objetivo.Id}}"}""");
        var reactivar = await PeticionManualAsync(token, HttpMethod.Post, "/usuarios/reactivar",
            $$"""{"usuarioId":"{{objetivo.Id}}"}""");

        Assert.Equal(HttpStatusCode.Forbidden, desactivar.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, reactivar.StatusCode);

        // Ni una ni otra tocaron nada.
        Assert.False((await UsuarioAsync(objetivo.Id)).Activo);
    }

    // ---------------------------------------------------------------------------------------
    // Utilidades
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Construye la petición a mano, sin los ayudantes de la interfaz. Es el caso que
    /// RD-06 pide comprobar: nada de lo que hace el cliente puede formar parte de la
    /// protección.
    /// </summary>
    private async Task<HttpResponseMessage> PeticionManualAsync(
        string? token, HttpMethod metodo, string ruta, string cuerpo)
    {
        var peticion = new HttpRequestMessage(metodo, ruta);

        if (cuerpo.Length > 0)
        {
            peticion.Content = new StringContent(cuerpo, Encoding.UTF8, "application/json");
        }

        if (token is not null)
        {
            peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await _app.CreateClient().SendAsync(peticion);
    }

    private static string CorreoUnico(string marca, Rol rol) =>
        $"{marca}-{rol}-{Guid.NewGuid():N}@ejemplo.com";

    /// <summary>
    /// Inserta un usuario directamente. La contraseña se hashea con el mismo hasher que
    /// usa la aplicación, para que la prueba no dependa del endpoint de registro.
    /// </summary>
    private async Task<Usuario> CrearUsuarioAsync(
        bool activo = true,
        Rol rol = Rol.Estandar,
        DateTimeOffset? bloqueadoHasta = null,
        string marca = "prueba")
    {
        var usuario = new Usuario
        {
            Nombre = "Ana Ruiz",
            Correo = CorreoUnico(marca, rol),
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

    private async Task<string> TokenDeAdministradorAsync() =>
        await IniciarSesionAsync(
            (await CrearUsuarioAsync(rol: Rol.Administrador, marca: "sesion")).Correo, ContrasenaValida);

    private async Task<string> TokenDeEstandarAsync() =>
        await IniciarSesionAsync(
            (await CrearUsuarioAsync(rol: Rol.Estandar, marca: "sesion")).Correo, ContrasenaValida);

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

    private async Task<string> IniciarSesionAsync(string correo, string contrasena) =>
        await IntentarIniciarSesionAsync(correo, contrasena)
        ?? throw new InvalidOperationException(
            $"No se pudo iniciar sesión con {correo}. La prueba requiere un usuario activo.");

    private HttpClient Cliente(string token)
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

    /// <summary>
    /// Fila del listado tal como la ve el cliente, con el enum de rol ya convertido a
    /// texto. Es una proyección propia y no la entidad: si un día el listado devolviera
    /// la entidad, esta lectura fallaría al no encontrar estos campos.
    /// </summary>
    private async Task<List<Fila>> LeerListadoAsync(HttpClient cliente)
    {
        var respuesta = await cliente.GetAsync("/usuarios");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        return await respuesta.Content.ReadFromJsonAsync<List<Fila>>() ?? [];
    }

    /// <summary>
    /// Recorta el listado a las cuentas de la marca indicada, porque la base es
    /// compartida entre pruebas y no tiene sentido afirmar sobre usuarios ajenos.
    /// </summary>
    private static IEnumerable<Fila> Mios(List<Fila> listado, string marca) =>
        listado.Where(f => f.Correo.StartsWith($"{marca}-", StringComparison.Ordinal));

    private sealed record Fila(Guid Id, string Nombre, string Correo, string Rol, bool Activo);

    private sealed record Caso(HttpMethod Metodo, string Ruta, string Cuerpo);
}