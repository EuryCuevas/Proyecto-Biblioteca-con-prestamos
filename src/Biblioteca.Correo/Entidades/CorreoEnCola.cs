namespace Biblioteca.Correo.Entidades;

/// <summary>
/// Correo en cola. Conserva los atributos que el Core exige para la entidad
/// <c>CorreoEnCola</c>: destinatario, asunto, cuerpo, estado, intentos, fecha de
/// creación, fecha de envío y último error.
///
/// Ninguna operación de negocio envía correo: sólo inserta esta fila (RF-NOT-08).
/// Un proceso independiente la reclama y la entrega (RF-NOT-09).
/// </summary>
public sealed class CorreoEnCola
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Destinatario { get; set; } = string.Empty;

    public string Asunto { get; set; } = string.Empty;

    public string Cuerpo { get; set; } = string.Empty;

    public EstadoCorreo Estado { get; set; } = EstadoCorreo.Pendiente;

    /// <summary>Veces que se intentó entregar. La pieza 4 lo usa para limitar reintentos (RF-NOT-10).</summary>
    public int Intentos { get; set; }

    /// <summary>Instante UTC de encolado.</summary>
    public DateTimeOffset CreadoEn { get; set; }

    /// <summary>Instante UTC de entrega efectiva, o <see langword="null"/> si aún no se entregó.</summary>
    public DateTimeOffset? FechaEnvio { get; set; }

    /// <summary>Texto del último fallo. No debe contener credenciales ni trazas (RD-08).</summary>
    public string? UltimoError { get; set; }

    /// <summary>Motivo del correo, para que el emisor aplique la plantilla correcta.</summary>
    public string? Plantilla { get; set; }

    /// <summary>Dato auxiliar de la plantilla, por ejemplo el enlace de activación ya armado.</summary>
    public string? DatoPlantilla { get; set; }
}