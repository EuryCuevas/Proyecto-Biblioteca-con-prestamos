namespace Biblioteca.Biblioteca.Prestamos;

/// <summary>
/// Entidad central del módulo de negocio. Existe en el modelo de datos con su
/// atributo de estado, que es lo que se exige en la Práctica 1 (RF-NEG-03).
///
/// Nota de alcance: en la Práctica 1 sólo se entrega la ESTRUCTURA de la máquina de
/// estados. Las operaciones de préstamo, devolución y disponibilidad se desarrollan
/// más adelante; la prueba de esta máquina llega en la semana 8.
/// </summary>
public sealed class Prestamo
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Socio que solicita el préstamo.</summary>
    public Guid SocioId { get; set; }

    /// <summary>Ejemplar concreto que se presta (copia física).</summary>
    public Guid EjemplarId { get; set; }

    /// <summary>Estado actual, regido por <see cref="TransicionesPrestamo"/>.</summary>
    public EstadoPrestamo Estado { get; set; } = EstadosPrestamo.Inicial;

    /// <summary>Instante UTC en que el socio solicitó el préstamo.</summary>
    public DateTimeOffset SolicitadoEn { get; set; }

    /// <summary>Instante UTC de la entrega del ejemplar.</summary>
    public DateTimeOffset? EntregadoEn { get; set; }

    /// <summary>Instante UTC en que el préstamo llegó a un estado terminal.</summary>
    public DateTimeOffset? ResueltoEn { get; set; }

    /// <summary>Fecha límite de devolución acordada.</summary>
    public DateTimeOffset? FechaLimite { get; set; }

    /// <summary>Motivo, cuando el estado es <see cref="EstadoPrestamo.Rechazado"/> o <see cref="EstadoPrestamo.Perdido"/>.</summary>
    public string? Motivo { get; set; }
}