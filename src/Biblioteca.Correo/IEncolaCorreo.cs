namespace Biblioteca.Correo;

/// <summary>Puerto de encolado de correo (RF-NOT-08).</summary>
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