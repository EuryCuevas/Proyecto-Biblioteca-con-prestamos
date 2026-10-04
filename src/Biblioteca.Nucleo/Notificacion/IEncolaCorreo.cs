namespace Biblioteca.Nucleo.Notificacion;

/// <summary>
/// Puerto de encolado de correo (RF-NOT-08).
///
/// Vive en el Core, y no en la pieza que lo implementa, porque es lo que permite que
/// <c>Biblioteca.Identidad</c> notifique un correo sin referenciar
/// <c>Biblioteca.Correo</c>: la pieza depende del PUERTO, nunca de la pieza que lo
/// resuelve. Es el mismo patrón que <see cref="Auditoria.IPublicaAuditoria"/>.
/// </summary>
public interface IEncolaCorreo
{
    /// <summary>
    /// Registra el correo y devuelve. NO abre conexión SMTP: la operación de negocio
    /// termina bien aunque el servidor de correo no responda.
    /// </summary>
    Task EncolarAsync(
        string destinatario,
        string asunto,
        string cuerpo,
        string? plantilla = null,
        string? datoPlantilla = null,
        CancellationToken cancelacion = default);
}