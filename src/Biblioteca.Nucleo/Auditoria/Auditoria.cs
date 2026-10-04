namespace Biblioteca.Nucleo.Auditoria;

/// <summary>
/// Datos de una entrada de auditoría. Refleja los atributos mínimos que exige
/// el Core para <c>RegistroAuditoria</c> (RF-AUD-01).
///
/// REGLA INNEGOCIABLE (RF-AUD-05): aquí nunca se registra una contraseña, ni su
/// hash, ni un token o código de un solo uso. Quien construya la entrada debe
/// asegurarse de que <paramref name="ValorAnterior"/> y
/// <paramref name="ValorNuevo"/> son seguros de exponer.
/// </summary>
/// <param name="UsuarioQueActuo">Identificador del usuario que ejecutó la acción.</param>
/// <param name="Accion">Qué se hizo, por ejemplo "cambio.rol" o "documento.subida".</param>
/// <param name="EntidadAfectada">Nombre de la entidad modificada.</param>
/// <param name="EntidadId">Identificador de esa entidad.</param>
/// <param name="FechaHora">Instante en UTC, con el criterio único del sistema (RD-11).</param>
/// <param name="ValorAnterior">Estado previo, o <see langword="null"/> si no aplica.</param>
/// <param name="ValorNuevo">Estado posterior, o <see langword="null"/> si no aplica.</param>
public sealed record EntradaAuditoria(
    string UsuarioQueActuo,
    string Accion,
    string EntidadAfectada,
    string EntidadId,
    DateTimeOffset FechaHora,
    string? ValorAnterior,
    string? ValorNuevo);

/// <summary>
/// Puerto de auditoría. Vive en el núcleo para que las piezas publiquen entradas
/// sin depender todavía de la pieza 6 (Auditoría, semana 14).
///
/// En la Práctica 1 no se califica la persistencia de auditoría, así que la
/// implementación registrada es un sumidero que sólo escribe en el log.
/// En la semana 14 se enchufa <c>RegistroAuditoria</c> al mismo puerto.
/// </summary>
public interface IPublicaAuditoria
{
    Task PublicarAsync(EntradaAuditoria entrada, CancellationToken cancelacion = default);
}