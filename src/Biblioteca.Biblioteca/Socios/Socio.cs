using Biblioteca.Biblioteca.Prestamos;

namespace Biblioteca.Biblioteca.Socios;

/// <summary>
/// Persona inscrita en la biblioteca. La identidad —nombre, correo, rol— pertenece a la
/// pieza Control de acceso del Core y aquí no se copia: se consulta, para que no pueda
/// quedar desactualizada.
/// </summary>
public sealed class Socio
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Identificador del Usuario del Core. Para este módulo es un dato opaco: se usa
    /// como valor y <b>no</b> como llave foránea, porque las tablas del Core no forman
    /// parte de este esquema y el Core no depende de este módulo (RD-03).
    /// </summary>
    public Guid UsuarioId { get; set; }

    /// <summary>Identificador legible de la inscripción: la credencial de la biblioteca.</summary>
    public string NumeroSocio { get; set; } = string.Empty;

    public DateTimeOffset AltaEn { get; set; }

    public bool Activo { get; set; } = true;

    /// <summary>Cupo de préstamos vivos permitidos a la vez (RF-NEG-06).</summary>
    public byte LimitePrestamosSimultaneos { get; set; } = 3;

    /// <summary>
    /// Préstamos ya vencidos que bloquean uno nuevo. El vencimiento se deriva comparando
    /// <see cref="Prestamo.FechaLimite"/> con el instante actual; no hay columna que
    /// mantenerla sincronizada.
    /// </summary>
    public byte LimitePrestamosVencidos { get; set; } = 2;

    public ICollection<Prestamo> Prestamos { get; set; } = [];
}