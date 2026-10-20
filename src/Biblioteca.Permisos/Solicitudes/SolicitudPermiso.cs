using Biblioteca.Nucleo.Errores;

namespace Biblioteca.Permisos.Solicitudes;

/// <summary>
/// Estados de una solicitud de permiso. Los cuatro son los que pide la asignación.
/// </summary>
public enum EstadoSolicitud
{
    Pendiente = 0,
    Aprobada = 1,
    Rechazada = 2,
    Aplicada = 3
}

/// <summary>Declaración única de los estados (calcado del patrón de la máquina de negocio).</summary>
public static class EstadosSolicitud
{
    public static readonly IReadOnlyList<EstadoSolicitud> Todos = new[]
    {
        EstadoSolicitud.Pendiente,
        EstadoSolicitud.Aprobada,
        EstadoSolicitud.Rechazada,
        EstadoSolicitud.Aplicada
    };

    public static readonly IReadOnlySet<EstadoSolicitud> Terminales = new HashSet<EstadoSolicitud>
    {
        EstadoSolicitud.Rechazada,
        EstadoSolicitud.Aplicada
    };

    public const EstadoSolicitud Inicial = EstadoSolicitud.Pendiente;
}

/// <summary>Una transición permitida de la máquina de estados de solicitudes.</summary>
public sealed record TransicionSolicitud(
    EstadoSolicitud Desde,
    EstadoSolicitud Hacia,
    string Quien,
    string Condicion);

/// <summary>Tabla única de transiciones (RD-04). Solo las 3 válidas.</summary>
public static class TransicionesSolicitud
{
    public static readonly IReadOnlyList<TransicionSolicitud> Permitidas = new List<TransicionSolicitud>
    {
        new(EstadoSolicitud.Pendiente, EstadoSolicitud.Aprobada, "Administrador", "Aprueba una solicitud pendiente"),
        new(EstadoSolicitud.Aprobada, EstadoSolicitud.Aplicada, "Sistema", "Aplica el cambio de permiso automáticamente"),
        new(EstadoSolicitud.Pendiente, EstadoSolicitud.Rechazada, "Administrador", "Rechaza con motivo una solicitud pendiente")
    };

    private static readonly HashSet<(EstadoSolicitud Desde, EstadoSolicitud Hacia)> PermitidasSet =
        Permitidas.Select(t => (t.Desde, t.Hacia)).ToHashSet();

    public static bool EsPermitida(EstadoSolicitud desde, EstadoSolicitud hacia) =>
        PermitidasSet.Contains((desde, hacia));
}

/// <summary>Entidad de solicitud de permiso.</summary>
public sealed class SolicitudPermiso
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SolicitanteId { get; set; }

    public string Operacion { get; set; } = string.Empty;

    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;

    public DateTimeOffset CreadaEn { get; set; }

    public Guid? AprobadaPorId { get; set; }

    public DateTimeOffset? AprobadaEn { get; set; }

    public DateTimeOffset? AplicadaEn { get; set; }

    public Guid? RechazadaPorId { get; set; }

    public DateTimeOffset? RechazadaEn { get; set; }

    public string? MotivoRechazo { get; set; }

    public void Aprobar(Guid adminId, DateTimeOffset momento)
    {
        if (adminId == SolicitanteId)
        {
            throw ExcepcionDominio.ReglaDeNegocio("solicitud.autoResolver", "Nadie puede resolver su propia solicitud.");
        }

        if (!TransicionesSolicitud.EsPermitida(Estado, EstadoSolicitud.Aprobada))
        {
            throw ExcepcionDominio.ReglaDeNegocio("solicitud.transicionProhibida", "La transición no está permitida.");
        }

        Estado = EstadoSolicitud.Aprobada;
        AprobadaPorId = adminId;
        AprobadaEn = momento;
    }

    public void Rechazar(Guid adminId, string motivo, DateTimeOffset momento)
    {
        if (adminId == SolicitanteId)
        {
            throw ExcepcionDominio.ReglaDeNegocio("solicitud.autoResolver", "Nadie puede resolver su propia solicitud.");
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw ExcepcionDominio.Validacion("solicitud.motivoRequerido", "El motivo de rechazo es obligatorio.");
        }

        if (!TransicionesSolicitud.EsPermitida(Estado, EstadoSolicitud.Rechazada))
        {
            throw ExcepcionDominio.ReglaDeNegocio("solicitud.transicionProhibida", "La transición no está permitida.");
        }

        Estado = EstadoSolicitud.Rechazada;
        RechazadaPorId = adminId;
        RechazadaEn = momento;
        MotivoRechazo = motivo.Trim();
    }

    public void Aplicar(DateTimeOffset momento)
    {
        if (!TransicionesSolicitud.EsPermitida(Estado, EstadoSolicitud.Aplicada))
        {
            throw ExcepcionDominio.ReglaDeNegocio("solicitud.transicionProhibida", "La transición no está permitida.");
        }

        Estado = EstadoSolicitud.Aplicada;
        AplicadaEn = momento;
    }
}
