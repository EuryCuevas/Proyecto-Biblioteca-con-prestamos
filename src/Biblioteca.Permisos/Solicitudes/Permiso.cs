namespace Biblioteca.Permisos.Solicitudes;

/// <summary>
/// Permiso otorgado a un usuario tras aprobar una solicitud.
/// </summary>
public sealed class Permiso
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UsuarioId { get; set; }

    public string Operacion { get; set; } = string.Empty;

    public DateTimeOffset OtorgadoEn { get; set; }

    public Guid? SolicitudId { get; set; }
}
