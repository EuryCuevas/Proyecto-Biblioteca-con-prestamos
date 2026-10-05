using System.Net.Mail;
using Biblioteca.Identidad.Entidades;
using Biblioteca.Identidad.Password;
using Biblioteca.Identidad.Persistencia;
using Biblioteca.Nucleo.Configuracion;
using Biblioteca.Nucleo.Notificacion;
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
}

public sealed class ServicioDeContrasenas(
    IDbContextFactory<IdentidadDbContext> fabrica,
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