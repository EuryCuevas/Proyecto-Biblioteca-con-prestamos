using System.Net.Mail;
using Biblioteca.Identidad.Entidades;
using Biblioteca.Identidad.Password;
using Biblioteca.Identidad.Persistencia;
using Biblioteca.Nucleo.Configuracion;
using Biblioteca.Nucleo.Errores;
using Biblioteca.Nucleo.Notificacion;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Biblioteca.Identidad.Registro;

/// <summary>
/// Alta de cuentas y activación por enlace (RF-CA-01, 02, 14, 15, 16, 17).
///
/// El servicio es dueño de las reglas; el host sólo traduce HTTP (RD-02). El correo
/// nunca se envía aquí: se encola, y un proceso aparte lo entrega (RF-NOT-08).
/// </summary>
public interface IServicioDeRegistro
{
    /// <summary>
    /// Crea la cuenta inactiva y encola el enlace de activación.
    /// </summary>
    /// <param name="urlBase">
    /// Base pública donde vive el enlace, por ejemplo <c>http://localhost:5000</c>. La
    /// pasa el host porque es quien conoce el esquema y el puerto reales; así el enlace
    /// es correcto en cualquier entorno sin que haya que configurarlo a mano.
    /// </param>
    Task<Usuario> RegistrarAsync(
        string nombre,
        string correo,
        string contrasena,
        string urlBase,
        CancellationToken cancelacion = default);

    /// <summary>Abre el enlace: marca el token como usado y activa la cuenta (RF-CA-16).</summary>
    Task ActivarAsync(string token, CancellationToken cancelacion = default);

    /// <summary>
    /// Reenvía el enlace de activación (RF-CA-17). La respuesta es idéntica exista o no
    /// el correo: el método no revela qué correos están registrados.
    /// </summary>
    Task ReenviarActivacionAsync(
        string correo,
        string urlBase,
        CancellationToken cancelacion = default);
}

public sealed class ServicioDeRegistro(
    IDbContextFactory<IdentidadDbContext> fabrica,
    IPasswordHasher<Usuario> hasher,
    IEncolaCorreo encola,
    TimeProvider reloj,
    IOptions<OpcionesCorreo> opciones) : IServicioDeRegistro
{
    private readonly OpcionesCorreo _opciones = opciones.Value;

    public async Task<Usuario> RegistrarAsync(
        string nombre,
        string correo,
        string contrasena,
        string urlBase,
        CancellationToken cancelacion = default)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw ExcepcionDominio.Validacion("nombre.invalido", "El nombre es obligatorio.");
        }

        var correoNormalizado = NormalizarCorreo(correo);

        // La política se comprueba ANTES de tocar la base: una contraseña inválida no
        // deja ni un usuario a medias (RF-CA-14, RD-07).
        var problema = PoliticaDeContrasenas.Validar(contrasena);
        if (problema is not null)
        {
            throw ExcepcionDominio.Validacion("contrasena.invalida", problema);
        }

        await using var db = await fabrica.CreateDbContextAsync(cancelacion);

        var yaExiste = await db.Usuarios
            .AsNoTracking()
            .AnyAsync(u => u.Correo == correoNormalizado, cancelacion);

        if (yaExiste)
        {
            throw ExcepcionDominio.Conflicto(
                "correo.duplicado", "Ese correo ya está registrado.");
        }

        var ahora = reloj.GetUtcNow();

        var usuario = new Usuario
        {
            Nombre = nombre.Trim(),
            Correo = correoNormalizado,
            // RF-CA-15: la cuenta nace inactiva. Nadie puede iniciar sesión hasta abrir
            // el enlace, por mucho que conozca la contraseña.
            Activo = false,
            Rol = Rol.Estandar,
            CreadoEn = ahora,
            PasswordCambiadoEn = ahora
        };

        // RF-CA-02: hash con sal propia del usuario. El valor guardado no coincide con
        // la contraseña y dos usuarios con la misma contraseña no comparten valor.
        usuario.PasswordHash = hasher.HashPassword(usuario, contrasena);

        var token = GeneradorDeSecretos.NuevoToken();

        usuario.TokensActivacion.Add(new TokenActivacion
        {
            UsuarioId = usuario.Id,
            // Sólo se persiste el hash. El valor en claro viaja en el correo y no
            // vuelve a existir en el servidor.
            TokenHash = GeneradorDeSecretos.Hash(token),
            EmitidoEn = ahora,
            VenceEn = ahora.AddMinutes(_opciones.MinutosValidezToken)
        });

        db.Usuarios.Add(usuario);

        try
        {
            await db.SaveChangesAsync(cancelacion);
        }
        catch (DbUpdateException)
        {
            // Dos registros simultáneos con el mismo correo pasan ambos la comprobación
            // anterior, y el índice único UQ_Usuario_Correo rechaza al segundo. Se traduce
            // al MISMO rechazo, no a un fallo interno (RF-CA-01).
            if (await YaExisteAsync(db, correoNormalizado, cancelacion))
            {
                throw ExcepcionDominio.Conflicto(
                    "correo.duplicado", "Ese correo ya está registrado.");
            }

            throw;
        }

        await encola.EncolarAsync(
            usuario.Correo,
            "Activa tu cuenta de la Biblioteca",
            CuerpoDeActivacion(usuario.Nombre, token, urlBase),
            plantilla: "activacion",
            cancelacion: cancelacion);

        return usuario;
    }

    public async Task ActivarAsync(string token, CancellationToken cancelacion = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw ExcepcionDominio.NoEncontrado(
                "activacion.enlace_invalido", "El enlace no es válido.");
        }

        var hash = GeneradorDeSecretos.Hash(token);
        var ahora = reloj.GetUtcNow();

        await using var db = await fabrica.CreateDbContextAsync(cancelacion);

        var registro = await db.TokensActivacion
            .Include(t => t.Usuario)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancelacion);

        if (registro is null)
        {
            throw ExcepcionDominio.NoEncontrado(
                "activacion.enlace_invalido", "El enlace no es válido.");
        }

        // Un enlace ya usado o vencido se rechaza SIN tocar el estado (RF-CA-16).
        if (!registro.EstaVigente(ahora))
        {
            throw ExcepcionDominio.ReglaDeNegocio(
                "activacion.enlace_no_vigente",
                "El enlace ya se usó o ha vencido. Pide uno nuevo.");
        }

        registro.UsadoEn = ahora;
        registro.Usuario.Activo = true;

        await db.SaveChangesAsync(cancelacion);
    }

    public async Task ReenviarActivacionAsync(
        string correo,
        string urlBase,
        CancellationToken cancelacion = default)
    {
        var correoNormalizado = NormalizarCorreo(correo);

        await using var db = await fabrica.CreateDbContextAsync(cancelacion);

        var usuario = await db.Usuarios
            .Include(u => u.TokensActivacion)
            .FirstOrDefaultAsync(u => u.Correo == correoNormalizado, cancelacion);

        // Si el correo no existe, o si la cuenta ya está activa, no se hace nada y no se
        // dice nada. Quien llama obtiene siempre la misma respuesta (RF-CA-17).
        if (usuario is null || usuario.Activo)
        {
            return;
        }

        var ahora = reloj.GetUtcNow();

        // El reenvío invalida el enlace anterior: pasa a usado, no a vigente (RF-CA-17).
        foreach (var anterior in usuario.TokensActivacion.Where(t => t.UsadoEn is null))
        {
            anterior.UsadoEn = ahora;
        }

        var token = GeneradorDeSecretos.NuevoToken();

        // Se añade POR EL DbSet, no por la colección de navegación: el usuario ya está
        // rastreado, y sobre un grafo rastreado añadir a la colección no marca la entidad
        // como insertable — su Id es un Guid ya asignado en memoria, así que EF no puede
        // deducir que es nueva y la trata como modificación de una fila que no existe.
        db.TokensActivacion.Add(new TokenActivacion
        {
            UsuarioId = usuario.Id,
            TokenHash = GeneradorDeSecretos.Hash(token),
            EmitidoEn = ahora,
            VenceEn = ahora.AddMinutes(_opciones.MinutosValidezToken)
        });

        await db.SaveChangesAsync(cancelacion);

        await encola.EncolarAsync(
            usuario.Correo,
            "Activa tu cuenta de la Biblioteca",
            CuerpoDeActivacion(usuario.Nombre, token, urlBase),
            plantilla: "activacion",
            cancelacion: cancelacion);
    }

    /// <summary>
    /// Un correo vacío o mal formado es un rechazo controlado, nunca una excepción sin
    /// manejar (RD-07).
    /// </summary>
    private static string NormalizarCorreo(string correo)
    {
        if (string.IsNullOrWhiteSpace(correo) || !MailAddress.TryCreate(correo.Trim(), out var parsed))
        {
            throw ExcepcionDominio.Validacion("correo.invalido", "El correo no es válido.");
        }

        return parsed.Address.ToLowerInvariant();
    }

    private static Task<bool> YaExisteAsync(
        IdentidadDbContext db,
        string correo,
        CancellationToken cancelacion) =>
        db.Usuarios.AsNoTracking().AnyAsync(u => u.Correo == correo, cancelacion);

    private string CuerpoDeActivacion(string nombre, string token, string urlBase)
    {
        var enlace = $"{urlBase.TrimEnd('/')}/usuarios/activar"
                   + $"?token={Uri.EscapeDataString(token)}";

        return $"""
                Hola {nombre}:

                Confirma tu cuenta abriendo este enlace:

                {enlace}

                El enlace vence en {_opciones.MinutosValidezToken} minutos y sólo sirve una vez.
                Si no lo abres, puedes pedir otro desde la página de acceso.
                """;
    }
}