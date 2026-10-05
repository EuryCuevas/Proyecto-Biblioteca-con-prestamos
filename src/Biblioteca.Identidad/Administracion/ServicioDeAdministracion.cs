using Biblioteca.Identidad.Entidades;
using Biblioteca.Identidad.Persistencia;
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
}

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
    IDbContextFactory<IdentidadDbContext> fabrica) : IServicioDeAdministracion
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
}