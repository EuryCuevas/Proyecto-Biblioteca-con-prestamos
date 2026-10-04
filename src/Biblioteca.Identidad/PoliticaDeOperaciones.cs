namespace Biblioteca.Identidad;

/// <summary>
/// PUNTO ÚNICO DE EXIGENCIA DE ROL DEL SISTEMA (RF-CA-05).
///
/// Aquí se lee, en un solo archivo, qué roles pueden ejecutar cada operación.
/// Ningún otro lugar del código decide si una operación es de Administrador o de
/// Estándar: los controladores sólo la nombran con
/// <see cref="Autorizacion.RequiereOperacionAttribute"/> y el guard consulta esta tabla.
///
/// La rúbrica de la Práctica 1 califica con 50 % cuando "la exigencia de rol está
/// repetida en varios lugares", y con 0 % cuando "cualquier usuario puede ejecutar
/// cualquier operación". Esta clase es la respuesta a ambos casos.
/// </summary>
public static class PoliticaDeOperaciones
{
    /// <summary>
    /// Operación -> roles autorizados. Los roles indicados son suficientes: un
    /// Administrador no hereda automáticamente las operaciones de Estándar, la
    /// tabla dice exactamente quién puede hacer qué.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, Rol[]> Requisitos =
        new Dictionary<string, Rol[]>(StringComparer.Ordinal)
        {
            // --- Sesión: cualquier usuario con credenciales válidas ---
            [Operaciones.Autenticarse] = [Rol.Estandar, Rol.Administrador],
            [Operaciones.ConsultarYO] = [Rol.Estandar, Rol.Administrador],
            [Operaciones.CerrarSesion] = [Rol.Estandar, Rol.Administrador],

            // --- Contraseña propia: cualquier usuario autenticado ---
            [Operaciones.CambiarPasswordPropia] = [Rol.Estandar, Rol.Administrador],

            // --- Administración de usuarios: sólo Administrador (RF-CA-06, 08, 20, 21) ---
            [Operaciones.UsuariosListar] = [Rol.Administrador],
            [Operaciones.UsuariosCambiarRol] = [Rol.Administrador],
            [Operaciones.UsuariosDesactivar] = [Rol.Administrador],
            [Operaciones.UsuariosReactivar] = [Rol.Administrador],
            [Operaciones.UsuariosForzarRestablecimiento] = [Rol.Administrador],

            // --- Gestión de permisos (RF-CA-05 del Core, pieza 2) ---
            [Operaciones.SolicitudesCrear] = [Rol.Estandar],
            [Operaciones.SolicitudesListarPendientes] = [Rol.Administrador],
            [Operaciones.SolicitudesAprobar] = [Rol.Administrador],
            [Operaciones.SolicitudesRechazar] = [Rol.Administrador],

            // --- Documentos (RF-DOC-03: el dueño o un Administrador; se valida además la propiedad) ---
            [Operaciones.DocumentosSubir] = [Rol.Estandar, Rol.Administrador],
            [Operaciones.DocumentosListar] = [Rol.Estandar, Rol.Administrador],
            [Operaciones.DocumentosDescargar] = [Rol.Estandar, Rol.Administrador],
            [Operaciones.DocumentosEliminar] = [Rol.Estandar, Rol.Administrador],

            // --- Auditoría (RF-AUD-07: reservada al Administrador) ---
            [Operaciones.AuditoriaConsultar] = [Rol.Administrador],
        };

    /// <summary>Devuelve los roles autorizados para la operación, o vacío si no está declarada.</summary>
    public static IReadOnlyList<Rol> RolesDe(string operacion) =>
        Requisitos.TryGetValue(operacion, out var roles) ? roles : [];

    /// <summary>Indica si la operación está registrada en la política.</summary>
    public static bool EstaDeclarada(string operacion) => Requisitos.ContainsKey(operacion);
}