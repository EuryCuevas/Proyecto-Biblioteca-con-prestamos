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
    /// Cerrar las sesiones abiertas del usuario no es un efecto secundario opcional.
    /// El rol viaja dentro de la credencial, congelado en el claim
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

    /// <summary>
    /// Desactiva una cuenta y le cierra las sesiones abiertas (RF-CA-20).
    ///
    /// Es la operación de la que el criterio se ocupa con más palabras porque es la que
    /// tiene una condición: "un Administrador no puede desactivarse a sí mismo". Esa
    /// condición se comprueba y se dice; no se ignora en silencio.
    /// </summary>
    /// <remarks>
    /// "No puede desactivarse a sí mismo" es un 422 y no un 403. Quien llama tiene
    /// permiso de Administrador y de hecho va a administrarlo; lo que no puede es
    /// aplicar el signo de menos sobre su propia cuenta. Un 403 diría "no estás
    /// autorizado", que es falso: está autorizado, y además es el único que puede
    /// hacer esto a cualquiera.
    /// </remarks>
    /// <param name="actorId">
    /// Quién hace la petición. Se pasa porque sólo quien llama puede compararse con
    /// <paramref name="usuarioId"/>, y el servicio no lo deduce: el host ya lo sabe.
    /// </param>
    /// <exception cref="ExcepcionDominio">
    /// <see cref="TipoError.NoEncontrado"/> si no existe ese usuario;
    /// <see cref="TipoError.ReglaDeNegocio"/> si quien llama es ese mismo usuario, o si
    /// ya estaba desactivado.
    /// </exception>
    Task<CambioDeEstado> DesactivarAsync(
        Guid usuarioId,
        Guid actorId,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Reactiva una cuenta desactivada (RF-CA-20).
    ///
    /// Vuelve a estar activo y puede volver a iniciar sesión. No le devuelve las
    /// sesiones que tenía: éstas se cerraron al desactivarlo, y devolverle la entrada
    /// por la vía corta no es lo que un Administrador quiere decir cuando desactiva a
    /// alguien. El usuario tiene que entrar otra vez.
    /// </summary>
    /// <remarks>
    /// No levanta el bloqueo por cinco intentos fallidos (RF-CA-19). Son dos cosas
    /// distintas y quien reactiva no ha pedido ninguna de las dos: desactivar es quitar
    /// el acceso a una cuenta, y el bloqueo expira solo a los quince minutos. Un
    /// Administrador que además de reactivar quiere el bloqueo limpio lo consigue
    /// esperando quince minutos, que es menos código y menos superficie.
    /// </remarks>
    /// <exception cref="ExcepcionDominio">
    /// <see cref="TipoError.NoEncontrado"/> si no existe ese usuario;
    /// <see cref="TipoError.ReglaDeNegocio"/> si ya estaba activo.
    /// </exception>
    Task<CambioDeEstado> ReactivarAsync(
        Guid usuarioId,
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
/// Resultado de <c>POST /usuarios/desactivar</c> y de
/// <c>POST /usuarios/reactivar</c> (RF-CA-20).
///
/// En la reactivación <c>SesionesCerradas</c> es siempre cero, y se dice igualmente:
/// quien lo lee quiere ver que el cambio se aplicó entero, no tener que suponerlo.
/// </summary>
public sealed record CambioDeEstado(
    Guid UsuarioId,
    bool Activo,
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
        // Si el rol no es uno de los del enum, se rechaza como regla de negocio y no se
        // toca nada. El endpoint ya traduce el texto y ha comprobado que el nombre
        // existe, así que desde HTTP esto no se puede alcanzar: queda aquí porque
        // decidir qué roles existen es de la pieza, no del host, y porque es la
        // comprobación que protege a cualquier otro que llame a este servicio. Un
        // usuario con un rol fuera de la tabla no podría ejecutar nada, y sin esto
        // nadie entendería por qué (RF-CA-04).
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

    public async Task<CambioDeEstado> DesactivarAsync(
        Guid usuarioId,
        Guid actorId,
        CancellationToken cancelacion = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancelacion);

        var usuario = await db.Usuarios
            .FirstOrDefaultAsync(u => u.Id == usuarioId, cancelacion)
            ?? throw ExcepcionDominio.NoEncontrado(
                "usuario.no_encontrado", "Usuario no encontrado.");

        // La condición que el enunciado pone en su propia frase, y por eso está en el
        // servicio y no en el endpoint: es una regla de negocio, no una comprobación de
        // transporte, y tiene que cumplirse aunque alguien escriba el endpoint de otra
        // forma mañana. Comprobarla después de haber escrito nada es lo que la hace de
        // verdad una regla (RF-CA-20).
        if (usuario.Id == actorId)
        {
            throw ExcepcionDominio.ReglaDeNegocio(
                "usuario.no_autodesactivacion",
                "Un Administrador no puede desactivar su propia cuenta.");
        }

        if (!usuario.Activo)
        {
            // Igual que en el cambio de rol: cerrar sesiones sin que nada haya cambiado
            // no es neutro, expulsa al usuario sin motivo.
            throw ExcepcionDominio.ReglaDeNegocio(
                "usuario.ya_desactivado", "El usuario ya está desactivado.");
        }

        usuario.Activo = false;

        // Cerrar ANTES de guardar, como en el cambio de contraseña y en el cambio de rol.
        // El peor desenlace de un fallo es que la cuenta siga activa con las sesiones
        // cerradas, y el peor de cerrarlo al revés es una cuenta desactivada con
        // credenciales vivas, que es exactamente lo que RF-CA-20 viene a impedir.
        var cerradas = await sesiones.CerrarTodasAsync(
            usuario.Id,
            "Desactivación por un Administrador.",
            db,
            cancelacion);

        await db.SaveChangesAsync(cancelacion);

        return new CambioDeEstado(usuario.Id, false, cerradas);
    }

    public async Task<CambioDeEstado> ReactivarAsync(
        Guid usuarioId,
        CancellationToken cancelacion = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancelacion);

        var usuario = await db.Usuarios
            .FirstOrDefaultAsync(u => u.Id == usuarioId, cancelacion)
            ?? throw ExcepcionDominio.NoEncontrado(
                "usuario.no_encontrado", "Usuario no encontrado.");

        if (usuario.Activo)
        {
            throw ExcepcionDominio.ReglaDeNegocio(
                "usuario.ya_activo", "El usuario ya está activo.");
        }

        usuario.Activo = true;

        // Ni IntentosFallidos ni BloqueadoHasta se tocan. Desactivar y bloquear son dos
        // cosas distintas (RF-CA-19, RF-CA-20) y reactivar no promete la segunda; el
        // bloqueo se levanta solo a los quince minutos.
        await db.SaveChangesAsync(cancelacion);

        return new CambioDeEstado(usuario.Id, true, 0);
    }
}