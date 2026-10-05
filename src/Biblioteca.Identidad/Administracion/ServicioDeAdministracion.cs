using Biblioteca.Identidad.Entidades;
using Biblioteca.Identidad.Persistencia;
using Biblioteca.Identidad.Sesiones;
using Biblioteca.Nucleo.Errores;
using Microsoft.EntityFrameworkCore;

namespace Biblioteca.Identidad.Administracion;

/// <summary>
/// Administración de usuarios (RF-CA-04, 06, 08, 20 y 21).
///
/// Es una pieza distinta de <see cref="Registro.IServicioDeRegistro"/>,
/// <see cref="Acceso.IServicioDeAcceso"/> y <see cref="Contrasenas.IServicioDeContrasenas"/>:
/// aquéllos tratan a un usuario consigo mismo —su alta, su contraseña, su sesión— y ésta
/// trata a un Administrador tratando con los demás.
///
/// No comprueba que quien llama sea Administrador, y no es un descuido: la exigencia de
/// rol vive en un solo punto, <see cref="PoliticaDeOperaciones"/>, y el endpoint nombra su
/// operación con <see cref="Autorizacion.RequiereOperacionAttribute"/>. Un método que
/// añadiera su propia comprobación dejaría dos verdades que pueden desincronizarse
/// (RF-CA-05, RF-CA-06).
/// </summary>
public interface IServicioDeAdministracion
{
    /// <summary>
    /// Lista los usuarios con su rol y su estado (RF-CA-21).
    ///
    /// Devuelve <see cref="ResumenDeUsuario"/>, no <see cref="Usuario"/>. El motivo es
    /// que la entidad lleva dentro <c>PasswordHash</c>, y que un listado que devuelve la
    /// entidad es un listado a un campo de distância de filtrar por descuido.
    /// </summary>
    Task<IReadOnlyList<ResumenDeUsuario>> ListarAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Cambia el rol de un usuario, que es exactamente el que tiene: uno solo
    /// (RF-CA-04, RF-CA-08).
    ///
    /// El nuevo rol sustituye al anterior, no se añade al lado. Con eso "todo usuario
    /// tiene exactamente un rol asignado" no es una promesa del código sino una
    /// consecuencia de la forma de la operación: no existe el camino que deje dos.
    /// </summary>
    /// <remarks>
    /// Cerrar las sesiones abiertas del usuario no es un efecto secundario opcional. El rol viaja dentro de la credencial, congelado en el claim
    /// <c>ClaimTypes.Role</c> que se emitió al iniciar sesión. Si no se cerraran:
    /// un Estándar ascendido a Administrador seguiría recibiendo 403 en
    /// <c>GET /usuarios</c> hasta que venciera su credencial, y —en sentido contrario, que
    /// es el que importa— un Administrador degradado a Estándar conservaría el acceso
    /// a las operaciones de administración hasta que venciera su credencial.
    /// </remarks>
    /// <exception cref="ExcepcionDominio">
    /// <see cref="TipoError.NoEncontrado"/> si no existe ese usuario;
    /// <see cref="TipoError.ReglaDeNegocio"/> si ya tiene ese rol.
    /// </exception>
    Task<CambioDeRol> CambiarRolAsync(
        Guid usuarioId,
        Rol nuevoRol,
        CancellationToken cancelacion = default);
}

/// <summary>
/// Resultado de <c>POST /usuarios/cambiar-rol</c> (RF-CA-08).
///
/// Se devuelve el número de sesiones cerradas porque no es un dato interno: quien
/// administra necesita poder decir "le he cerrado la sesión" sin tener que deducirlo,
/// y la revisión manual comprueba justo eso —desactivar a alguien con la sesión
/// abierta y probarla después.
/// </summary>
public sealed record CambioDeRol(
    Guid UsuarioId,
    Rol RolAnterior,
    Rol RolNuevo,
    int SesionesCerradas);

/// <summary>
/// Fila de <c>GET /usuarios</c> (RF-CA-21).
///
/// Los campos son los que el criterio nombra —rol y estado— más los que un Administrador
/// necesita para reconocer a quién está mirando. Deliberadamente NO hay ningún campo
/// <c>*Hash</c>, <c>*Token</c> ni <c>*Codigo*</c>: no están en la entidad que se proyecta,
/// así que no se pueden filtrar ni por accidente.
/// </summary>
/// <remarks>
/// Un <c>record</c> y no una clase: no se le puede añadir una propiedad en caliente desde
/// fuera, y el compilador obliga a actualizar el constructor cuando se toca la proyección.
/// </remarks>
public sealed record ResumenDeUsuario(
    Guid Id,
    string Nombre,
    string Correo,
    Rol Rol,
    bool Activo,
    DateTimeOffset CreadoEn,
    DateTimeOffset? PasswordCambiadoEn);

public sealed class ServicioDeAdministracion(
    IDbContextFactory<IdentidadDbContext> fabrica,
    IServicioDeSesiones sesiones) : IServicioDeAdministracion
{
    public async Task<IReadOnlyList<ResumenDeUsuario>> ListarAsync(
        CancellationToken cancelacion = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancelacion);

        // Se proyecta a ResumenDeUsuario en la consulta, no en memoria: PasswordHash viaja
        // desde la base hasta el proceso aunque el listado no lo devuelva. La proyección la
        // deja en el servidor (RF-CA-21).
        return await db.Usuarios
            .AsNoTracking()
            .OrderBy(u => u.Correo)
            .Select(u => new ResumenDeUsuario(
                u.Id,
                u.Nombre,
                u.Correo,
                u.Rol,
                u.Activo,
                u.CreadoEn,
                u.PasswordCambiadoEn))
            .ToListAsync(cancelacion);
    }

    public async Task<CambioDeRol> CambiarRolAsync(
        Guid usuarioId,
        Rol nuevoRol,
        CancellationToken cancelacion = default)
    {
        // El model binding acepta cualquier entero en un enum si el tipo lo admite. Sin
        // esta comprobación, un cuerpo {"rol": 7} crearía un usuario con un rol que
        // no está en PoliticaDeOperaciones, y ese usuario no podría ejecutar nada sin
        // que nadie entienda por qué. Se rechaza como regla de negocio, no como error
        // de forma: la petición está bien formada, el valor no existe (RF-CA-04).
        if (!Enum.IsDefined(nuevoRol))
        {
            throw ExcepcionDominio.ReglaDeNegocio(
                "usuario.rol_invalido", "El rol indicado no existe.");
        }

        await using var db = await fabrica.CreateDbContextAsync(cancelacion);

        var usuario = await db.Usuarios
            .FirstOrDefaultAsync(u => u.Id == usuarioId, cancelacion)
            ?? throw ExcepcionDominio.NoEncontrado(
                "usuario.no_encontrado", "Usuario no encontrado.");

        var anterior = usuario.Rol;
        if (anterior == nuevoRol)
        {
            // No es un no-op inocuo: cerrar las sesiones aquí expulsaría al usuario sin
            // que su situación haya cambiado. Se dice que no hay nada que hacer.
            throw ExcepcionDominio.ReglaDeNegocio(
                "usuario.rol_ya_asignado", "El usuario ya tiene ese rol.");
        }

        usuario.Rol = nuevoRol;

        // El mismo orden que al cambiar una contraseña (RF-CA-12): las sesiones se
        // cierran ANTES de guardar. Si el proceso se cae entre los dos pasos, el peor
        // desenlace es que el rol nuevo no se aplique; al revés, un fallo dejaría
        // credenciales con el rol viejo junto a un rol nuevo en la base, que es la
        // dirección en la que esto es un agujero de seguridad.
        var cerradas = await sesiones.CerrarTodasAsync(
            usuario.Id,
            "Cambio de rol.",
            db,
            cancelacion);

        await db.SaveChangesAsync(cancelacion);

        return new CambioDeRol(usuario.Id, anterior, nuevoRol, cerradas);
    }
}