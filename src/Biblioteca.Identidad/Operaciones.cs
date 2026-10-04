namespace Biblioteca.Identidad;

/// <summary>
/// Claves de las operaciones del sistema. Se usan como identificador estable en
/// <see cref="PoliticaDeOperaciones"/> y en <see cref="Autorizacion.RequiereOperacionAttribute"/>,
/// de modo que la declaración de una operación y su exigencia de rol no puedan
/// desincronizarse.
/// </summary>
public static class Operaciones
{
    // --- Sesión (RF-CA-03, RF-CA-07, RF-CA-18) ---
    public const string Autenticarse = "sesion.autenticar";
    public const string ConsultarYO = "sesion.consultarYO";
    public const string CerrarSesion = "sesion.cerrar";

    // --- Contraseña del propio usuario (RF-CA-22) ---
    public const string CambiarPasswordPropia = "password.cambiarPropia";

    // --- Administración de usuarios (RF-CA-08, RF-CA-20, RF-CA-21) ---
    public const string UsuariosListar = "usuarios.listar";
    public const string UsuariosCambiarRol = "usuarios.cambiarRol";
    public const string UsuariosDesactivar = "usuarios.desactivar";
    public const string UsuariosReactivar = "usuarios.reactivar";
    public const string UsuariosForzarRestablecimiento = "usuarios.forzarRestablecimiento";

    // --- Gestión de permisos (pieza 2, semanas 6-8) ---
    public const string SolicitudesCrear = "solicitudes.crear";
    public const string SolicitudesListarPendientes = "solicitudes.listarPendientes";
    public const string SolicitudesAprobar = "solicitudes.aprobar";
    public const string SolicitudesRechazar = "solicitudes.rechazar";

    // --- Documentos (pieza 3, semana 9) ---
    public const string DocumentosSubir = "documentos.subir";
    public const string DocumentosListar = "documentos.listar";
    public const string DocumentosDescargar = "documentos.descargar";
    public const string DocumentosEliminar = "documentos.eliminar";

    // --- Auditoría (pieza 6, semana 14) ---
    public const string AuditoriaConsultar = "auditoria.consultar";
}