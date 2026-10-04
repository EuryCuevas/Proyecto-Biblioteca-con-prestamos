namespace Biblioteca.Identidad.Entidades;

/// <summary>
/// Token de un solo uso para activar la cuenta (RF-CA-15, RF-CA-16).
/// Se guarda su hash, nunca el valor que viaja en el enlace.
/// </summary>
public sealed class TokenActivacion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    /// <summary>SHA-256 del token que viaja en el enlace del correo.</summary>
    public required byte[] TokenHash { get; set; }

    /// <summary>Instante UTC de emisión.</summary>
    public DateTimeOffset EmitidoEn { get; set; }

    /// <summary>Instante UTC de vencimiento. Vencido, no se puede usar (RF-CA-16).</summary>
    public DateTimeOffset VenceEn { get; set; }

    /// <summary>Momento en que se consumió. Segundo uso o uso vencido ⇒ rechazo (RF-CA-16).</summary>
    public DateTimeOffset? UsadoEn { get; set; }

    public bool EstaVigente(DateTimeOffset ahora) => UsadoEn is null && VenceEn > ahora;
}