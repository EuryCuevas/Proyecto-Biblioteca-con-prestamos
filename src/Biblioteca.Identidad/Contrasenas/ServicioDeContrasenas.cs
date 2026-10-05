using System.Net.Mail;
using Biblioteca.Identidad.Entidades;
using Biblioteca.Identidad.Password;
using Biblioteca.Identidad.Persistencia;
using Biblioteca.Identidad.Sesiones;
using Biblioteca.Nucleo.Configuracion;
using Biblioteca.Nucleo.Errores;
using Biblioteca.Nucleo.Notificacion;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Biblioteca.Identidad.Contrasenas;

/// <summary>
/// Recuperación, restablecimiento y cambio de contraseña
/// (RF-CA-09, 10, 11, 12, 13 y 22).
///
/// Es una pieza distinta de <see cref="Acceso.IServicioDeAcceso"/> y de
/// <see cref="Sesiones.IServicioDeSesiones"/>: aquéllos deciden si una pareja de
/// credenciales abre sesión y dan de baja el ciclo de vida de una sesión emitida.
/// Esta decide qué hacer cuando el usuario ya no puede autenticarse, o cuando quiere
/// rotar su contraseña sin quedarse fuera a sí mismo.
///
/// Como el registro y el acceso, el correo nunca se envía aquí: se encola y lo
/// entrega un proceso aparte (RF-NOT-08).
/// </summary>
public interface IServicioDeContrasenas
{
    /// <summary>
    /// Emite un código de recuperación de un solo uso y lo encola al correo del
    /// usuario (RF-CA-10).
    ///
    /// Si el correo no está registrado, está mal formado, o la cuenta sigue inactiva,
    /// el método no encola nada y no dice nada. Quien llama obtiene siempre la misma
    /// respuesta (RF-CA-09): el flujo no revela qué correos están registrados.
    /// </summary>
    /// <param name="urlBase">
    /// Base pública donde vive el enlace, por ejemplo <c>http://localhost:5000</c>. La
    /// pasa el host porque es quien conoce el esquema y el puerto reales, igual que en
    /// el alta de cuentas.
    /// </param>
    Task IniciarRecuperacionAsync(
        string correo,
        string urlBase,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Dice si un código sigue sirviendo, SIN consumirlo (RF-CA-10).
    ///
    /// Es lo que responde al enlace del correo: le dice al usuario si tiene que
    /// pedir uno nuevo, sin gastarlo. No revelar nada aquí ya no importa: el código
    /// es el secreto, con 256 bits de entropía.
    /// </summary>
    /// <exception cref="ExcepcionDominio">
    /// <see cref="TipoError.NoEncontrado"/> si el código no existe, o
    /// <see cref="TipoError.ReglaDeNegocio"/> si ya se usó o venció.
    /// </exception>
    Task ComprobarCodigoAsync(string codigo, CancellationToken cancelacion = default);

    /// <summary>
    /// Define una contraseña nueva con un código de recuperación válido
    /// (RF-CA-10, 11, 12).
    ///
    /// Un código ya usado o vencido se rechaza sin tocar nada: la contraseña no
    /// cambia (RF-CA-10). Con un código válido, la nueva contraseña se guarda
    /// hasheada y la anterior deja de servir (RF-CA-11). Además se cierran todas las
    /// sesiones abiertas del usuario, incluidas las que ya tuvieran (RF-CA-12).
    /// </summary>
    /// <param name="codigo">
    /// El valor en claro que llegó por correo. En la base sólo está su hash.
    /// </param>
    Task RestablecerConCodigoAsync(
        string codigo,
        string contrasenaNueva,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Restablece la contraseña de un usuario desde la administración (RF-CA-13).
    ///
    /// La contraseña anterior deja de servir en el acto y las sesiones abiertas se
    /// cierran. Después se emite un código nuevo y se encola al correo registrado,
    /// que es lo que le permitirá al usuario definir una contraseña.
    /// </summary>
    /// <remarks>
    /// Esta operación no lleva la cuenta a un estado "pendiente de restablecer": el
    /// sistema no tiene ninguno. Lo que hace es dejar la contraseña en un valor que
    /// nadie conoce, que es exactamente lo que exige RF-CA-13.
    /// </remarks>
    /// <exception cref="ExcepcionDominio">
    /// <see cref="TipoError.NoEncontrado"/> si no existe ese usuario.
    /// </exception>
    Task ForzarRestablecimientoAsync(
        Guid usuarioId,
        string urlBase,
        CancellationToken cancelacion = default);
}

public sealed class ServicioDeContrasenas(
    IDbContextFactory<IdentidadDbContext> fabrica,
    IPasswordHasher<Usuario> hasher,
    IServicioDeSesiones sesiones,
    IEncolaCorreo encola,
    TimeProvider reloj,
    IOptions<OpcionesCorreo> opciones) : IServicioDeContrasenas
{
    private readonly OpcionesCorreo _opciones = opciones.Value;

    public async Task IniciarRecuperacionAsync(
        string correo,
        string urlBase,
        CancellationToken cancelacion = default)
    {
        var canonico = CanonicalizarCorreo(correo);

        await using var db = await fabrica.CreateDbContextAsync(cancelacion);

        var usuario = canonico is null
            ? null
            : await db.Usuarios.FirstOrDefaultAsync(u => u.Correo == canonico, cancelacion);

        // Una cuenta inactiva no puede iniciar sesión (RF-CA-15), así que un código de
        // recuperación no le serviría de nada: es mejor no enviárselo. La respuesta es la
        // misma que si el correo no existiera, de modo que esta decisión tampoco se
        // puede leer desde fuera (RF-CA-09).
        if (usuario is null || !usuario.Activo)
        {
            return;
        }

        await EmitirCodigoAsync(db, usuario, urlBase, cancelacion);
    }

    public async Task ComprobarCodigoAsync(
        string codigo,
        CancellationToken cancelacion = default) =>
        await BuscarCodigoVigenteAsync(codigo, cancelacion);

    public async Task RestablecerConCodigoAsync(
        string codigo,
        string contrasenaNueva,
        CancellationToken cancelacion = default)
    {
        // La política se comprueba ANTES de tocar la base: una contraseña que no
        // cumple no puede gastar el código (RF-CA-14, RD-07).
        var problema = PoliticaDeContrasenas.Validar(contrasenaNueva);
        if (problema is not null)
        {
            throw ExcepcionDominio.Validacion("contrasena.invalida", problema);
        }

        await using var db = await fabrica.CreateDbContextAsync(cancelacion);

        var registro = await BuscarCodigoVigenteAsync(codigo, cancelacion, db);

        registro.UsadoEn = reloj.GetUtcNow();

        await SustituirContrasenaYCerrarSesionesAsync(
            db, registro.Usuario, contrasenaNueva, "Recuperación de contraseña.", cancelacion);
    }

    /// <summary>
    /// Devuelve el código vigente, o lanza el rechazo que corresponda.
    ///
    /// Un código inexistente y un código ya usado o vencido NO se distinguen entre sí
    /// más de lo imprescindible, pero sí del correo: quien llama ya tiene el código
    /// en la mano, así que no hay nada que proteger (RF-CA-10).
    /// </summary>
    private async Task<CodigoRecuperacion> BuscarCodigoVigenteAsync(
        string codigo,
        CancellationToken cancelacion,
        IdentidadDbContext? contexto = null)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            throw ExcepcionDominio.NoEncontrado(
                "recuperacion.codigo_invalido", "El código no es válido.");
        }

        var db = contexto ?? await fabrica.CreateDbContextAsync(cancelacion);
        var propio = contexto is null;

        try
        {
            var registro = await db.CodigosRecuperacion
                .Include(c => c.Usuario)
                .FirstOrDefaultAsync(c => c.CodigoHash == GeneradorDeSecretos.Hash(codigo), cancelacion);

            if (registro is null)
            {
                throw ExcepcionDominio.NoEncontrado(
                    "recuperacion.codigo_invalido", "El código no es válido.");
            }

            if (!registro.EstaVigente(reloj.GetUtcNow()))
            {
                throw ExcepcionDominio.ReglaDeNegocio(
                    "recuperacion.codigo_no_vigente",
                    "El código ya se usó o ha vencido. Pide uno nuevo.");
            }

            return registro;
        }
        finally
        {
            if (propio)
            {
                await db.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Deja la contraseña nueva hasheada y deja al usuario sin ninguna sesión abierta
    /// (RF-CA-11, RF-CA-12). La comparten los tres caminos que cambian una contraseña.
    /// </summary>
    /// <param name="nuevaContrasena">
    /// La contraseña que se quiere dejar. En el restablecimiento forzado por
    /// Administrador (RF-CA-13) no es una contraseña real: es un valor aleatorio que
    /// nadie conoce, y su único papel es que la anterior deje de servir.
    /// </param>
    private async Task SustituirContrasenaYCerrarSesionesAsync(
        IdentidadDbContext db,
        Usuario usuario,
        string nuevaContrasena,
        string motivo,
        CancellationToken cancelacion)
    {
        var ahora = reloj.GetUtcNow();

        // RF-CA-11: la nueva se guarda con hash y la anterior deja de servir, porque
        // es lo único que se compara al iniciar sesión.
        usuario.PasswordHash = hasher.HashPassword(usuario, nuevaContrasena);
        usuario.PasswordCambiadoEn = ahora;

        // Quien llega hasta aquí ha probado que controla su correo (tiene el código) o
        // que conoce su contraseña actual (RF-CA-22). Las dos cosas son pruebas de
        // identidad más fuertes que un intento de acceso, así que el bloqueo por
        // intentos se levanta: de lo contrario, un usuario bloqueado que recupera su
        // contraseña seguiría sin poder entrar hasta que venciera el bloqueo.
        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;

        // El orden importa y es deliberado. Las sesiones se cierran ANTES de guardar
        // la contraseña nueva: si el proceso se cae entre los dos pasos, el peor
        // desenlace es que las credenciales viejas ya no sirven y el usuario tenga que
        // entrar otra vez. Al revés, un fallo dejaría credenciales activas junto a una
        // contraseña nueva, que es justo el escenario que RF-CA-12 viene a cerrar.
        await sesiones.CerrarTodasAsync(usuario.Id, motivo, db, cancelacion);

        await db.SaveChangesAsync(cancelacion);
    }

    public async Task ForzarRestablecimientoAsync(
        Guid usuarioId,
        string urlBase,
        CancellationToken cancelacion = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancelacion);

        var usuario = await db.Usuarios
            .FirstOrDefaultAsync(u => u.Id == usuarioId, cancelacion)
            ?? throw ExcepcionDominio.NoEncontrado(
                "usuario.no_encontrado", "Usuario no encontrado.");

        // La contraseña se sustituye por un valor aleatorio de 256 bits que no se
        // guarda en ningun sitio y que nadie, tampoco el Administrador, conoce. Es la
        // unica forma de que "la contrasena anterior deja de servir" sin dejar al
        // usuario con una contrasena inventada por el sistema que luego tendria que
        // cambiar (RF-CA-13).
        await SustituirContrasenaYCerrarSesionesAsync(
            db, usuario, GeneradorDeSecretos.NuevoToken(), "Restablecimiento forzado por un Administrador.", cancelacion);

        // El codigo se emite DESPUES de cerrar las sesiones. Si el encolado del correo
        // fallara, el usuario se quedaria sin poder entrar y sin saber por que, que es
        // el peor de los dos desordenes posibles.
        await EmitirCodigoAsync(db, usuario, urlBase, cancelacion);
    }

    /// <summary>
    /// Da de baja los códigos pendientes del usuario y encola uno nuevo. Sólo el
    /// último código emitido sirve: pedir la recuperación dos veces invalida el
    /// primero (RF-CA-10).
    /// </summary>
    private async Task EmitirCodigoAsync(
        IdentidadDbContext db,
        Usuario usuario,
        string urlBase,
        CancellationToken cancelacion)
    {
        var ahora = reloj.GetUtcNow();

        await db.CodigosRecuperacion
            .Where(c => c.UsuarioId == usuario.Id && c.UsadoEn == null)
            .ExecuteUpdateAsync(c => c.SetProperty(x => x.UsadoEn, ahora), cancelacion);

        var codigo = GeneradorDeSecretos.NuevoToken();

        // Se añade POR EL DbSet, no por la colección de navegación: el usuario ya está
        // rastreado, y sobre un grafo rastreado añadir a la colección no marca la entidad
        // como insertable — su Id es un Guid ya asignado en memoria, así que EF no puede
        // deducir que es nueva y la trata como modificación de una fila que no existe.
        db.CodigosRecuperacion.Add(new CodigoRecuperacion
        {
            UsuarioId = usuario.Id,
            // Sólo se persiste el hash. El valor en claro viaja en el correo y no
            // vuelve a existir en el servidor.
            CodigoHash = GeneradorDeSecretos.Hash(codigo),
            EmitidoEn = ahora,
            VenceEn = ahora.AddMinutes(_opciones.MinutosValidezToken)
        });

        await db.SaveChangesAsync(cancelacion);

        await encola.EncolarAsync(
            usuario.Correo,
            "Recupera tu contraseña de la Biblioteca",
            CuerpoDeRecuperacion(usuario.Nombre, codigo, urlBase),
            plantilla: "recuperacion",
            cancelacion: cancelacion);
    }

    /// <summary>
    /// Devuelve el correo en la misma forma en que se guardó, o <see langword="null"/>
    /// si no es un correo válido.
    ///
    /// Aquí NO se lanza un error de validación, a diferencia del registro. En la
    /// recuperación todo se responde igual (RF-CA-09): un correo mal formado no puede
    /// coincidir con ninguna cuenta, así que distinguirlodel resto no aportaría nada
    /// al usuario y sí revelaría qué formato espera el sistema.
    /// </summary>
    private static string? CanonicalizarCorreo(string correo) =>
        !string.IsNullOrWhiteSpace(correo)
        && MailAddress.TryCreate(correo.Trim(), out var parsed)
            ? parsed.Address.ToLowerInvariant()
            : null;

    private string CuerpoDeRecuperacion(string nombre, string codigo, string urlBase)
    {
        var enlace = $"{urlBase.TrimEnd('/')}/contrasenas/recuperacion"
                   + $"?codigo={Uri.EscapeDataString(codigo)}";

        return $"""
                Hola {nombre}:

                Abre este enlace para comprobar tu código:

                {enlace}

                Si el enlace no te sirve, usa este código en POST /contrasenas/restablecer
                junto con la contraseña nueva que quieras dejar:

                {codigo}

                El código vence en {_opciones.MinutosValidezToken} minutos y sólo sirve una vez.
                Si no lo pediste, puedes ignorar este mensaje: tu contraseña no cambia.
                """;
    }
}