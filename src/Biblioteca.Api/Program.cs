using System.Text.Json.Serialization;
using Biblioteca.Api.ManiojoDeErrores;
using Biblioteca.Api.Seguridad;
using Biblioteca.Biblioteca.Persistencia;
using Biblioteca.Correo;
using Biblioteca.Correo.Persistencia;
using Biblioteca.Nucleo.Notificacion;
using Biblioteca.Identidad;
using Biblioteca.Identidad.Acceso;
using Biblioteca.Identidad.Autorizacion;
using Biblioteca.Identidad.Contrasenas;
using Biblioteca.Identidad.Entidades;
using Biblioteca.Identidad.Persistencia;
using Biblioteca.Identidad.Registro;
using Biblioteca.Identidad.Sesiones;
using Biblioteca.Nucleo.Auditoria;
using Biblioteca.Nucleo.Configuracion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// =============================================================================================
// Configuración. Las credenciales llegan por variables de entorno; nunca están en
// appsettings.json ni en el repositorio (RD-10, RF-NOT-13).
// =============================================================================================
builder.Services.Configure<OpcionesCorreo>(
    builder.Configuration.GetSection(OpcionesCorreo.Seccion));

// =============================================================================================
// Persistencia: un contexto por pieza, con el proveedor SQL Server registrado sólo en el
// host. Cada componente es dueño de sus propias tablas (RD-01, RD-03).
// =============================================================================================
var conexion = builder.Configuration.GetConnectionString("Biblioteca")
    ?? throw new InvalidOperationException(
        "Falta la cadena de conexión 'Biblioteca'. Defina la variable de entorno " +
        "ConnectionStrings__Biblioteca (RD-10).");

// Las MIGRACIONES viven en el host (Biblioteca.Api), no en las piezas. Motivo: una
// migración generada referencia extensiones del proveedor SQL Server, y si viviera
// en la pieza obligaría a que el dominio dependiera del proveedor. Con las migraciones
// en el host, las tres piezas siguen siendo autónomas y sin proveedor (RD-01).
builder.Services.AddDbContextFactory<IdentidadDbContext>(o => o.UseSqlServer(conexion, sql =>
    sql.MigrationsAssembly(typeof(Program).Assembly.FullName)));

builder.Services.AddDbContextFactory<CorreoDbContext>(o => o.UseSqlServer(conexion, sql =>
    sql.MigrationsAssembly(typeof(Program).Assembly.FullName)));

builder.Services.AddDbContextFactory<BibliotecaDbContext>(o => o.UseSqlServer(conexion, sql =>
    sql.MigrationsAssembly(typeof(Program).Assembly.FullName)));

// Un único reloj para todo el sistema (RD-11). Inyectarlo hace que los plazos de
// 15 minutos y las vigencias sean comprobables con un reloj simulado en las pruebas.
builder.Services.AddSingleton(TimeProvider.System);

// =============================================================================================
// Sesiones: la pieza es dueña de la lógica (crear, cerrar, revocar, resolver). El host
// sólo traduce HTTP en llamadas a este servicio (RD-02).
// =============================================================================================
builder.Services.AddScoped<IServicioDeSesiones, ServicioDeSesiones>();

// =============================================================================================
// Registro y activación (RF-CA-01, 02, 14, 15, 16, 17). La pieza decide; el host traduce.
// =============================================================================================
builder.Services.AddScoped<IServicioDeRegistro, ServicioDeRegistro>();

// =============================================================================================
// Acceso: decide si una pareja de credenciales abre sesión, y con qué restricciones
// (cuenta activa, bloqueo por intentos). Va aparte de las sesiones porque aquél da de
// baja el ciclo de vida de una credencial ya emitida, y esto no sabe nada de eso
// (RF-CA-03, RF-CA-07, RF-CA-18, RF-CA-19, RF-CA-15).
// =============================================================================================
builder.Services.AddScoped<IServicioDeAcceso, ServicioDeAcceso>();

// Contraseñas: recuperación, restablecimiento y cambio propio. Va aparte del acceso
// porque no se ocupa de abrir sesión, sino de lo que hace un usuario que ya no puede
// (RF-CA-09, RF-CA-10).
builder.Services.AddScoped<IServicioDeContrasenas, ServicioDeContrasenas>();

// El hash de contraseña se inyecta, no se instancia dentro del servicio, para que las
// pruebas puedan sustituirlo por uno rápido. El algoritmo por defecto es PBKDF2 con sal
// propia del usuario: es el que garantiza RF-CA-02 sin que escribamos criptografía a mano.
builder.Services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();

// =============================================================================================
// Correo: se registra únicamente el puerto. La implementación escribe en la cola y NO
// abre conexión SMTP durante una operación de negocio (RF-NOT-08).
// =============================================================================================
builder.Services.AddScoped<IEncolaCorreo, EncolaCorreo>();
builder.Services.AddScoped<SmtpEntregador>();

// =============================================================================================
// Auditoría: en la Práctica 1 no se persiste; se califica en la semana 14. El puerto ya
// existe para que las piezas publiquen entradas sin acoplarse a la pieza 6.
// =============================================================================================
builder.Services.AddSingleton<IPublicaAuditoria, SumideroDeAuditoria>();

// =============================================================================================
// Autorización: la exigencia de rol vive ÚNICAMENTE en PoliticaDeOperaciones (RF-CA-05).
// =============================================================================================
builder.Services.AddSingleton<IAuthorizationHandler, RequisitoDeOperacionHandler>();

builder.Services.AddAuthentication(EsquemaAutenticacion.Nombre)
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
               AutenticacionPorSesion>(EsquemaAutenticacion.Nombre, _ => { });

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .AddAuthenticationSchemes(EsquemaAutenticacion.Nombre)
        // Este requisito es lo que hace que RequisitoDeOperacionHandler se ejecute en
        // TODA acción que no sea [AllowAnonymous]. ASP.NET sólo invoca un handler si
        // alguna política le pide su requisito, y sin esta línea ninguna lo pedía: el
        // handler quedaba registrado y muerto, y la exigencia de rol de RF-CA-05 no
        // se comprobaba en ninguna petición. El guard de arranque declaraba la
        // operación y la política la conocía, pero nadie la evaluaba.
        .AddRequirements(new RequisitoDeOperacion())
        .Build());

builder.Services.AddControllers(opciones =>
{
    // Si alguna acción no declara [RequiereOperacion], la aplicación no arranca (RF-CA-05).
    opciones.Conventions.Add(new ValidarOperacionesDeclaradasConvention());
});

builder.Services.ConfigureHttpJsonOptions(opciones =>
    opciones.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

// Aplica las migraciones de las tres piezas al arrancar.
await Biblioteca.Api.ExtensionesDeMigracion.AplicarMigracionesAsync(app.Services);

// Traduce los rechazos controlados a respuestas HTTP limpias, sin filtrar internals (RD-08).
app.UseMiddleware<ManejadorDeExcepciones>();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

/// <summary>Expuesto para las pruebas de integración con WebApplicationFactory.</summary>
public partial class Program;